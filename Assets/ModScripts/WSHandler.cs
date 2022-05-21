using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DiscordPlays;
using Newtonsoft.Json;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Net;

[DisallowMultipleComponent]
[RequireComponent(typeof(DiscordPlaysService))]
public class WSHandler : MonoBehaviour
{
    public enum WSState
    {
        Disconnected,
        Changing,
        Connected
    }
    
    private struct GameInfo
    {
        public string Version
        {
            get
            {
                return Application.version;
            }
        }
        public bool Retry { get; set; }
    }

    [SerializeField] private string DefaultURL;
    private readonly Queue<Action> ActionQueue = new Queue<Action>();

    private readonly ushort[] RetryCodes =
    {
        1002,
        1006
    };
    
    private readonly ushort[] ErrorCodes = 
    {
        1003,
        1014
    };

    internal TokenInputPage _tokenInputPage;

    internal volatile WSState CurrentState = WSState.Disconnected;


    private new bool enabled;

    internal bool PageActive;

    private bool Retry;
    private Coroutine RetryRoutine;
    private Uri RetryURI;

    internal string Token;

    private WebSocket ws;

    private void Update()
    {
        lock (ActionQueue)
        {
            if (ActionQueue.Count > 0)
                ActionQueue.Dequeue().Invoke();
        }

        if (PageActive)
        {
			try
			{
				_tokenInputPage.StatusText.text = CurrentState.ToString();
				_tokenInputPage.ConnectButton.gameObject.SetActive(CurrentState != WSState.Changing);
				_tokenInputPage.ButtonText.text = CurrentState == WSState.Connected ? "Disconnect" : "Connect";
			}
			catch(NullReferenceException) {}
        }
    }

    private void OnEnable()
    {
        enabled = true;
    }

    private void OnDisable()
    {
        enabled = false;
    }

    public void OnDestroy()
    {
        if (ws != null)
        {
            CurrentState = WSState.Changing;
            RetryURI = ws.Url;
            if (ws.IsAlive)
                ws.Close();
            ((IDisposable) ws).Dispose();
            ws = null;
        }
    }
    
    private string GetCookie(bool retry)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new GameInfo {Retry = retry})));
    }
    
    private void SetErrorText(string text)
    {
        lock (ActionQueue)
        {
            ActionQueue.Enqueue(() =>
            {
                if (PageActive)
                {
                    try
                    {
                        _tokenInputPage.ErrorText.text = text;
                        _tokenInputPage.ErrorText.gameObject.SetActive(true);
                    }
                    catch(NullReferenceException) {}
                }
            });
        }
    }

    public void Connect(bool retry)
    {
        if (ws != null && ws.IsAlive)
            return;
        retry &= RetryURI != null;
        if (!retry && RetryRoutine != null)
        {
            Retry = false;
            StopCoroutine(RetryRoutine);
            RetryRoutine = null;
            RetryURI = null;
        }
		
		if(PageActive)
		{
			try
			{
				_tokenInputPage.ErrorText.gameObject.SetActive(false);
			}
			catch(NullReferenceException) {}
		}
        Debug.LogFormat("[Discord Plays] {0}", retry ? "Retrying connection" : "Connecting");
        CurrentState = WSState.Changing;
        OnDestroy();
        var settings = DiscordPlaysService.settings;
        if(settings.URLOverride == null)
			settings.URLOverride = "";
        settings.URLOverride = settings.URLOverride.Trim();
        var OverrideURL = !string.IsNullOrEmpty(settings.URLOverride);
        try
        {
            ws = new WebSocket(!retry
                ? string.Format("{0}://{1}", OverrideURL && settings.UseWSSOnOverride ? "wss" : "ws",
                    OverrideURL ? settings.URLOverride : string.Format("{0}:{1}", DefaultURL, (int) settings.Server))
                : string.Format("{0}://{1}:{2}", RetryURI.Scheme, RetryURI.DnsSafeHost, RetryURI.Port));
        }
        catch (Exception ex)
        {
            CurrentState = WSState.Disconnected;
            if (PageActive)
            {
				try
				{
					_tokenInputPage.ErrorText.gameObject.SetActive(true);
				}
				catch(NullReferenceException) {}
			}
            Debug.LogError("[Discord Plays] Failed to connect");
            Debug.LogException(ex);
            return;
        }

        ws.SetCookie(new Cookie("GameInfo", GetCookie(retry)));

        ws.OnMessage += (sender, e) =>
        {
            if (!enabled)
                return;
            var msg = e.Data;
            var match = Regex.Match(msg, @"streamer (.+)");
            if (match.Success && DiscordPlaysService.AddUserMethod != null)
            {
                lock (ActionQueue)
                {
                    ActionQueue.Enqueue(() =>
                    {
                        DiscordPlaysService.AddUserMethod.Invoke(null,
                            new object[] { match.Groups[1].Value, 0x2000 | 0x4000 | 0x8000 | 0x10000 });
                        if (DiscordPlaysService.WriteAccessListMethod != null)
                            DiscordPlaysService.WriteAccessListMethod.Invoke(null, new object[0]);
                    });
                }
                return;
            }
            var message = JsonConvert.DeserializeObject<DiscordMessage>(msg);
            if (message != null)
                lock (ActionQueue)
                {
                    ActionQueue.Enqueue(() =>
                    {
                        ReceiveMessagePatch.FromDiscord = true;
                        if(DiscordPlaysService.ReceiveMessageMethod == null)
							Debug.LogError("Receive message method could not be found in Twitch Plays");
                        else DiscordPlaysService.ReceiveMessageMethod.Invoke(null, new object[]
                        {
                            message.User, message.Color, message.Message, false, false
                        });
                    });
                }
        };
        ws.OnError += (sender, e) =>
        {
            SetErrorText("ERROR: " + e.Message);
            Debug.LogErrorFormat("[Discord Plays] Websocket error: {0}", e.Message);
            Debug.LogException(e.Exception);
            CurrentState = WSState.Disconnected;
        };
        ws.OnOpen += (sender, e) =>
        {
            Debug.Log("[Discord Plays] Connection successful");
            CurrentState = WSState.Connected;
            Send(Token);
        };
        ws.OnClose += (sender, e) =>
        {
            lock (ActionQueue)
            {
                ActionQueue.Enqueue(() =>
                {
                    Retry = !e.WasClean && RetryCodes.Contains(e.Code);
                    if (RetryRoutine == null)
                        RetryRoutine = StartCoroutine(HandleRetry());
                    var msg = "[Discord Plays] Connection closed ";
                    if (e.WasClean)
                        msg += "(clean) ";
                    else SetErrorText("Connection error");
                    if(e.WasClean && ErrorCodes.Contains(e.Code))
                        SetErrorText(string.Format("ERROR: {0} ({1})", e.Reason, e.Code));
                    Debug.LogFormat("{0}//{1} {2}", msg, e.Code, e.Reason);
                    CurrentState = WSState.Disconnected;
                });
            }
        };
        Retry = false;
        ws.Connect();
    }

    private IEnumerator HandleRetry()
    {
		yield return new WaitForSecondsRealtime(.5f);
        while (Retry)
        {
			Connect(true);
            yield return new WaitForSecondsRealtime(5f);
        }
        RetryRoutine = null;
    }

    public void Send(string message)
    {
        if (enabled && ws != null && ws.IsAlive)
            ws.Send(message);
    }

#pragma warning disable 649
    private class DiscordMessage
    {
        public string Color;
        public string Message;
        public string User;
    }
#pragma warning restore 649
}
