using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DiscordPlays;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WSHandler))]
[RequireComponent(typeof(KMService))]
[RequireComponent(typeof(KMGameInfo))]
public class DiscordPlaysService : MonoBehaviour
{
    internal const string SettingsFile = "DiscordPlays.json";

    internal static DiscordPlaysSettings settings = new DiscordPlaysSettings();

    internal static Type IRCConnectionType;

    internal static MethodInfo ReceiveMessageMethod;

    internal static WSHandler ws;

    private static IDictionary<string, object> ModSelectorApi;

    public static Dictionary<string, object>[] TweaksEditorSettings =
    {
        new Dictionary<string, object>
        {
            {"Filename", SettingsFile},
            {"Name", "Discord Plays: KTaNE"},
            {
                "Listings", new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        {"Key", "URLOverride"}, {"Text", "URL Override"},
                        {"Description", "Discord Plays URL for private instance of the KTaNE Bot"}
                    },
                    new Dictionary<string, object>
                    {
                        {"Key", "UseWSSOnOverride"}, {"Text", "Use WSS on override"},
                        {"Description", "Use SSL connection when using an overridden URL"}
                    },
                    new Dictionary<string, object>
                    {
                        {"Key", "BlockTwitchInput"},
                        {"Text", "Block Twitch input"},
                        {"Description", "Sets when to block input and output from Twitch"},
                        {"Type", "Dropdown"},
                        {"DropdownItems", new List<object> {"Always", "WhenConnected", "Never"}}
                    },
                    new Dictionary<string, object>
                    {
                        {"Key", "Server"},
                        {"Text", "KTaNE Bot server"},
                        {"Description", "The KTaNE Bot instance to use when the URL is not overridden"},
                        {"Type", "Dropdown"},
                        {"DropdownItems", new List<object> {"Main", "Beta"}}
                    }
                }
            }
        }
    };

    public TokenInputPage _TokenInputPage;
    public Texture2D ModSelectorIcon;

    internal static bool EnableTwitchInput
    {
        get
        {
            return settings.BlockTwitchInput == BlockTwitchOption.Never ||
                   settings.BlockTwitchInput == BlockTwitchOption.WhenConnected &&
                   ws.CurrentState != WSHandler.WSState.Connected;
        }
    }

    private void Awake()
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
                        new[]
                        {
                            typeof(string), typeof(string), typeof(string), typeof(bool), typeof(bool)
                        }, null);
                    Patcher.Patch();
                }
            }
        };
        StartCoroutine(FindModSelector());
    }


    public static void RefreshSettings()
    {
        settings = ModConfigHelper.ReadConfig<DiscordPlaysSettings>(SettingsFile);
    }

    public static void Connect()
    {
        RefreshSettings();
        ws.Connect(false);
    }

    private IEnumerator FindModSelector()
    {
        while (true)
        {
            var modSelectorObject = GameObject.Find("ModSelector_Info");
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
        var selectable = _TokenInputPage.GetComponent<KMSelectable>();
        var addPageMethod = (Action<KMSelectable>) ModSelectorApi["AddPageMethod"];
        addPageMethod(selectable);

        var addHomePageMethod = (Action<string, KMSelectable, Texture2D>) ModSelectorApi["AddHomePageMethod"];
        addHomePageMethod("Discord Plays: KTaNE", selectable, ModSelectorIcon);
    }

    internal enum DefaultServers
    {
        Main = 8080,
        Beta = 8880
    }

    internal enum BlockTwitchOption
    {
        Always,
        WhenConnected,
        Never
    }

    internal class DiscordPlaysSettings
    {
        public BlockTwitchOption BlockTwitchInput = BlockTwitchOption.WhenConnected;
        public DefaultServers Server = DefaultServers.Main;
        public string URLOverride = "";
        public bool UseWSSOnOverride = false;
    }
}