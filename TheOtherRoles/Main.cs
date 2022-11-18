using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using HarmonyLib;
using Hazel;
using TheOtherRoles.Modules;
using TheOtherRoles.Objects;
using TheOtherRoles.Patches;
using UnityEngine;
using UnhollowerRuntimeLib;


namespace TheOtherRoles
{
    [BepInPlugin(Id, "The Other Roles GM", VersionString)]
    [BepInDependency(SubmergedCompatibility.SUBMERGED_GUID, BepInDependency.DependencyFlags.SoftDependency)]
    //[BepInProcess("Among Us.exe")]
    public class TheOtherRolesPlugin : BasePlugin
    {
        public const string Id = "me.eisbison.theotherroles";

        public const string VersionString = "999.999.999";

        public static System.Version Version = System.Version.Parse(VersionString);
        internal static BepInEx.Logging.ManualLogSource Logger;

        public Harmony Harmony { get; } = new Harmony(Id);
        public static TheOtherRolesPlugin Instance;

        public static int optionsPage = 0;

        public static ConfigEntry<bool> DebugMode { get; private set; }
        public static ConfigEntry<bool> StreamerMode { get; set; }
        public static ConfigEntry<bool> GhostsSeeTasks { get; set; }
        public static ConfigEntry<bool> GhostsSeeRoles { get; set; }
        public static ConfigEntry<bool> GhostsSeeVotes { get; set; }
        public static ConfigEntry<bool> ShowRoleSummary { get; set; }
        public static ConfigEntry<bool> HideNameplates { get; set; }
        public static ConfigEntry<bool> ShowLighterDarker { get; set; }
        public static ConfigEntry<bool> HideTaskArrows { get; set; }
        public static ConfigEntry<bool> OfflineHats { get; set; }
        public static ConfigEntry<bool> HideFakeTasks { get; set; }
        public static ConfigEntry<bool> BetterSabotageMap { get; set; }
        public static ConfigEntry<bool> ForceNormalSabotageMap { get; set; }
        public static ConfigEntry<string> StreamerModeReplacementText { get; set; }
        public static ConfigEntry<string> StreamerModeReplacementColor { get; set; }
        public static ConfigEntry<string> Ip { get; set; }
        public static ConfigEntry<ushort> Port { get; set; }
        public static ConfigEntry<string> DebugRepo { get; private set; }
        public static ConfigEntry<string> ShowPopUpVersion { get; set; }
        public static ConfigEntry<string> WebhookUrl { get; set; }
        public static ConfigEntry<bool> TransparentMap { get; set; }

        public static Assembly JsonNet;

        public static Sprite ModStamp;

        public static IRegionInfo[] defaultRegions;
        public static void UpdateRegions()
        {
            ServerManager serverManager = FastDestroyableSingleton<ServerManager>.Instance;
            IRegionInfo[] regions = new IRegionInfo[] {
                new DnsRegionInfo("haoming-server.com", "haoming-server", StringNames.NoTranslation, "haoming-server.com", 22023, false).CastFast<IRegionInfo>(),
                new DnsRegionInfo(Ip.Value, "Custom", StringNames.NoTranslation, Ip.Value, Port.Value, false).CastFast<IRegionInfo>()
            };
#nullable enable
            IRegionInfo? currentRegion = serverManager.CurrentRegion;
#nullable disable
            foreach (IRegionInfo region in regions)
            {
                if (region == null)
                    Logger.LogError("Could not add region");
                else
                {
                    if (currentRegion != null && region.Name.Equals(currentRegion.Name, StringComparison.OrdinalIgnoreCase))
                        currentRegion = region;
                    serverManager.AddOrUpdateRegion(region);
                }
            }

            // AU remembers the previous region that was set, so we need to restore it
            if (currentRegion != null)
            {
                Logger.LogDebug("Resetting previous region");
                serverManager.SetRegion(currentRegion);
            }
        }

        public override void Load()
        {
            ModTranslation.Load();
            Logger = Log;
            DebugMode = Config.Bind("Custom", "Enable Debug Mode", false);
            StreamerMode = Config.Bind("Custom", "Enable Streamer Mode", false);
            GhostsSeeTasks = Config.Bind("Custom", "Ghosts See Remaining Tasks", true);
            GhostsSeeRoles = Config.Bind("Custom", "Ghosts See Roles", true);
            GhostsSeeVotes = Config.Bind("Custom", "Ghosts See Votes", true);
            ShowRoleSummary = Config.Bind("Custom", "Show Role Summary", true);
            HideNameplates = Config.Bind("Custom", "Hide Nameplates", false);
            ShowLighterDarker = Config.Bind("Custom", "Show Lighter / Darker", false);
            HideTaskArrows = Config.Bind("Custom", "Hide Task Arrows", false);
            OfflineHats = Config.Bind("Custom", "Offline Hats", false);
            HideFakeTasks = Config.Bind("Custom", "Hide Fake Tasks", false);
            BetterSabotageMap = Config.Bind("Custom", "BetterSabotageMap", false);
            ForceNormalSabotageMap = Config.Bind("Custom", "ForceNormalSabotageMap", false);
            ShowPopUpVersion = Config.Bind("Custom", "Show PopUp", "0");
            StreamerModeReplacementText = Config.Bind("Custom", "Streamer Mode Replacement Text", "\n\nThe Other Roles GM");
            StreamerModeReplacementColor = Config.Bind("Custom", "Streamer Mode Replacement Text Hex Color", "#87AAF5FF");
            DebugRepo = Config.Bind("Custom", "Debug Hat Repo", "");
            WebhookUrl = Config.Bind("Custom", "WebhookUrl", "");
            TransparentMap = Config.Bind("Custom", "TransparentMap", false);

            Ip = Config.Bind("Custom", "Custom Server IP", "127.0.0.1");
            Port = Config.Bind("Custom", "Custom Server Port", (ushort)22023);
            defaultRegions = ServerManager.DefaultRegions;

            UpdateRegions();

            GameOptionsData.RecommendedImpostors = Enumerable.Repeat(3, 16).ToArray();
            GameOptionsData.MaxImpostors = Enumerable.Repeat(15, 16).ToArray(); // Max Imp = Recommended Imp = 3
            GameOptionsData.MinPlayers = Enumerable.Repeat(4, 15).ToArray(); // Min Players = 4

            DebugMode = Config.Bind("Custom", "Enable Debug Mode", false);
            Instance = this;
            CustomOptionHolder.Load();
            RoleInfo.Load();
            CustomColors.Load();
            Harmony.PatchAll();
            Patches.SubmergedPatch.Patch();
            SubmergedCompatibility.Initialize();

            //Newtonsoft.Jsonを読み込み
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TheOtherRoles.Resources.Newtonsoft.Json.dll");
            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            JsonNet = Assembly.Load(buffer);

            // オレオレオブジェクト有効化
            ClassInjector.RegisterTypeInIl2Cpp(typeof(FoxTask));
            ClassInjector.RegisterTypeInIl2Cpp(typeof(HaomingMenu));
        }

