using System;
using System.Reflection;
using HarmonyLib;

namespace DiscordPlays
{
    public static class Patcher
    {
        public static void Patch()
        {
            new Harmony("qkrisi.discordplays_ktane").PatchAll();
        }
    }

    [HarmonyPatch]
    public static class ConnectPatch
    {
        static MethodBase TargetMethod()
        {
            return DiscordPlaysService.IRCConnectionType.GetMethod("Connect", AccessTools.all);
        }
        
        static bool Prefix()
        {
            return DiscordPlaysService.settings.EnableTwitchInput;
        }
    }

    [HarmonyPatch]
    public static class ConnectButtonPatch
    {
        static MethodBase TargetMethod()
        {
            return ReflectionHelper.FindType("IRCConnectionManagerHoldable", "TwitchPlaysAssembly")
                .GetMethod("ConnectDisconnect", AccessTools.all);
        }

        static bool Prefix(out bool __result)
        {
            __result = false;
            return DiscordPlaysService.settings.EnableTwitchInput;
        }
    }
    
    [HarmonyPatch]
    public static class SendMessagePatch
    {
        internal static bool SkipResend;
        internal static MethodInfo SendMessageMethod;
        
        static MethodBase TargetMethod()
        {
            SendMessageMethod = DiscordPlaysService.IRCConnectionType.GetMethod("SendMessage", AccessTools.all, Type.DefaultBinder,
                new Type[]
                {
                    typeof(string), typeof(string), typeof(bool)
                }, null);
            return SendMessageMethod;
        }

        static bool Prefix(string message)
        {
            if(!SkipResend)
                DiscordPlaysService.ws.Send(message);
            return DiscordPlaysService.settings.EnableTwitchInput;
        }
    }

    [HarmonyPatch]
    public static class ReceiveMessagePatch
    {
        internal static bool FromDiscord;

        internal static FieldInfo NicknameField;
        internal static PropertyInfo TextProperty;
        
        static MethodBase TargetMethod()
        {
            Type IRCMessage = ReflectionHelper.FindType("IRCMessage", "TwitchPlaysAssembly");
            NicknameField = IRCMessage.GetField("UserNickName", AccessTools.all);
            TextProperty = IRCMessage.GetProperty("Text", AccessTools.all);
            return DiscordPlaysService.IRCConnectionType.GetMethod("ReceiveMessage", AccessTools.all,
                Type.DefaultBinder,
                new Type[]
                {
                    IRCMessage, typeof(bool)
                }, null);
        }

        static bool Prefix(object msg)
        {
            bool EnableTwitchInput = DiscordPlaysService.settings.EnableTwitchInput;
            bool cont = FromDiscord || EnableTwitchInput;
            if (EnableTwitchInput)
            {
                string Message = String.Format("[{0}] {1}", (string) NicknameField.GetValue(msg),
                    (string) TextProperty.GetValue(msg, null));
                if(!FromDiscord)
                    DiscordPlaysService.ws.Send(Message);
                else
                {
                    SendMessagePatch.SkipResend = true;
                    SendMessagePatch.SendMessageMethod.Invoke(null, new object[] {Message, null, true});
                    SendMessagePatch.SkipResend = false;
                }
            }
            FromDiscord = false;
            return cont;
        }
    }
}