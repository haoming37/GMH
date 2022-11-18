using System;
using System.Linq;
using HarmonyLib;
using static TheOtherRoles.TheOtherRoles;
using TheOtherRoles.Objects;

namespace TheOtherRoles.Patches
{
    [HarmonyPatch(typeof(Console), nameof(Console.Use))]
    class ConsoleUsePatch
    {
        public static void Prefix(Console __instance)
        {
            if (CustomOptionHolder.airshipReplaceSafeTask.getBool())
            {
                var playerTask = __instance.FindTask(PlayerControl.LocalPlayer);
                var alignTelescopeMinigame = MapData.PolusShip.NormalTasks.FirstOrDefault(x => x.name == "AlignTelescope").MinigamePrefab;
                if (playerTask.MinigamePrefab.name == "SafeGame")
                {
                    playerTask.MinigamePrefab = alignTelescopeMinigame;
                }
            }
        }
    }
}
