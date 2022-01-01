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
        private static MethodBase TargetMethod()
        {
            return DiscordPlaysService.IRCConnectionType.GetMethod("Connect", AccessTools.all);
        }

        private static bool Prefix()
        {
            return DiscordPlaysService.EnableTwitchInput;
        }
    }

    [HarmonyPatch]
    public static class ConnectButtonPatch
    {
        private static MethodBase TargetMethod()
        {
            return ReflectionHelper.FindType("IRCConnectionManagerHoldable", DiscordPlaysService.TwitchPlaysAssembly)
                .GetMethod("ConnectDisconnect", AccessTools.all);
        }

        private static bool Prefix(out bool __result)
        {
            __result = false;
            return DiscordPlaysService.EnableTwitchInput;
        }
    }

    [HarmonyPatch]
    public static class SendMessagePatch
    {
        internal static bool SkipResend;
        internal static MethodInfo SendMessageMethod;

        private static MethodBase TargetMethod()
        {
            SendMessageMethod = DiscordPlaysService.IRCConnectionType.GetMethod("SendMessage", AccessTools.all,
                Type.DefaultBinder,
                new[]
                {
                    typeof(string), typeof(string), typeof(bool)
                }, null);
            return SendMessageMethod;
        }

        private static bool Prefix(string message)
        {
            if (!SkipResend)
                DiscordPlaysService.ws.Send(message);
            return DiscordPlaysService.EnableTwitchInput;
        }
    }

    [HarmonyPatch]
    public static class ReceiveMessagePatch
    {
        internal static bool FromDiscord;

        private static FieldInfo NicknameField;
        private static PropertyInfo TextProperty;

        private static MethodBase TargetMethod()
        {
            var IRCMessage = ReflectionHelper.FindType("IRCMessage", DiscordPlaysService.TwitchPlaysAssembly);
            NicknameField = IRCMessage.GetField("UserNickName", AccessTools.all);
            TextProperty = IRCMessage.GetProperty("Text", AccessTools.all);
            return DiscordPlaysService.IRCConnectionType.GetMethod("ReceiveMessage", AccessTools.all,
                Type.DefaultBinder,
                new[]
                {
                    IRCMessage, typeof(bool)
                }, null);
        }

        private static bool Prefix(object msg)
        {
            var EnableTwitchInput = DiscordPlaysService.EnableTwitchInput;
            bool cont = FromDiscord || EnableTwitchInput;
            if (EnableTwitchInput)
            {
                var Message = string.Format("[{0}] {1}", (string) NicknameField.GetValue(msg),
                    (string) TextProperty.GetValue(msg, null));
                if (!FromDiscord)
                {
                    DiscordPlaysService.ws.Send(Message);
                }
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