        public static Sprite GetModStamp()
        {
            if (ModStamp) return ModStamp;
            return ModStamp = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.ModStamp.png", 150f);
        }
    }

    // Deactivate bans, since I always leave my local testing game and ban myself
    [HarmonyPatch(typeof(StatsManager), nameof(StatsManager.AmBanned), MethodType.Getter)]
    public static class AmBannedPatch
    {
        public static void Postfix(out bool __result)
        {
            __result = false;
        }
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Awake))]
    public static class ChatControllerAwakePatch
    {
        private static void Prefix()
        {
            if (!EOSManager.Instance.isKWSMinor)
            {
                AmongUs.Data.DataManager.Settings.Multiplayer.chatMode = (InnerNet.QuickChatModes)1;
                // SaveManager.isGuest = false;
            }
        }
    }

    // Debugging tools
    [HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
    public static class DebugManager
    {
        private static readonly System.Random random = new((int)DateTime.Now.Ticks);
        private static List<PlayerControl> bots = new();

        public static void Postfix(KeyboardJoystick __instance)
        {
            if (AmongUsClient.Instance.AmHost && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
            {
                //ゲーム強制終了
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.F5))
                {
                    ShipStatus.RpcEndGame((GameOverReason)CustomGameOverReason.ForceEnd, false);
                }
            }

            if (!TheOtherRolesPlugin.DebugMode.Value) return;

            // Spawn dummys
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.F))
            {
                var playerControl = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab);
                var i = playerControl.PlayerId = (byte)GameData.Instance.GetAvailableId();

                bots.Add(playerControl);
                GameData.Instance.AddPlayer(playerControl);
                AmongUsClient.Instance.Spawn(playerControl, -2, InnerNet.SpawnFlags.None);

                // int hat = random.Next(HatManager.Instance.AllHats.Count);
                // int pet = random.Next(HatManager.Instance.AllPets.Count);
                // int skin = random.Next(HatManager.Instance.AllSkins.Count);
                // int visor = random.Next(HatManager.Instance.AllVisors.Count);
                // int color = random.Next(Palette.PlayerColors.Length);
                // int nameplate = random.Next(HatManager.Instance.AllNamePlates.Count);

                // playerControl.transform.position = CachedPlayer.LocalPlayer.PlayerControl.transform.position;
                // playerControl.GetComponent<DummyBehaviour>().enabled = true;
                // playerControl.NetTransform.enabled = false;
                // playerControl.SetName(RandomString(10));
                // playerControl.SetColor(color);
                // playerControl.SetHat(HatManager.Instance.AllHats[hat].ProductId, color);
                // playerControl.SetPet(HatManager.Instance.AllPets[pet].ProductId, color);
                // playerControl.SetVisor(HatManager.Instance.AllVisors[visor].ProductId);
                // playerControl.SetSkin(HatManager.Instance.AllSkins[skin].ProductId);
                // playerControl.SetNamePlate(HatManager.Instance.AllNamePlates[nameplate].ProductId);
                GameData.Instance.RpcSetTasks(playerControl.PlayerId, new byte[0]);
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                var obj = new GameObject("HaomingMenu");
                obj.AddComponent<HaomingMenu>();
                obj.SetActive(true);
            }

            // ゲーム内ログ出力のトグル
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.F2))
            {
                Logger.isAlsoInGame = !Logger.isAlsoInGame;
                Logger.SendInGame("isAlsoInGame: " + Logger.isAlsoInGame);
            }
        }

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
    [HarmonyPatch(typeof(SplashManager), nameof(SplashManager.Update))]
    class SplashLogoAnimatorPatch
    {
        public static void Prefix(SplashManager __instance)
        {
            if (TheOtherRolesPlugin.DebugMode.Value)
            {
                __instance.sceneChanger.AllowFinishLoadingScene();
                __instance.startedSceneLoad = true;
            }
        }
    }
    // [HarmonyPatch(typeof(SignInGuestOfflineChoice), nameof(SignInGuestOfflineChoice.Open))]
    // public class SignInGuestOfflineChoiceOpenPatch
    // {
    //     private static void Postfix(SignInGuestOfflineChoice __instance)
    //     {
    //         if (TheOtherRolesPlugin.DebugMode.Value) __instance?.continueOfflineButton?.OnClick?.Invoke();
    //     }
    // }
}
