using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using UnityEngine;
using TheOtherRoles.Objects;
using TheOtherRoles.Modules;
using static TheOtherRoles.TheOtherRoles;
using static TheOtherRoles.TheOtherRolesGM;
using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Core;
using System.Reflection;
using UnhollowerRuntimeLib;



namespace TheOtherRoles.Patches
{
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
    class IntroCutsceneOnDestroyPatch
    {
        public static void Prefix(IntroCutscene __instance)
        {
            // Generate and initialize player icons
            if (CachedPlayer.LocalPlayer.PlayerControl != null && FastDestroyableSingleton<HudManager>.Instance != null)
            {
                Vector3 bottomLeft = new(-FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.x, FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.y, FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.z);
                foreach (PlayerControl p in CachedPlayer.AllPlayers)
                {
                    GameData.PlayerInfo data = p.Data;
                    PoolablePlayer player = UnityEngine.Object.Instantiate<PoolablePlayer>(__instance.PlayerPrefab, FastDestroyableSingleton<HudManager>.Instance.transform);
                    player.UpdateFromPlayerOutfit((GameData.PlayerOutfit)p.Data.DefaultOutfit, PlayerMaterial.MaskType.ComplexUI, p.Data.IsDead, true);
                    player.SetFlipX(true);
                    // player.PetSlot.gameObject.SetActive(false);
                    player.cosmetics.currentPet?.gameObject.SetActive(false);
                    player.cosmetics.nameText.text = p.Data.DefaultOutfit.PlayerName;
                    MapOptions.playerIcons[p.PlayerId] = player;

                    if (CachedPlayer.LocalPlayer.PlayerControl == BountyHunter.bountyHunter)
                    {
                        player.transform.localPosition = bottomLeft + new Vector3(-0.25f, 0f, 0);
                        player.transform.localScale = Vector3.one * 0.4f;
                        player.gameObject.SetActive(false);
                    }
                    else if (CachedPlayer.LocalPlayer.PlayerControl == GM.gm)
                    {
                        player.transform.localPosition = Vector3.zero;
                        player.transform.localScale = Vector3.one * 0.3f;
                        player.setSemiTransparent(false);
                        player.gameObject.SetActive(false);
                    }
                    else
                    {
                        player.gameObject.SetActive(false);
                    }
                }
            }

            // Force Bounty Hunter to load a new Bounty when the Intro is over
            if (BountyHunter.bounty != null && CachedPlayer.LocalPlayer.PlayerControl == BountyHunter.bountyHunter)
            {
                BountyHunter.bountyUpdateTimer = 0f;
                if (FastDestroyableSingleton<HudManager>.Instance != null)
                {
                    Vector3 bottomLeft = new Vector3(-FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.x, FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.y, FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.z) + new Vector3(-0.25f, 1f, 0);
                    BountyHunter.cooldownText = UnityEngine.Object.Instantiate<TMPro.TextMeshPro>(FastDestroyableSingleton<HudManager>.Instance.KillButton.cooldownTimerText, FastDestroyableSingleton<HudManager>.Instance.transform);
                    BountyHunter.cooldownText.alignment = TMPro.TextAlignmentOptions.Center;
                    BountyHunter.cooldownText.transform.localPosition = bottomLeft + new Vector3(0f, -1f, -1f);
                    BountyHunter.cooldownText.gameObject.SetActive(true);
                }
            }

            Arsonist.updateIcons();
            Morphling.resetMorph();
            Camouflager.resetCamouflage();

            if (CachedPlayer.LocalPlayer.PlayerControl == GM.gm && !GM.hasTasks)
            {
                CachedPlayer.LocalPlayer.PlayerControl.clearAllTasks();
            }

            if (CachedPlayer.LocalPlayer.PlayerControl.isGM())
            {
                FastDestroyableSingleton<HudManager>.Instance.ShadowQuad.gameObject.SetActive(false);
                FastDestroyableSingleton<HudManager>.Instance.ReportButton.gameObject.SetActiveRecursively(false);
                FastDestroyableSingleton<HudManager>.Instance.ReportButton.SetActive(false);
                FastDestroyableSingleton<HudManager>.Instance.ReportButton.graphic.enabled = false;
                FastDestroyableSingleton<HudManager>.Instance.ReportButton.enabled = false;
                FastDestroyableSingleton<HudManager>.Instance.ReportButton.graphic.sprite = null;
                FastDestroyableSingleton<HudManager>.Instance.ReportButton.buttonLabelText.enabled = false;
                FastDestroyableSingleton<HudManager>.Instance.ReportButton.buttonLabelText.SetText("");

                FastDestroyableSingleton<HudManager>.Instance.roomTracker.gameObject.SetActiveRecursively(false);
                FastDestroyableSingleton<HudManager>.Instance.roomTracker.text.enabled = false;
                FastDestroyableSingleton<HudManager>.Instance.roomTracker.text.SetText("");
                FastDestroyableSingleton<HudManager>.Instance.roomTracker.enabled = false;
            }
            // インポスター視界の場合に昇降機右の影を無効化
            if (PlayerControl.GameOptions.MapId == 4 && CustomOptionHolder.airshipOptimizeMap.getBool() && Helpers.hasImpostorVision(CachedPlayer.LocalPlayer.PlayerControl))
            {
                var obj = ShipStatus.Instance.FastRooms[SystemTypes.GapRoom].gameObject;
                OneWayShadows oneWayShadow = obj.transform.FindChild("Shadow").FindChild("LedgeShadow").GetComponent<OneWayShadows>();
                oneWayShadow.gameObject.SetActive(false);
            }

            // ベントを追加する
            AdditionalVents.AddAdditionalVents();

            // スペシメンにバイタルを移動する
            SpecimenVital.moveVital();

            // アーカイブのアドミンを消す
            if (PlayerControl.GameOptions.MapId == 4 && CustomOptionHolder.airshipOldAdmin.getBool())
            {
                GameObject records = ShipStatus.Instance.FastRooms[SystemTypes.Records].gameObject;
                records.GetComponentsInChildren<MapConsole>().Where(x => x.name == "records_admin_map").FirstOrDefault()?.gameObject.SetActive(false);
            }

            if (ShipStatus.Instance.FastRooms.ContainsKey(SystemTypes.GapRoom))
            {
                GameObject gapRoom = ShipStatus.Instance.FastRooms[SystemTypes.GapRoom].gameObject;
                // GapRoomの配電盤を消す
                if (PlayerControl.GameOptions.MapId == 4 && CustomOptionHolder.airshipDisableGapSwitchBoard.getBool())
                {
                    GameObject sabo = gapRoom.GetComponentsInChildren<Console>().Where(x => x.name == "task_lightssabotage (gap)").FirstOrDefault()?.gameObject;
                    sabo.SetActive(false);
                    MapUtilities.CachedShipStatus.AllConsoles = MapUtilities.CachedShipStatus.AllConsoles.Where(x => x != sabo.GetComponent<Console>()).ToArray();
                }

                // ぬ～んを消す
                if (PlayerControl.GameOptions.MapId == 4 && CustomOptionHolder.airshipDisableMovingPlatform.getBool())
                {
                    gapRoom.GetComponentInChildren<MovingPlatformBehaviour>().gameObject.SetActive(false);
                    gapRoom.GetComponentsInChildren<PlatformConsole>().ForEach(x => x.gameObject.SetActive(false));
                }
            }

            //タスクバグ修正
            if (PlayerControl.GameOptions.MapId == 4 && CustomOptionHolder.airshipEnableWallCheck.getBool())
            {
                var objects = UnityEngine.GameObject.FindObjectsOfType<Console>().ToList();
                objects.Find(x => x.name == "task_garbage1").checkWalls = true;
                objects.Find(x => x.name == "task_garbage2").checkWalls = true;
                objects.Find(x => x.name == "task_garbage3").checkWalls = true;
                objects.Find(x => x.name == "task_garbage4").checkWalls = true;
                objects.Find(x => x.name == "task_garbage5").checkWalls = true;
                objects.Find(x => x.name == "task_shower").checkWalls = true;
                objects.Find(x => x.name == "task_developphotos").checkWalls = true;
                objects.Find(x => x.name == "DivertRecieve" && x.Room == SystemTypes.Armory).checkWalls = true;
                objects.Find(x => x.name == "DivertRecieve" && x.Room == SystemTypes.MainHall).checkWalls = true;
            }

            // 最初から一人の場合はLast Impostorになる
            if (AmongUsClient.Instance.AmHost)
            {
                LastImpostor.promoteToLastImpostor();
            }

            // タスクパネルの表示優先度を上げる
            var taskPanel = FastDestroyableSingleton<HudManager>.Instance.TaskStuff;
            var pos = taskPanel.transform.position;
            taskPanel.transform.position = new Vector3(pos.x, pos.y, -20);

            // ダミー人形をスポーンさせておく
            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Puppeteer) && SubmergedCompatibility.isSubmerged())
            {
                var playerId = (byte)GameData.Instance.GetAvailableId();
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.SpawnDummy, Hazel.SendOption.Reliable, -1);
                writer.Write(playerId);
                writer.Write(CachedPlayer.LocalPlayer.PlayerControl.transform.position.x);
                writer.Write(CachedPlayer.LocalPlayer.PlayerControl.transform.position.y);
                writer.Write(CachedPlayer.LocalPlayer.PlayerControl.transform.position.z);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                RPCProcedure.spawnDummy(playerId, CachedPlayer.LocalPlayer.PlayerControl.transform.position);
            }

            // Cornucopiaのバナーを表示する
            Cornucopia.showBanner();

            // マップデータのコピーを読み込み
            if (CustomOptionHolder.airshipReplaceSafeTask.getBool())
            {
                MapData.LoadAssets(AmongUsClient.Instance);
            }

            // お参りタスク
            if (CustomOptionHolder.foxSpawnRate.getSelection() > 0)
            {
                Shrine.activateShrines(PlayerControl.GameOptions.MapId);
                List<Byte> taskIdList = new();
                Shrine.allShrine.ForEach(shrine => taskIdList.Add((byte)shrine.console.ConsoleId));
                taskIdList.Shuffle();
                var cpt = new CustomNormalPlayerTask("foxTaskStay", Il2CppType.Of<FoxTask>(), Fox.numTasks, taskIdList.ToArray(), Shrine.allShrine.Find(x => x.console.ConsoleId == taskIdList.ToArray()[0]).console.Room, true);
                foreach (PlayerControl p in CachedPlayer.AllPlayers)
                {
                    if (p.isRole(RoleType.Fox))
                    {
                        p.clearAllTasks();
                        cpt.addTaskToPlayer(p.PlayerId);
                    }
                }
            }

            // シュレディンガーの猫変身メニュー用テンプレート
            SchrodingersCat.playerTemplate = UnityEngine.Object.Instantiate<PoolablePlayer>(__instance.PlayerPrefab, FastDestroyableSingleton<HudManager>.Instance.transform);
            SchrodingersCat.playerTemplate.UpdateFromPlayerOutfit((GameData.PlayerOutfit)CachedPlayer.LocalPlayer.PlayerControl.Data.DefaultOutfit, PlayerMaterial.MaskType.ComplexUI, false, true);
            SchrodingersCat.playerTemplate.SetFlipX(true);
            SchrodingersCat.playerTemplate.gameObject.SetActive(false);
            SchrodingersCat.playerTemplate.cosmetics.currentPet?.gameObject.SetActive(false);
            SchrodingersCat.playerTemplate.cosmetics.nameText.text = "";
            SchrodingersCat.playerTemplate.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch]
    class IntroPatch
    {
        public static void setupIntroTeamIcons(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
        {
            // Intro solo teams
            if (CachedPlayer.LocalPlayer.PlayerControl.isNeutral() || CachedPlayer.LocalPlayer.PlayerControl == GM.gm)
            {
                if(!(CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.SchrodingersCat) && SchrodingersCat.hideRole))
                {
                    var soloTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
                    soloTeam.Add(CachedPlayer.LocalPlayer.PlayerControl);
                    yourTeam = soloTeam;
                }
            }

            // Don't show the GM
            if (!CachedPlayer.LocalPlayer.PlayerControl.isGM())
            {
                var newTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
                foreach (PlayerControl p in yourTeam)
                {
                    if (p != GM.gm)
                        newTeam.Add(p);
                }
                yourTeam = newTeam;
            }

            // Add the Spy to the Impostor team (for the Impostors)
            if (Spy.spy != null && CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor)
            {
                List<PlayerControl> players = PlayerControl.AllPlayerControls.GetFastEnumerator().ToArray().ToList().OrderBy(x => Guid.NewGuid()).ToList();
                var fakeImpostorTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>(); // The local player always has to be the first one in the list (to be displayed in the center)
                fakeImpostorTeam.Add(CachedPlayer.LocalPlayer.PlayerControl);
                foreach (PlayerControl p in players)
                {
                    if (CachedPlayer.LocalPlayer.PlayerControl != p && (p == Spy.spy || p.Data.Role.IsImpostor))
                        fakeImpostorTeam.Add(p);
                }
                yourTeam = fakeImpostorTeam;
            }
        }

        public static void setupIntroTeam(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
        {
            List<RoleInfo> infos = RoleInfo.getRoleInfoForPlayer(CachedPlayer.LocalPlayer.PlayerControl);
            RoleInfo roleInfo = infos.Where(info => info.roleType != RoleType.Lovers).FirstOrDefault();
            if (roleInfo == null) return;
            if (CachedPlayer.LocalPlayer.PlayerControl.isNeutral() || CachedPlayer.LocalPlayer.PlayerControl.isGM())
            {
                if (!(CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.SchrodingersCat) && SchrodingersCat.hideRole))
                {
                    __instance.BackgroundBar.material.color = roleInfo.color;
                    __instance.TeamTitle.text = roleInfo.name;
                    __instance.TeamTitle.color = roleInfo.color;
                    __instance.ImpostorText.text = "";
                }
            }
        }

        [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
        class IntroCutsceneCoBeginPatch
        {
            private static bool Prefix(IntroCutscene __instance, ref Il2CppSystem.Collections.IEnumerator __result)
            {
                if (RoleAssignmentPatch.isAssigned)
                {
                    return true;
                }
                __result = CoBegin(__instance).WrapToIl2Cpp();
                return false;
            }
            private static IEnumerator CoBegin(IntroCutscene __instance)
            {
                yield return waitRoleAssign().WrapToIl2Cpp();
                yield return __instance.CoBegin();
                yield break;
            }
            private static IEnumerator waitRoleAssign()
            {
                if (!CustomOptionHolder.activateRoles.getBool()) yield break;

                while (!RoleAssignmentPatch.isAssigned)
                {
                    yield return null;
                }
                yield break;
            }

        }

        [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.ShowRole))]
        class SetUpRoleTextPatch
        {
            public static bool Prefix(IntroCutscene __instance, ref Il2CppSystem.Collections.IEnumerator __result)
            {
                if (!CustomOptionHolder.activateRoles.getBool()) return true; // Don't override the intro of the vanilla roles
                __result = setupRole(__instance).WrapToIl2Cpp();
                return false;
            }

            private static IEnumerator setupRole(IntroCutscene __instance)
            {
                List<RoleInfo> infos = RoleInfo.getRoleInfoForPlayer(CachedPlayer.LocalPlayer.PlayerControl, new RoleType[] { RoleType.Lovers });
                RoleInfo roleInfo = infos.FirstOrDefault();
                if (roleInfo == RoleInfo.fortuneTeller && FortuneTeller.numTasks > 0)
                {
                    roleInfo = RoleInfo.crewmate;
                }

                Logger.info("----------Role Assign-----------", "Settings");
                foreach (var pc in PlayerControl.AllPlayerControls.GetFastEnumerator())
                    Logger.info(String.Format("{0,-3}{1,-2}:{2}:{3}", pc.AmOwner ? "[*]" : "", pc.PlayerId, pc.Data.PlayerName.PadRightV2(20), RoleInfo.GetRolesString(pc, false, joinSeparator: " + ")), "Settings");
                Logger.info("-----------Platforms------------", "Settings");
                foreach (var pc in PlayerControl.AllPlayerControls.GetFastEnumerator())
                    Logger.info(String.Format("{0,-3}{1,-2}:{2}:{3}", pc.AmOwner ? "[*]" : "", pc.PlayerId, pc.Data.PlayerName.PadRightV2(20), pc.getPlatform().Replace("Standalone", "")), "Settings");
                Logger.info("---------Game Settings----------", "Settings");
                TheOtherRolesPlugin.optionsPage = 0;
                var tmp = PlayerControl.GameOptions.ToHudString(GameData.Instance ? GameData.Instance.PlayerCount : 10).Split("\r\n");
                foreach (var t in tmp[1..(tmp.Length - 2)])
                    Logger.info(t, "Settings");
                Logger.info("--------Advance Settings--------", "Settings");
                foreach (var o in CustomOption.options)
                    if (o.parent == null ? !o.getString().Equals("0%") : o.parent.enabled)
                        Logger.info(String.Format("{0}:{1}", o.parent == null ? o.name.removeHtml().PadRightV2(43) : $"┗ {o.name.removeHtml().PadRightV2(41)}", o.getString().removeHtml()), "Settings");
                Logger.info("--------------------------------", "Settings");

                __instance.YouAreText.color = roleInfo.color;
                __instance.RoleText.text = roleInfo.name;
                __instance.RoleText.color = roleInfo.color;
                __instance.RoleBlurbText.text = roleInfo.introDescription;
                __instance.RoleBlurbText.color = roleInfo.color;

                var isSiuneInGame = CachedPlayer.AllPlayers.ToArray().Count(player => player.Data.PlayerName == "卯ノ花しうね") != 0;
                if (roleInfo == RoleInfo.crewmate && isSiuneInGame && rnd.Next(1, 101) > 90)
                {
                    __instance.RoleText.text = "素村";
                    __instance.RoleBlurbText.text = "大当たりー";
                }

                if (CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.Madmate))
                {
                    if (roleInfo == RoleInfo.crewmate)
                    {
                        __instance.RoleText.text = ModTranslation.getString("madmate");
                    }
                    else
                    {
                        __instance.RoleText.text = ModTranslation.getString("madmatePrefix") + __instance.RoleText.text;
                    }
                    __instance.YouAreText.color = Madmate.color;
                    __instance.RoleText.color = Madmate.color;
                    __instance.RoleBlurbText.text = ModTranslation.getString("madmateIntroDesc");
                    __instance.RoleBlurbText.color = Madmate.color;
                }

                if (infos.Any(info => info.roleType == RoleType.Lovers))
                {
                    PlayerControl otherLover = CachedPlayer.LocalPlayer.PlayerControl.getPartner();
                    __instance.RoleBlurbText.text += "\n" + Helpers.cs(Lovers.color, String.Format(ModTranslation.getString("loversFlavor"), otherLover?.Data?.PlayerName ?? ""));
                }

                // 従来処理
                SoundManager.Instance.PlaySound(CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IntroSound, false, 1f);
                __instance.YouAreText.gameObject.SetActive(true);
                __instance.RoleText.gameObject.SetActive(true);
                __instance.RoleBlurbText.gameObject.SetActive(true);

                if (__instance.ourCrewmate == null)
                {
                    __instance.ourCrewmate = __instance.CreatePlayer(0, 1, CachedPlayer.LocalPlayer.PlayerControl.Data, false);
                    __instance.ourCrewmate.gameObject.SetActive(false);
                }
                __instance.ourCrewmate.gameObject.SetActive(true);
                __instance.ourCrewmate.transform.localPosition = new Vector3(0f, -1.05f, -18f);
                __instance.ourCrewmate.transform.localScale = new Vector3(1f, 1f, 1f);
                yield return new WaitForSeconds(2.5f);
                __instance.YouAreText.gameObject.SetActive(false);
                __instance.RoleText.gameObject.SetActive(false);
                __instance.RoleBlurbText.gameObject.SetActive(false);
                __instance.ourCrewmate.gameObject.SetActive(false);

                yield break;
            }
        }

        [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
        class BeginCrewmatePatch
        {
            public static void Prefix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay)
            {
                setupIntroTeamIcons(__instance, ref teamToDisplay);
            }

            public static void Postfix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay)
            {
                setupIntroTeam(__instance, ref teamToDisplay);
            }
        }

        [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
        class BeginImpostorPatch
        {
            public static void Prefix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
            {
                setupIntroTeamIcons(__instance, ref yourTeam);
            }

            public static void Postfix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
            {
                setupIntroTeam(__instance, ref yourTeam);
            }
        }
    }
}

