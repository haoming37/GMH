using HarmonyLib;
using TheOtherRoles.Objects;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Hazel;

namespace TheOtherRoles
{
    [HarmonyPatch]
    public class SoulPlayer
    {
        public static Color color = Palette.CrewmateBlue;
        private static CustomButton senriganButton;
        private static bool enableSenrigan { get { return CustomOptionHolder.enableSenrigan.getBool(); } }
        public static bool toggle = false;
        public static Sprite senriganIcon;
        public static Dictionary<byte, float> killTimers;
        public static float updateInterval = 2f;
        public static float timer = 0;
        public static TMPro.TMP_Text statusText = null;
        public static void senrigan()
        {
            var hm = FastDestroyableSingleton<HudManager>.Instance;
            if (toggle)
            {
                Camera.main.orthographicSize /= 6f;
                hm.UICamera.orthographicSize /= 6f;
                hm.transform.localScale /= 6f;
            }
            else
            {
                Camera.main.orthographicSize *= 6f;
                hm.UICamera.orthographicSize *= 6f;
                hm.transform.localScale *= 6f;
            }
            toggle = !toggle;
        }

        public static void FixedUpdate()
        {
            if (CustomOptionHolder.deadImpostorCanSeeKillColdown.getBool() && CachedPlayer.LocalPlayer.PlayerControl.isImpostor() && CachedPlayer.LocalPlayer.PlayerControl.isAlive())
            {
                timer += Time.fixedDeltaTime;
                if (timer >= updateInterval)
                {
                    timer = 0;
                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.SyncKillTimer, Hazel.SendOption.Reliable, -1);
                    writer.Write(CachedPlayer.LocalPlayer.PlayerControl.PlayerId);
                    writer.Write(CachedPlayer.LocalPlayer.PlayerControl.killTimer);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    RPCProcedure.syncKillTimer(CachedPlayer.LocalPlayer.PlayerControl.PlayerId, CachedPlayer.LocalPlayer.PlayerControl.killTimer);
                }
            }
            UpdateStatusText();
        }
        public static void MakeButtons(HudManager hm)
        {
            senriganButton = new CustomButton(
                () =>
                {/*ボタンが押されたとき*/
                    senrigan();
                },
                () => {/*ボタンが有効になる条件*/ return enableSenrigan && CachedPlayer.LocalPlayer.PlayerControl.isDead() && !CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Puppeteer); },
                () => {/*ボタンが使える条件*/ return CachedPlayer.LocalPlayer.PlayerControl.isDead(); },
                () => {/*ミーティング終了時*/ },
                getSenriganIcon(),
                new Vector3(-1.8f, -0.06f, 0),
                hm,
                hm.AbilityButton,
                KeyCode.F
            )
            {
                MaxTimer = 0f,
                Timer = 0f,
                buttonText = ModTranslation.getString("")
            };
        }
        public static Sprite getSenriganIcon()
        {
            if (senriganIcon) return senriganIcon;
            senriganIcon = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.Senrigan.png", 115f);
            return senriganIcon;
        }
        public static void SetButtonCooldowns()
        {
            senriganButton.Timer = senriganButton.MaxTimer = 0f;

        }



        public static void Clear()
        {
            toggle = false;
            if (statusText)
            {
                UnityEngine.GameObject.Destroy(statusText);
            }
            statusText = null;
            killTimers = new();
        }
        public static void UpdateStatusText()
        {
            if (CustomOptionHolder.deadImpostorCanSeeKillColdown.getBool())
            {
                if (MeetingHud.Instance != null)
                {
                    if (statusText != null)
                    {
                        statusText.gameObject.SetActive(false);
                    }
                    return;
                }

                if (CachedPlayer.LocalPlayer.PlayerControl.isDead() && CachedPlayer.LocalPlayer.PlayerControl.isImpostor())
                {
                    if (statusText == null)
                    {
                        GameObject gameObject = UnityEngine.Object.Instantiate(FastDestroyableSingleton<HudManager>.Instance?.roomTracker.gameObject);
                        gameObject.transform.SetParent(FastDestroyableSingleton<HudManager>.Instance.transform);
                        gameObject.SetActive(true);
                        UnityEngine.Object.DestroyImmediate(gameObject.GetComponent<RoomTracker>());
                        statusText = gameObject.GetComponent<TMPro.TMP_Text>();
                        gameObject.transform.localPosition = new Vector3(-2.7f, -0.1f, gameObject.transform.localPosition.z);

                        statusText.transform.localScale = new Vector3(1f, 1f, 1f);
                        statusText.fontSize = 1.5f;
                        statusText.fontSizeMin = 1.5f;
                        statusText.fontSizeMax = 1.5f;
                        statusText.alignment = TMPro.TextAlignmentOptions.BottomLeft;
                    }

                    statusText.gameObject.SetActive(true);
                    string text = "KillTimer\n";
                    killTimers.Keys.ToList().ForEach(key =>
                    {
                        if (key == CachedPlayer.LocalPlayer.PlayerControl.PlayerId) return;
                        PlayerControl p = Helpers.playerById(key);
                        if (p.isDead()) return;
                        if (killTimers[key] > 0)
                        {
                            killTimers[key] -= Time.fixedDeltaTime;
                        }
                        else
                        {
                            killTimers[key] = 0;
                        }
                        text += $"{p.name}: {killTimers[key]:F2}s";
                        text += "\n";

                    });
                    statusText.text = text;
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
        class StartMeetingPatch
        {
            public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] GameData.PlayerInfo meetingTarget)
            {
                if (CachedPlayer.LocalPlayer.PlayerControl.Data.IsDead)
                {
                    if (toggle)
                    {
                        senrigan();
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Minigame), nameof(Minigame.Begin))]
        class MinigameBeginPatch
        {
            static void Prefix(Minigame __instance)
            {
                if (CachedPlayer.LocalPlayer.PlayerControl.isDead())
                {
                    if (toggle)
                    {
                        senrigan();
                    }
                }
            }
        }
    }
}
