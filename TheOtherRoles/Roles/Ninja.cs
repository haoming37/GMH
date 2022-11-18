using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using TheOtherRoles.Objects;
using TheOtherRoles.Patches;
using UnityEngine;

namespace TheOtherRoles
{
    [HarmonyPatch]
    public class Ninja : RoleBase<Ninja>
    {

        private static CustomButton ninjaButton;

        public static Color color = Palette.ImpostorRed;

        public static float stealthCooldown { get { return CustomOptionHolder.ninjaStealthCooldown.getFloat(); } }
        public static float stealthDuration { get { return CustomOptionHolder.ninjaStealthDuration.getFloat(); } }
        public static float killPenalty { get { return CustomOptionHolder.ninjaKillPenalty.getFloat(); } }
        public static float speedBonus { get { return CustomOptionHolder.ninjaSpeedBonus.getFloat() / 100f; } }
        public static float fadeTime { get { return CustomOptionHolder.ninjaFadeTime.getFloat(); } }
        public static bool canUseVents { get { return CustomOptionHolder.ninjaCanVent.getBool(); } }
        public static bool canBeTargeted { get { return CustomOptionHolder.ninjaCanBeTargeted.getBool(); } }

        public bool penalized = false;
        public bool stealthed = false;
        public DateTime stealthedAt = DateTime.UtcNow;

        public Ninja()
        {
            RoleType = roleId = RoleType.Ninja;
            penalized = false;
            stealthed = false;
            stealthedAt = DateTime.UtcNow;
        }

        public override void OnMeetingStart()
        {
            stealthed = false;
            ninjaButton.isEffectActive = false;
            ninjaButton.Timer = ninjaButton.MaxTimer = Ninja.stealthCooldown;
        }

        public override void OnMeetingEnd()
        {
            if (player == CachedPlayer.LocalPlayer.PlayerControl)
            {
                if (penalized)
                {
                    player.SetKillTimerUnchecked(PlayerControl.GameOptions.KillCooldown + killPenalty);
                }
                else
                {
                    player.SetKillTimer(PlayerControl.GameOptions.KillCooldown);
                }
            }
        }

        public override void ResetRole()
        {
            penalized = false;
            stealthed = false;
            setOpacity(player, 1.0f);
            ninjaButton.isEffectActive = false;
            ninjaButton.Timer = ninjaButton.MaxTimer = Ninja.stealthCooldown;
        }

        public override void FixedUpdate() { }
        public override void HandleDisconnect(PlayerControl player, DisconnectReasons reason) { }

        public static bool isStealthed(PlayerControl player)
        {
            if (isRole(player) && player.isAlive())
            {
                Ninja n = players.First(x => x.player == player);
                return n.stealthed;
            }
            return false;
        }

        public static float stealthFade(PlayerControl player)
        {
            if (isRole(player) && fadeTime > 0f && player.isAlive())
            {
                Ninja n = players.First(x => x.player == player);
                return Mathf.Min(1.0f, (float)(DateTime.UtcNow - n.stealthedAt).TotalSeconds / fadeTime);
            }
            return 1.0f;
        }

        public static bool isPenalized(PlayerControl player)
        {
            if (isRole(player) && player.isAlive())
            {
                Ninja n = players.First(x => x.player == player);
                return n.penalized;
            }
            return false;
        }

        public static void setStealthed(PlayerControl player, bool stealthed = true)
        {
            if (isRole(player))
            {
                Ninja n = players.First(x => x.player == player);
                n.stealthed = stealthed;
                n.stealthedAt = DateTime.UtcNow;
            }
        }

        public override void OnKill(PlayerControl target)
        {
            penalized = stealthed;
            float penalty = penalized ? killPenalty : 0f;
            if (CachedPlayer.LocalPlayer.PlayerControl == player)
                player.SetKillTimerUnchecked(PlayerControl.GameOptions.KillCooldown + penalty);
        }

        public override void OnDeath(PlayerControl killer)
        {
            stealthed = false;
            ninjaButton.isEffectActive = false;
        }
        public override void OnFinishShipStatusBegin() { }

        private static Sprite buttonSprite;
        public static Sprite getButtonSprite()
        {
            if (buttonSprite) return buttonSprite;
            buttonSprite = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.NinjaButton.png", 115f);
            return buttonSprite;
        }

