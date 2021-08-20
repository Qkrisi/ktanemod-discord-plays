using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
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

    private WebSocketSharp.WebSocket ws;
    private Queue<Action> ActionQueue = new Queue<Action>();
    
    internal TokenInputPage _tokenInputPage;
    

    private Dictionary<DiscordPlaysService.DefaultServers, string> DefaultURLs =
        new Dictionary<DiscordPlaysService.DefaultServers, string>()
        {
            {DiscordPlaysService.DefaultServers.Main, "server.qkrisi.tech:8080"},
            {DiscordPlaysService.DefaultServers.Beta, "server.qkrisi.tech:8880"}
        };
    
    internal volatile WSState CurrentState = WSState.Disconnected;

    internal bool PageActive;

    internal string Token;

    public void Connect()
    {
        if (ws!=null && ws.IsAlive)
            return;
        _tokenInputPage.ErrorText.SetActive(false);
        Debug.Log("[Discord Plays] Connecting");
        CurrentState = WSState.Changing;
        OnDestroy();
        var settings = DiscordPlaysService.settings;
        bool OverrideURL = !String.IsNullOrEmpty(settings.URLOverride);
        ws = new WebSocketSharp.WebSocket(String.Format("{0}://{1}", OverrideURL && settings.UseWSSOnOverride ? "wss" : "ws",
            OverrideURL ? settings.URLOverride : DefaultURLs[settings.Server]));
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
            if(PageActive)
                _tokenInputPage.ErrorText.SetActive(true);
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
            ws = null;
        }
    }
}
