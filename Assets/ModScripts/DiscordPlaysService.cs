using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DiscordPlays;

[DisallowMultipleComponent]
[RequireComponent(typeof(WSHandler))]
[RequireComponent(typeof(KMService))]
[RequireComponent(typeof(KMGameInfo))]
public class DiscordPlaysService : MonoBehaviour
{
    public enum DefaultServers
    {
        Main,
        Beta
    }
    
    public class DiscordPlaysSettings
    {
        public string URLOverride = "";
        public bool UseWSSOnOverride = false;
        public bool EnableTwitchInput = false;
        public DefaultServers Server = DefaultServers.Main;
    }

    internal static DiscordPlaysSettings settings;

    internal static Type IRCConnectionType = null;

    internal static MethodInfo ReceiveMessageMethod;

    internal static WSHandler ws;
    

    public static void RefreshSettings()
    {
        settings = ModConfigHelper.ReadConfig<DiscordPlaysSettings>("DiscordPlays");
        if (settings == null)
            settings = new DiscordPlaysSettings();
    }

    public static void Connect()
    {
        RefreshSettings();
        ws.Connect();
    }

    void Awake()
    {
        ws = GetComponent<WSHandler>();
        GetComponent<KMGameInfo>().OnStateChange += state =>
        {
            if (IRCConnectionType == null && state == KMGameInfo.State.Setup)
            {
                IRCConnectionType = ReflectionHelper.FindType("IRCConnection", "TwitchPlaysAssembly");
                if (IRCConnectionType != null)
                {
                    RefreshSettings();
                    ReceiveMessageMethod = IRCConnectionType.GetMethod("ReceiveMessage", ReflectionHelper.AllFlags,
                        Type.DefaultBinder,
                        new Type[]
                        {
                            typeof(string), typeof(string), typeof(string), typeof(bool), typeof(bool)
                        }, null);
                    Patcher.Patch();
                }
            }
        };
        StartCoroutine(FindModSelector());
    }
    
    public TokenInputPage _TokenInputPage;
    public Texture2D ModSelectorIcon;

    private static IDictionary<string, object> ModSelectorApi;
    private IEnumerator FindModSelector()
    {
        while (true)
        {
            GameObject modSelectorObject = GameObject.Find("ModSelector_Info");
            if (modSelectorObject != null)
            {
                ModSelectorApi = modSelectorObject.GetComponent<IDictionary<string, object>>();
                RegisterService();
                yield break;
            }

            yield return null;
        }
    }
    
    private void RegisterService()
    {
        KMSelectable selectable = _TokenInputPage.GetComponent<KMSelectable>();
        Action<KMSelectable> addPageMethod = (Action<KMSelectable>)ModSelectorApi["AddPageMethod"];
        addPageMethod(selectable);

        Action<string, KMSelectable, Texture2D> addHomePageMethod = (Action<string, KMSelectable, Texture2D>)ModSelectorApi["AddHomePageMethod"];
        addHomePageMethod("Discord Plays: KTaNE", selectable, ModSelectorIcon);
    }
}