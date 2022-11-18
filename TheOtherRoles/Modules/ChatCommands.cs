using System;
using System.Linq;
using HarmonyLib;
using static TheOtherRoles.TheOtherRoles;

namespace TheOtherRoles.Modules
{
    [HarmonyPatch]
    public static class ChatCommands
    {

        [HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
        private static class SendChatPatch
        {
            static bool Prefix(ChatController __instance)
            {
                string text = __instance.TextArea.text;
                bool handled = false;
                if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
                {
                    if (text.ToLower().StartsWith("/kick "))
                    {
                        string playerName = text[6..];
                        PlayerControl target = PlayerControl.AllPlayerControls.GetFastEnumerator().ToArray().ToList().FirstOrDefault(x => x.Data.PlayerName.Equals(playerName));
                        if (target != null && AmongUsClient.Instance != null && AmongUsClient.Instance.CanBan())
                        {
                            var client = AmongUsClient.Instance.GetClient(target.OwnerId);
                            if (client != null)
                            {
                                AmongUsClient.Instance.KickPlayer(client.Id, false);
                                handled = true;
                            }
                        }
                    }
                    else if (text.ToLower().StartsWith("/ban "))
                    {
                        string playerName = text[5..];
                        PlayerControl target = PlayerControl.AllPlayerControls.GetFastEnumerator().ToArray().ToList().FirstOrDefault(x => x.Data.PlayerName.Equals(playerName));
                        if (target != null && AmongUsClient.Instance != null && AmongUsClient.Instance.CanBan())
                        {
                            var client = AmongUsClient.Instance.GetClient(target.OwnerId);
                            if (client != null)
                            {
                                AmongUsClient.Instance.KickPlayer(client.Id, true);
                                handled = true;
                            }
                        }
                    }
                }

                if (AmongUsClient.Instance.GameMode == GameModes.FreePlay)
                {
                    if (text.ToLower().Equals("/murder"))
                    {
                        CachedPlayer.LocalPlayer.PlayerControl.Exiled();
                        FastDestroyableSingleton<HudManager>.Instance.KillOverlay.ShowKillAnimation(CachedPlayer.LocalPlayer.PlayerControl.Data, CachedPlayer.LocalPlayer.PlayerControl.Data);
                        handled = true;
                    }
                    else if (text.ToLower().StartsWith("/color "))
                    {
                        handled = true;
                        if (!Int32.TryParse(text[7..], out int col))
                        {
                            __instance.AddChat(CachedPlayer.LocalPlayer.PlayerControl, "Unable to parse color id\nUsage: /color {id}");
                        }
                        col = Math.Clamp(col, 0, Palette.PlayerColors.Length - 1);
                        CachedPlayer.LocalPlayer.PlayerControl.SetColor(col);
                        __instance.AddChat(CachedPlayer.LocalPlayer.PlayerControl, "Changed color successfully"); ;
                    }
                }

                if (text.ToLower().StartsWith("/tp ") && CachedPlayer.LocalPlayer.PlayerControl.Data.IsDead)
                {
                    string playerName = text[4..].ToLower();
                    PlayerControl target = PlayerControl.AllPlayerControls.GetFastEnumerator().ToArray().ToList().FirstOrDefault(x => x.Data.PlayerName.ToLower().Equals(playerName));
                    if (target != null)
                    {
                        CachedPlayer.LocalPlayer.PlayerControl.transform.position = target.transform.position;
                        handled = true;
                    }
                }

                if (handled)
                {
                    __instance.TextArea.Clear();
                    __instance.quickChatMenu.ResetGlyphs();
                }
                return !handled;
            }
        }

        [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
        public static class EnableChat
        {
            public static void Postfix(HudManager __instance)
            {
                if (__instance?.Chat?.isActiveAndEnabled == false && (AmongUsClient.Instance?.GameMode == GameModes.FreePlay || (CachedPlayer.LocalPlayer.PlayerControl.isLovers() && Lovers.enableChat)))
                    __instance?.Chat?.SetVisible(true);
            }
        }

        [HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetName))]
        public static class SetBubbleName
        {
            public static void Postfix(ChatBubble __instance, [HarmonyArgument(0)] string playerName)
            {
                PlayerControl sourcePlayer = PlayerControl.AllPlayerControls.GetFastEnumerator().ToArray().ToList().FirstOrDefault(x => x.Data.PlayerName.Equals(playerName));
                if (CachedPlayer.LocalPlayer.PlayerControl != null && CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor && ((Spy.spy != null && sourcePlayer.PlayerId == Spy.spy.PlayerId) || (Sidekick.sidekick != null && Sidekick.wasTeamRed && sourcePlayer.PlayerId == Sidekick.sidekick.PlayerId) || (Jackal.jackal != null && Jackal.wasTeamRed && sourcePlayer.PlayerId == Jackal.jackal.PlayerId)) && __instance != null) __instance.NameText.color = Palette.ImpostorRed;
            }
        }

        [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
        public static class AddChatPatch
        {
            public static bool Prefix(ChatController __instance, [HarmonyArgument(0)] PlayerControl sourcePlayer)
            {
                if (__instance != FastDestroyableSingleton<HudManager>.Instance.Chat)
                    return true;
                PlayerControl localPlayer = CachedPlayer.LocalPlayer.PlayerControl;
                return localPlayer == null ||
                    MeetingHud.Instance != null || LobbyBehaviour.Instance != null ||
                    localPlayer.isDead() || localPlayer.PlayerId == sourcePlayer.PlayerId ||
                    (Lovers.enableChat && localPlayer.getPartner() == sourcePlayer);
            }
        }
    }
}
