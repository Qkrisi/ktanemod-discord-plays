using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using WebSocketSharp;
using DiscordPlays;

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
    
#pragma warning disable 649
    private class DiscordMessage
    {
        public string Message;
        public string User;
        public string Color;
    }
#pragma warning restore 649


    private new bool enabled;

    private WebSocket ws;
    private Queue<Action> ActionQueue = new Queue<Action>();
    
    internal TokenInputPage _tokenInputPage;

    internal volatile WSState CurrentState = WSState.Disconnected;

    internal bool PageActive;

    internal string Token;

    [SerializeField]
    private string DefaultURL;

    public void Connect()
    {
        if (ws!=null && ws.IsAlive)
            return;
        _tokenInputPage.ErrorText.SetActive(false);
        Debug.Log("[Discord Plays] Connecting");
        CurrentState = WSState.Changing;
        OnDestroy();
        var settings = DiscordPlaysService.settings;
        settings.URLOverride = settings.URLOverride.Trim();
        bool OverrideURL = !String.IsNullOrEmpty(settings.URLOverride);
        try
        {
            ws = new WebSocket(String.Format("{0}://{1}", OverrideURL && settings.UseWSSOnOverride ? "wss" : "ws",
                OverrideURL ? settings.URLOverride : String.Format("{0}:{1}", DefaultURL, (int) settings.Server)));
        }
        catch (Exception ex)
        {
            CurrentState = WSState.Disconnected;
            if(PageActive)
                _tokenInputPage.ErrorText.SetActive(true);
            Debug.LogError("[Discord Plays] Failed to connect");
            Debug.LogException(ex);
            return;
        }
        ws.OnMessage += (sender, e) =>
        {
            if (!enabled)
                return;
            var message = JsonConvert.DeserializeObject<DiscordMessage>(e.Data);
            if (message != null)
                lock(ActionQueue)
                    ActionQueue.Enqueue(() =>
                    {
                        ReceiveMessagePatch.FromDiscord = true;
                        DiscordPlaysService.ReceiveMessageMethod.Invoke(null, new object[]
                        {
                            message.User, message.Color, message.Message, false, false
                        });
                    });
        };
        ws.OnError += (sender, e) =>
        {
            lock(ActionQueue)
                ActionQueue.Enqueue(() =>
                {
                    if(PageActive)
                        _tokenInputPage.ErrorText.SetActive(true);
                });
            Debug.LogErrorFormat("[Discord Plays] Websocket error: {0}", e.Message);
            Debug.LogException(e.Exception);
            CurrentState = WSState.Disconnected;
        };
        ws.OnOpen += (sender, e) =>
        {
            Debug.Log("[Discord Plays] Connection successful");
            CurrentState = WSState.Connected;
            ws.Send(Token);
        };
        ws.OnClose += (sender, e) =>
        {
            Debug.Log("[Discord Plays] Connection closed");
            CurrentState = WSState.Disconnected;
        };
        ws.Connect();
    }

    public void Send(string message)
    {
        if (enabled && ws!=null && ws.IsAlive)
            ws.Send(message);
    }

    void Update()
    {
        lock (ActionQueue)
        {
            if (ActionQueue.Count > 0)
                ActionQueue.Dequeue().Invoke();
        }
        if (PageActive)
        {
            _tokenInputPage.StatusText.text = CurrentState.ToString();
            _tokenInputPage.ConnectButton.gameObject.SetActive(CurrentState != WSState.Changing);
            _tokenInputPage.ButtonText.text = CurrentState == WSState.Connected ? "Disconnect" : "Connect";
        }
    }

    void OnEnable()
    {
        enabled = true;
    }

    void OnDisable()
    {
        enabled = false;
    }

    public void OnDestroy()
    {
        if (ws != null)
        {
            CurrentState = WSState.Changing;
            if (ws.IsAlive)
                ws.Close();
            ((IDisposable)ws).Dispose();
            ws = null;
        }
    }
}