        public static void MakeButtons(HudManager hm)
        {
            // Ninja stealth
            ninjaButton = new CustomButton(
                () =>
                {
                    if (ninjaButton.isEffectActive)
                    {
                        ninjaButton.Timer = 0;
                        return;
                    }

                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.NinjaStealth, Hazel.SendOption.Reliable, -1);
                    writer.Write(CachedPlayer.LocalPlayer.PlayerControl.PlayerId);
                    writer.Write(true);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    RPCProcedure.ninjaStealth(CachedPlayer.LocalPlayer.PlayerControl.PlayerId, true);
                },
                () => { return CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Ninja) && !CachedPlayer.LocalPlayer.PlayerControl.Data.IsDead; },
                () =>
                {
                    if (ninjaButton.isEffectActive)
                    {
                        ninjaButton.buttonText = ModTranslation.getString("NinjaUnstealthText");
                    }
                    else
                    {
                        ninjaButton.buttonText = ModTranslation.getString("NinjaText");
                    }
                    return CachedPlayer.LocalPlayer.PlayerControl.CanMove;
                },
                () =>
                {
                    ninjaButton.Timer = ninjaButton.MaxTimer = Ninja.stealthCooldown;
                },
                Ninja.getButtonSprite(),
                new Vector3(-1.8f, -0.06f, 0),
                hm,
                hm.KillButton,
                KeyCode.F,
                true,
                Ninja.stealthDuration,
                () =>
                {
                    ninjaButton.Timer = ninjaButton.MaxTimer = Ninja.stealthCooldown;

                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.NinjaStealth, Hazel.SendOption.Reliable, -1);
                    writer.Write(CachedPlayer.LocalPlayer.PlayerControl.PlayerId);
                    writer.Write(false);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    RPCProcedure.ninjaStealth(CachedPlayer.LocalPlayer.PlayerControl.PlayerId, false);

                    CachedPlayer.LocalPlayer.PlayerControl.SetKillTimerUnchecked(Math.Max(CachedPlayer.LocalPlayer.PlayerControl.killTimer, Ninja.killPenalty));
                }
            )
            {
                buttonText = ModTranslation.getString("NinjaText"),
                effectCancellable = true
            };
        }

        public static void SetButtonCooldowns()
        {
            ninjaButton.MaxTimer = Ninja.stealthCooldown;
        }

        public static void Clear()
        {
            players = new List<Ninja>();
        }

        public static void setOpacity(PlayerControl player, float opacity)
        {
            // Sometimes it just doesn't work?
            var color = Color.Lerp(Palette.ClearWhite, Palette.White, opacity);
            try
            {
                if (player.MyPhysics?.myPlayer.cosmetics.currentBodySprite.BodySprite != null)
                {
                    if (player.MyPhysics.myPlayer.cosmetics.currentBodySprite.BodySprite.color != color)
                        Logger.info($"ChangeOpacity {player.MyPhysics.myPlayer.cosmetics.currentBodySprite.BodySprite.color.a} to {opacity} of {player.getNameWithRole()}", "setOpacity");
                    player.MyPhysics.myPlayer.cosmetics.currentBodySprite.BodySprite.color = color;
                }

                if (player.MyPhysics?.myPlayer.cosmetics.skin?.layer != null)
                    player.MyPhysics.myPlayer.cosmetics.skin.layer.color = color;

                if (player.cosmetics.hat != null)
                    player.cosmetics.hat.SpriteColor = color;

                if (player.cosmetics.currentPet?.rend != null)
                    player.cosmetics.currentPet.rend.color = color;

                if (player.cosmetics.currentPet?.shadowRend != null)
                    player.cosmetics.currentPet.shadowRend.color = color;

                if (player.cosmetics.visor != null)
                    player.cosmetics.visor.Image.color = color;

                if (player.cosmetics.colorBlindText != null)
                    player.cosmetics.colorBlindText.color = color;
            }
            catch { }
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
        public static class PlayerPhysicsNinjaPatch
        {
            public static void Postfix(PlayerPhysics __instance)
            {
                if (__instance.AmOwner && __instance.myPlayer.CanMove && GameData.Instance && isStealthed(__instance.myPlayer))
                {
                    __instance.body.velocity *= speedBonus;
                }

                if (isRole(__instance.myPlayer))
                {
                    var ninja = __instance.myPlayer;
                    if (ninja == null || ninja.isDead()) return;

                    bool canSee =
                        CachedPlayer.LocalPlayer.PlayerControl.isImpostor() ||
                        CachedPlayer.LocalPlayer.PlayerControl.isDead() ||
                        (Lighter.canSeeNinja && CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Lighter) && Lighter.isLightActive(CachedPlayer.LocalPlayer.PlayerControl));

                    var opacity = canSee ? 0.1f : 0.0f;

                    if (isStealthed(ninja))
                    {
                        opacity = Math.Max(opacity, 1.0f - stealthFade(ninja));
                        ninja.cosmetics.currentBodySprite.BodySprite.material.SetFloat("_Outline", 0f);
                    }
                    else
                    {
                        opacity = Math.Max(opacity, stealthFade(ninja));
                    }

                    setOpacity(ninja, opacity);
                }
            }
        }
    }
}
