using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using TheOtherRoles.Objects;
using UnityEngine;
using static TheOtherRoles.TheOtherRoles;

namespace TheOtherRoles.Patches
{

    [HarmonyPatch(typeof(ShipStatus))]
    public class ShipStatusPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
        public static bool Prefix(ref float __result, ShipStatus __instance, [HarmonyArgument(0)] GameData.PlayerInfo player)
        {
            if (!__instance.Systems.ContainsKey(SystemTypes.Electrical)) return true;

            // If player is a role which has Impostor vision
            if (Helpers.hasImpostorVision(player.Object))
            {
                // __result = __instance.MaxLightRadius * PlayerControl.GameOptions.ImpostorLightMod;
                __result = GetNeutralLightRadius(__instance, true);
                return false;
            }

            // If player is Lighter with ability active
            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Lighter) && Lighter.isLightActive(CachedPlayer.LocalPlayer.PlayerControl))
            {
                float unlerped = Mathf.InverseLerp(__instance.MinLightRadius, __instance.MaxLightRadius, GetNeutralLightRadius(__instance, true));
                __result = Mathf.Lerp(__instance.MaxLightRadius * Lighter.lighterModeLightsOffVision, __instance.MaxLightRadius * Lighter.lighterModeLightsOnVision, unlerped);
                return false;
            }

            // If there is a Trickster with their ability active
            if (Trickster.trickster != null && Trickster.lightsOutTimer > 0f)
            {
                float lerpValue = 1f;
                if (Trickster.lightsOutDuration - Trickster.lightsOutTimer < 0.5f)
                {
                    lerpValue = Mathf.Clamp01((Trickster.lightsOutDuration - Trickster.lightsOutTimer) * 2);
                }
                else if (Trickster.lightsOutTimer < 0.5)
                {
                    lerpValue = Mathf.Clamp01(Trickster.lightsOutTimer * 2);
                }

                __result = Mathf.Lerp(__instance.MinLightRadius, __instance.MaxLightRadius, 1 - lerpValue) * PlayerControl.GameOptions.CrewLightMod;
                return false;
            }

            // If player is Lawyer, apply Lawyer vision modifier
            if (Lawyer.lawyer != null && Lawyer.lawyer.PlayerId == player.PlayerId)
            {
                float unlerped = Mathf.InverseLerp(__instance.MinLightRadius, __instance.MaxLightRadius, GetNeutralLightRadius(__instance, false));
                __result = Mathf.Lerp(__instance.MinLightRadius, __instance.MaxLightRadius * Lawyer.vision, unlerped);
                return false;
            }

            // Default light radius
            __result = GetNeutralLightRadius(__instance, false);
            return false;
        }

        public static float GetNeutralLightRadius(ShipStatus shipStatus, bool isImpostor)
        {
            if (SubmergedCompatibility.Loaded && shipStatus.Type == SubmergedCompatibility.SUBMERGED_MAP_TYPE)
            {
                return SubmergedCompatibility.GetSubmergedNeutralLightRadius(isImpostor);
            }

            if (isImpostor) return shipStatus.MaxLightRadius * PlayerControl.GameOptions.ImpostorLightMod;

            SwitchSystem switchSystem = shipStatus.Systems[SystemTypes.Electrical].TryCast<SwitchSystem>();
            float lerpValue = switchSystem.Value / 255f;

            return Mathf.Lerp(shipStatus.MinLightRadius, shipStatus.MaxLightRadius, lerpValue) * PlayerControl.GameOptions.CrewLightMod;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.IsGameOverDueToDeath))]
        public static void Postfix2(ShipStatus __instance, ref bool __result)
        {
            __result = false;
        }

        private static int originalNumCommonTasksOption = 0;
        private static int originalNumShortTasksOption = 0;
        private static int originalNumLongTasksOption = 0;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
        public static bool Prefix(ShipStatus __instance)
        {
            if (CustomOptionHolder.uselessOptions.getBool() && CustomOptionHolder.playerColorRandom.getBool() && AmongUsClient.Instance.AmHost)
            {
                List<int> colors = Enumerable.Range(0, Palette.PlayerColors.Count).ToList();
                foreach (PlayerControl p in CachedPlayer.AllPlayers)
                {
                    int i = TheOtherRoles.rnd.Next(0, colors.Count);
                    p.SetColor(colors[i]);
                    p.RpcSetColor((byte)colors[i]);
                    colors.RemoveAt(i);
                }
            }

            var commonTaskCount = __instance.CommonTasks.Count;
            var normalTaskCount = __instance.NormalTasks.Count;
            var longTaskCount = __instance.LongTasks.Count;
            originalNumCommonTasksOption = PlayerControl.GameOptions.NumCommonTasks;
            originalNumShortTasksOption = PlayerControl.GameOptions.NumShortTasks;
            originalNumLongTasksOption = PlayerControl.GameOptions.NumLongTasks;
            if (PlayerControl.GameOptions.NumCommonTasks > commonTaskCount) PlayerControl.GameOptions.NumCommonTasks = commonTaskCount;
            if (PlayerControl.GameOptions.NumShortTasks > normalTaskCount) PlayerControl.GameOptions.NumShortTasks = normalTaskCount;
            if (PlayerControl.GameOptions.NumLongTasks > longTaskCount) PlayerControl.GameOptions.NumLongTasks = longTaskCount;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
        public static void Postfix3(ShipStatus __instance)
        {
            // Restore original settings after the tasks have been selected
            PlayerControl.GameOptions.NumCommonTasks = originalNumCommonTasksOption;
            PlayerControl.GameOptions.NumShortTasks = originalNumShortTasksOption;
            PlayerControl.GameOptions.NumLongTasks = originalNumLongTasksOption;

            // 一部役職のタスクを再割り当てする
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.FinishShipStatusBegin, Hazel.SendOption.Reliable, -1);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            RPCProcedure.finishShipStatusBegin();
        }
        [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
        class ShipStatusStartPatch
        {
            public static void Postfix()
            {
                Logger.info("Game Started", "Phase");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.SpawnPlayer))]
        public static void Postfix(ShipStatus __instance, PlayerControl player, int numPlayers, bool initialSpawn)
        {
            // Polusの湧き位置をランダムにする 無駄に人数分シャッフルが走るのをそのうち直す
            if (PlayerControl.GameOptions.MapId == 2 && CustomOptionHolder.polusRandomSpawn.getBool())
            {
                if (AmongUsClient.Instance.AmHost)
                {
                    System.Random rand = new();
                    int randVal = rand.Next(0, 6);
                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.RandomSpawn, Hazel.SendOption.Reliable, -1);
                    writer.Write((byte)player.Data.PlayerId);
                    writer.Write((byte)randVal);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    RPCProcedure.randomSpawn((byte)player.Data.PlayerId, (byte)randVal);
                }
            }

            CustomButton.stopCountdown = false;
        }


    }
}
