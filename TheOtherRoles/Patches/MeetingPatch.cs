using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.CoreScripts;
using BepInEx.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using UnhollowerBaseLib;
using UnityEngine;
using static TheOtherRoles.GameHistory;
using static TheOtherRoles.MapOptions;
using static TheOtherRoles.TheOtherRoles;
using static TheOtherRoles.TheOtherRolesGM;

namespace TheOtherRoles.Patches
{
    [HarmonyPatch]
    class MeetingHudPatch
    {
        static bool[] selections;
        static SpriteRenderer[] renderers;
        private const float scale = 0.65f;
        private static Sprite blankNameplate = null;
        public static bool nameplatesChanged = true;
        public static bool animateSwap = false;

        static TMPro.TextMeshPro meetingInfoText;

        public static void updateNameplate(PlayerVoteArea pva, byte playerId = Byte.MaxValue)
        {
            blankNameplate ??= HatManager.Instance.GetNamePlateById("nameplate_NoPlate")?.viewData?.viewData?.Image;

            var nameplate = blankNameplate;
            if (!hideNameplates)
            {
                var p = Helpers.playerById(playerId != Byte.MaxValue ? playerId : pva.TargetPlayerId);
                var nameplateId = p?.CurrentOutfit?.NamePlateId;
                nameplate = HatManager.Instance.GetNamePlateById(nameplateId).viewData.viewData.Image;
            }
            pva.Background.sprite = nameplate;
        }

        [HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.SetCosmetics))]
        class PlayerVoteAreaCosmetics
        {
            static void Postfix(PlayerVoteArea __instance, GameData.PlayerInfo playerInfo)
            {
                updateNameplate(__instance, playerInfo.PlayerId);
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
        class MeetingHudUpdatePatch
        {
            static void Postfix(MeetingHud __instance)
            {
                if (nameplatesChanged)
                {
                    foreach (var pva in __instance.playerStates)
                    {
                        updateNameplate(pva);
                    }
                    nameplatesChanged = false;
                }

                if (__instance.state == MeetingHud.VoteStates.Animating)
                    return;

                // Deactivate skip Button if skipping on emergency meetings is disabled
                if (blockSkippingInEmergencyMeetings)
                    __instance.SkipVoteButton?.gameObject?.SetActive(false);

                updateMeetingText(__instance);

                // This fixes a bug with the original game where pressing the button and a kill happens simultaneously
                // results in bodies sometimes being created *after* the meeting starts, marking them as dead and
                // removing the corpses so there's no random corpses leftover afterwards
                foreach (DeadBody b in UnityEngine.Object.FindObjectsOfType<DeadBody>())
                {
                    if (b == null) continue;

                    foreach (PlayerVoteArea pva in __instance.playerStates)
                    {
                        if (pva == null) continue;

                        if (pva.TargetPlayerId == b?.ParentId && !pva.AmDead)
                        {
                            pva?.SetDead(pva.DidReport, true);
                            pva?.Overlay?.gameObject?.SetActive(true);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CheckForEndVoting))]
        class MeetingCalculateVotesPatch
        {
            private static Dictionary<byte, int> CalculateVotes(MeetingHud __instance)
            {
                Dictionary<byte, int> dictionary = new();
                for (int i = 0; i < __instance.playerStates.Length; i++)
                {
                    PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                    byte votedTarget = playerVoteArea.TargetPlayerId;
                    byte votedFor = playerVoteArea.VotedFor;
                    Logger.info(String.Format("{0,-2}{1}:{2,-3}{3}", votedTarget, $"({Helpers.getVoteName(votedTarget)})".PadRightV2(40), votedFor, Helpers.getVoteName(votedFor)), "Vote");
                    if (votedFor is not 252 and not 255 and not 254)
                    {
                        PlayerControl player = Helpers.playerById((byte)playerVoteArea.TargetPlayerId);
                        if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected || player.isGM()) continue;

                        // don't try to vote for the GM
                        if (GM.gm != null && votedFor == GM.gm.PlayerId) continue;

                        if (player.isRole(RoleType.BomberB) && BomberA.hasOneVote && BomberA.isAlive()) continue;
                        if (player.isRole(RoleType.MimicA) && MimicK.hasOneVote && MimicK.isAlive()) continue;

                        int additionalVotes = (Mayor.mayor != null && Mayor.mayor.PlayerId == playerVoteArea.TargetPlayerId) ? Mayor.numVotes : 1; // Mayor vote
                        if (dictionary.TryGetValue(votedFor, out int currentVotes))
                            dictionary[votedFor] = currentVotes + additionalVotes;
                        else
                            dictionary[votedFor] = additionalVotes;
                    }
                }

                // Swapper swap votes
                if (Swapper.swapper != null && !Swapper.swapper.Data.IsDead)
                {
                    PlayerVoteArea swapped1 = null;
                    PlayerVoteArea swapped2 = null;
                    foreach (PlayerVoteArea playerVoteArea in __instance.playerStates)
                    {
                        if (playerVoteArea.TargetPlayerId == Swapper.playerId1) swapped1 = playerVoteArea;
                        if (playerVoteArea.TargetPlayerId == Swapper.playerId2) swapped2 = playerVoteArea;
                    }

                    if (swapped1 != null && swapped2 != null)
                    {
                        if (!dictionary.ContainsKey(swapped1.TargetPlayerId)) dictionary[swapped1.TargetPlayerId] = 0;
                        if (!dictionary.ContainsKey(swapped2.TargetPlayerId)) dictionary[swapped2.TargetPlayerId] = 0;
                        (dictionary[swapped2.TargetPlayerId], dictionary[swapped1.TargetPlayerId]) = (dictionary[swapped1.TargetPlayerId], dictionary[swapped2.TargetPlayerId]);
                        if (AmongUsClient.Instance.AmHost)
                        {
                            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.SwapperAnimate, Hazel.SendOption.Reliable, -1);
                            AmongUsClient.Instance.FinishRpcImmediately(writer);
                            RPCProcedure.swapperAnimate();
                        }
                    }
                }

                return dictionary;
            }


            static bool Prefix(MeetingHud __instance)
            {
                if (__instance.playerStates.All((PlayerVoteArea ps) => ps.AmDead || ps.DidVote))
                {

                    Dictionary<byte, int> self = CalculateVotes(__instance);
                    KeyValuePair<byte, int> max = self.MaxPair(out bool tie);
                    GameData.PlayerInfo exiled = GameData.Instance.AllPlayers.ToArray().FirstOrDefault(v => !tie && v.PlayerId == max.Key && !v.IsDead);

                    MeetingHud.VoterState[] array = new MeetingHud.VoterState[__instance.playerStates.Length];
                    for (int i = 0; i < __instance.playerStates.Length; i++)
                    {
                        PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                        array[i] = new MeetingHud.VoterState
                        {
                            VoterId = playerVoteArea.TargetPlayerId,
                            VotedForId = playerVoteArea.VotedFor
                        };
                    }

                    // RPCVotingComplete
                    __instance.RpcVotingComplete(array, exiled, tie);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Select))]
        class MeetingHudSelectPatch
        {
            public static bool Prefix(ref bool __result, MeetingHud __instance, [HarmonyArgument(0)] int suspectStateIdx)
            {
                __result = false;
                if (GM.gm != null && GM.gm.PlayerId == suspectStateIdx) return false;
                if (noVoteIsSelfVote && CachedPlayer.LocalPlayer.PlayerControl.PlayerId == suspectStateIdx) return false;
                if (blockSkippingInEmergencyMeetings && suspectStateIdx == -1) return false;

                return true;
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.BloopAVoteIcon))]
        class MeetingHudBloopAVoteIconPatch
        {
            public static bool Prefix(MeetingHud __instance, [HarmonyArgument(0)] GameData.PlayerInfo voterPlayer, [HarmonyArgument(1)] int index, [HarmonyArgument(2)] Transform parent)
            {
                SpriteRenderer spriteRenderer = UnityEngine.Object.Instantiate<SpriteRenderer>(__instance.PlayerVotePrefab);
                int cId = voterPlayer.DefaultOutfit.ColorId;
                if (!(!PlayerControl.GameOptions.AnonymousVotes || (CachedPlayer.LocalPlayer.PlayerControl.Data.IsDead && MapOptions.ghostsSeeVotes)))
                    voterPlayer.Object.SetColor(6);
                voterPlayer.Object.SetPlayerMaterialColors(spriteRenderer);
                spriteRenderer.transform.SetParent(parent);
                spriteRenderer.transform.localScale = Vector3.zero;
                __instance.StartCoroutine(Effects.Bloop((float)index * 0.3f, spriteRenderer.transform, 1f, 0.5f));
                parent.GetComponent<VoteSpreader>().AddVote(spriteRenderer);
                voterPlayer.Object.SetColor(cId);
                return false;
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.PopulateResults))]
        class MeetingHudPopulateVotesPatch
        {

            static bool Prefix(MeetingHud __instance, Il2CppStructArray<MeetingHud.VoterState> states)
            {
                // Swapper swap
                PlayerVoteArea swapped1 = null;
                PlayerVoteArea swapped2 = null;

                foreach (PlayerVoteArea playerVoteArea in __instance.playerStates)
                {
                    if (playerVoteArea.TargetPlayerId == Swapper.playerId1) swapped1 = playerVoteArea;
                    if (playerVoteArea.TargetPlayerId == Swapper.playerId2) swapped2 = playerVoteArea;
                }
                bool doSwap = animateSwap && swapped1 != null && swapped2 != null && Swapper.swapper != null && !Swapper.swapper.Data.IsDead;
                if (doSwap)
                {
                    __instance.StartCoroutine(Effects.Slide3D(swapped1.transform, swapped1.transform.localPosition, swapped2.transform.localPosition, 1.5f));
                    __instance.StartCoroutine(Effects.Slide3D(swapped2.transform, swapped2.transform.localPosition, swapped1.transform.localPosition, 1.5f));

                    Swapper.numSwaps--;
                }


                __instance.TitleText.text = FastDestroyableSingleton<TranslationController>.Instance.GetString(StringNames.MeetingVotingResults, new Il2CppReferenceArray<Il2CppSystem.Object>(0));
                int num = 0;
                for (int i = 0; i < __instance.playerStates.Length; i++)
                {
                    PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                    byte targetPlayerId = playerVoteArea.TargetPlayerId;
                    // Swapper change playerVoteArea that gets the votes
                    if (doSwap && playerVoteArea.TargetPlayerId == swapped1.TargetPlayerId) playerVoteArea = swapped2;
                    else if (doSwap && playerVoteArea.TargetPlayerId == swapped2.TargetPlayerId) playerVoteArea = swapped1;

                    playerVoteArea.ClearForResults();
                    int num2 = 0;
                    //bool mayorFirstVoteDisplayed = false;
                    Dictionary<int, int> votesApplied = new();
                    for (int j = 0; j < states.Length; j++)
                    {
                        MeetingHud.VoterState voterState = states[j];
                        PlayerControl voter = Helpers.playerById(voterState.VoterId);
                        if (voter == null) continue;

                        GameData.PlayerInfo playerById = GameData.Instance.GetPlayerById(voterState.VoterId);
                        if (playerById == null)
                        {
                            Debug.LogError(string.Format("Couldn't find player info for voter: {0}", voterState.VoterId));
                        }
                        else if (GM.gm != null && (voterState.VoterId == GM.gm.PlayerId || voterState.VotedForId == GM.gm.PlayerId))
                        {
                            continue;
                        }
                        else if (i == 0 && voterState.SkippedVote && !playerById.IsDead)
                        {
                            __instance.BloopAVoteIcon(playerById, num, __instance.SkippedVoting.transform);
                            num++;
                        }
                        else if (voterState.VotedForId == targetPlayerId && !playerById.IsDead)
                        {
                            __instance.BloopAVoteIcon(playerById, num2, playerVoteArea.transform);
                            num2++;
                        }

                        if (!votesApplied.ContainsKey(voter.PlayerId))
                            votesApplied[voter.PlayerId] = 0;

                        votesApplied[voter.PlayerId]++;

                        // Major vote, redo this iteration to place a second vote
                        if (Mayor.mayor != null && voter.PlayerId == Mayor.mayor.PlayerId && votesApplied[voter.PlayerId] < Mayor.numVotes)
                        {
                            j--;
                        }
                    }
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
        class MeetingHudVotingCompletedPatch
        {
            static void Postfix(MeetingHud __instance, [HarmonyArgument(0)] byte[] states, [HarmonyArgument(1)] GameData.PlayerInfo exiled, [HarmonyArgument(2)] bool tie)
            {
                // Reset swapper values
                Swapper.playerId1 = Byte.MaxValue;
                Swapper.playerId2 = Byte.MaxValue;

                if (meetingInfoText != null)
                    meetingInfoText.gameObject.SetActive(false);

                foreach (DeadBody b in UnityEngine.Object.FindObjectsOfType<DeadBody>())
                {
                    UnityEngine.Object.Destroy(b.gameObject);
                }

                if (exiled != null)
                {
                    finalStatuses[exiled.PlayerId] = FinalStatus.Exiled;
                    bool isLovers = exiled.Object.isLovers();

                    if (isLovers)
                        finalStatuses[exiled.Object.getPartner().PlayerId] = FinalStatus.Suicide;
                    Logger.info($"Exiled: {exiled.PlayerId}({Helpers.getVoteName(exiled.PlayerId)})", "Vote");
                }
            }
        }

        static void gmKillOnClick(int i, MeetingHud __instance)
        {
            if (__instance.state == MeetingHud.VoteStates.Results) return;
            SpriteRenderer renderer = renderers[i];
            var target = __instance.playerStates[i];

            if (target != null)
            {
                if (target.AmDead)
                {
                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.GMRevive, Hazel.SendOption.Reliable, -1);
                    writer.Write((byte)target.TargetPlayerId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    RPCProcedure.GMRevive(target.TargetPlayerId);

                    renderer.sprite = Guesser.getTargetSprite();
                    renderer.color = Color.red;
                }
                else
                {
                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.GMKill, Hazel.SendOption.Reliable, -1);
                    writer.Write((byte)target.TargetPlayerId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    RPCProcedure.GMKill(target.TargetPlayerId);

                    renderer.sprite = Swapper.getCheckSprite();
                    renderer.color = Color.green;
                }
            }
        }

        static void swapperOnClick(int i, MeetingHud __instance)
        {
            if (Swapper.numSwaps <= 0) return;
            if (__instance.state == MeetingHud.VoteStates.Results) return;
            if (__instance.playerStates[i].AmDead) return;

            int selectedCount = selections.Where(b => b).Count();
            SpriteRenderer renderer = renderers[i];

            if (selectedCount == 0)
            {
                renderer.color = Color.green;
                selections[i] = true;
            }
            else if (selectedCount == 1)
            {
                if (selections[i])
                {
                    renderer.color = Color.red;
                    selections[i] = false;
                }
                else
                {
                    selections[i] = true;
                    renderer.color = Color.green;

                    PlayerVoteArea firstPlayer = null;
                    PlayerVoteArea secondPlayer = null;
                    for (int A = 0; A < selections.Length; A++)
                    {
                        if (selections[A])
                        {
                            if (firstPlayer != null)
                            {
                                secondPlayer = __instance.playerStates[A];
                                break;
                            }
                            else
                            {
                                firstPlayer = __instance.playerStates[A];
                            }
                        }
                    }

                    if (firstPlayer != null && secondPlayer != null)
                    {
                        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.SwapperSwap, Hazel.SendOption.Reliable, -1);
                        writer.Write((byte)firstPlayer.TargetPlayerId);
                        writer.Write((byte)secondPlayer.TargetPlayerId);
                        AmongUsClient.Instance.FinishRpcImmediately(writer);

                        RPCProcedure.swapperSwap((byte)firstPlayer.TargetPlayerId, (byte)secondPlayer.TargetPlayerId);
                    }
                }
            }
        }

        private static GameObject guesserUI;
        static void guesserOnClick(int buttonTarget, MeetingHud __instance)
        {
            if (guesserUI != null || !(__instance.state == MeetingHud.VoteStates.Voted || __instance.state == MeetingHud.VoteStates.NotVoted)) return;
            __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(false));

            Transform container = UnityEngine.Object.Instantiate(__instance.transform.FindChild("PhoneUI"), __instance.transform);
            container.transform.localPosition = new Vector3(0, 0, -200f);
            guesserUI = container.gameObject;

            int i = 0;
            var buttonTemplate = __instance.playerStates[0].transform.FindChild("votePlayerBase");
            var maskTemplate = __instance.playerStates[0].transform.FindChild("MaskArea");
            var smallButtonTemplate = __instance.playerStates[0].Buttons.transform.Find("CancelButton");
            var textTemplate = __instance.playerStates[0].NameText;

            Transform exitButtonParent = new GameObject().transform;
            exitButtonParent.SetParent(container);
            Transform exitButton = UnityEngine.Object.Instantiate(buttonTemplate.transform, exitButtonParent);
            Transform exitButtonMask = UnityEngine.Object.Instantiate(maskTemplate, exitButtonParent);
            exitButton.gameObject.GetComponent<SpriteRenderer>().sprite = smallButtonTemplate.GetComponent<SpriteRenderer>().sprite;
            exitButtonParent.transform.localPosition = new Vector3(2.725f, 2.1f, -200f);
            exitButtonParent.transform.localScale = new Vector3(0.25f, 0.9f, 1f);
            exitButtonParent.transform.SetAsFirstSibling();
            exitButton.GetComponent<PassiveButton>().OnClick.RemoveAllListeners();
            exitButton.GetComponent<PassiveButton>().OnClick.AddListener((System.Action)(() =>
            {
                __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(true));
                UnityEngine.Object.Destroy(container.gameObject);
            }));

            List<Transform> buttons = new();
            Transform selectedButton = null;

            foreach (RoleInfo roleInfo in RoleInfo.allRoleInfos)
            {
                RoleType guesserRole;
                if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.NiceGuesser))
                {
                    guesserRole = RoleType.NiceGuesser;
                }
                else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.EvilGuesser) || CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.LastImpostor))
                {
                    guesserRole = RoleType.EvilGuesser;
                }
                else
                {
                    guesserRole = RoleType.NiceGuesser;
                }

                if (roleInfo == null ||
                    roleInfo.roleType == RoleType.Lovers ||
                    roleInfo.roleType == guesserRole ||
                    (!Guesser.evilGuesserCanGuessSpy && guesserRole == RoleType.EvilGuesser && roleInfo.roleType == RoleType.Spy) ||
                    roleInfo == RoleInfo.gm ||
                    (Guesser.onlyAvailableRoles && !roleInfo.enabled) ||
                    roleInfo == RoleInfo.bomberB)
                    continue; // Not guessable roles
                if (Guesser.guesserCantGuessSnitch && Snitch.snitch != null)
                {
                    var (playerCompleted, playerTotal) = TasksHandler.taskInfo(Snitch.snitch.Data);
                    int numberOfLeftTasks = playerTotal - playerCompleted;
                    if (numberOfLeftTasks <= 0 && roleInfo.roleType == RoleType.Snitch) continue;
                }
                Transform buttonParent = new GameObject().transform;
                buttonParent.SetParent(container);
                Transform button = UnityEngine.Object.Instantiate(buttonTemplate, buttonParent);
                Transform buttonMask = UnityEngine.Object.Instantiate(maskTemplate, buttonParent);
                TMPro.TextMeshPro label = UnityEngine.Object.Instantiate(textTemplate, button);
                button.GetComponent<SpriteRenderer>().sprite = FastDestroyableSingleton<HatManager>.Instance.GetNamePlateById("nameplate_NoPlate")?.viewData?.viewData?.Image;
                buttons.Add(button);
                int row = i / 5, col = i % 5;
                buttonParent.localPosition = new Vector3(-3.47f + 1.75f * col, 1.5f - 0.45f * row, -200f);
                buttonParent.localScale = new Vector3(0.55f, 0.55f, 1f);
                label.text = Helpers.cs(roleInfo.color, roleInfo.name);
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.transform.localPosition = new Vector3(0, 0, label.transform.localPosition.z);
                label.transform.localScale *= 1.6f;
                label.autoSizeTextContainer = true;
                int copiedIndex = i;

                button.GetComponent<PassiveButton>().OnClick.RemoveAllListeners();
                if (CachedPlayer.LocalPlayer.PlayerControl.isAlive()) button.GetComponent<PassiveButton>().OnClick.AddListener((System.Action)(() =>
                {
                    if (selectedButton != button)
                    {
                        selectedButton = button;
                        buttons.ForEach(x => x.GetComponent<SpriteRenderer>().color = x == selectedButton ? Color.red : Color.white);
                    }
                    else
                    {
                        PlayerControl focusedTarget = Helpers.playerById((byte)__instance.playerStates[buttonTarget].TargetPlayerId);
                        if (!(__instance.state == MeetingHud.VoteStates.Voted || __instance.state == MeetingHud.VoteStates.NotVoted) || focusedTarget == null) return;
                        if (Guesser.remainingShots(CachedPlayer.LocalPlayer.PlayerControl) <= 0) return;

                        if (!Guesser.killsThroughShield && focusedTarget == Medic.shielded)
                        { // Depending on the options, shooting the shielded player will not allow the guess, notifiy everyone about the kill attempt and close the window
                            __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(true));
                            UnityEngine.Object.Destroy(container.gameObject);

                            MessageWriter murderAttemptWriter = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.ShieldedMurderAttempt, Hazel.SendOption.Reliable, -1);
                            AmongUsClient.Instance.FinishRpcImmediately(murderAttemptWriter);
                            RPCProcedure.shieldedMurderAttempt();
                            return;
                        }

                        var mainRoleInfo = RoleInfo.getRoleInfoForPlayer(focusedTarget).FirstOrDefault();
                        if (mainRoleInfo == null) return;

                        // BomberAとBomberBを同等に扱う
                        PlayerControl dyingTarget;
                        if (mainRoleInfo == roleInfo)
                        {
                            dyingTarget = focusedTarget;
                        }
                        else if (roleInfo == RoleInfo.bomberA && mainRoleInfo == RoleInfo.bomberB)
                        {
                            dyingTarget = focusedTarget;
                        }
                        else
                        {
                            dyingTarget = CachedPlayer.LocalPlayer.PlayerControl;
                        }


                        // Reset the GUI
                        __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(true));
                        UnityEngine.Object.Destroy(container.gameObject);
                        if (Guesser.hasMultipleShotsPerMeeting && Guesser.remainingShots(CachedPlayer.LocalPlayer.PlayerControl) > 1 && dyingTarget != CachedPlayer.LocalPlayer.PlayerControl)
                        {
                            __instance.playerStates.ToList().ForEach(x => { if (x.TargetPlayerId == dyingTarget.PlayerId && x.transform.FindChild("ShootButton") != null) UnityEngine.Object.Destroy(x.transform.FindChild("ShootButton").gameObject); });
                        }
                        else
                        {
                            __instance.playerStates.ToList().ForEach(x => { if (x.transform.FindChild("ShootButton") != null) UnityEngine.Object.Destroy(x.transform.FindChild("ShootButton").gameObject); });
                        }

                        // Shoot player and send chat info if activated
                        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.GuesserShoot, Hazel.SendOption.Reliable, -1);
                        writer.Write(CachedPlayer.LocalPlayer.PlayerControl.PlayerId);
                        writer.Write(dyingTarget.PlayerId);
                        writer.Write(focusedTarget.PlayerId);
                        writer.Write((byte)roleInfo.roleType);
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                        RPCProcedure.guesserShoot(CachedPlayer.LocalPlayer.PlayerControl.PlayerId, dyingTarget.PlayerId, focusedTarget.PlayerId, (byte)roleInfo.roleType);
                    }
                }));

                i++;
            }
            container.transform.localScale *= 0.75f;
        }

        [HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.Select))]
        class PlayerVoteAreaSelectPatch
        {
            static bool Prefix(MeetingHud __instance)
            {
                return !(CachedPlayer.LocalPlayer.PlayerControl != null && Guesser.isGuesser(CachedPlayer.LocalPlayer.PlayerControl.PlayerId) && guesserUI != null);
            }
        }


        static void populateButtonsPostfix(MeetingHud __instance)
        {
            nameplatesChanged = true;

            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.GM) && GM.canKill)
            {
                renderers = new SpriteRenderer[__instance.playerStates.Length];

                for (int i = 0; i < __instance.playerStates.Length; i++)
                {
                    PlayerVoteArea playerVoteArea = __instance.playerStates[i];

                    GameObject template = playerVoteArea.Buttons.transform.Find("CancelButton").gameObject;
                    GameObject checkbox = UnityEngine.Object.Instantiate(template);
                    checkbox.transform.SetParent(playerVoteArea.transform);
                    checkbox.transform.position = template.transform.position;
                    checkbox.transform.localPosition = new Vector3(-0.95f, 0.03f, -20f);
                    SpriteRenderer renderer = checkbox.GetComponent<SpriteRenderer>();
                    renderer.sprite = playerVoteArea.AmDead ? Swapper.getCheckSprite() : Guesser.getTargetSprite();
                    renderer.color = playerVoteArea.AmDead ? Color.green : Color.red;

                    PassiveButton button = checkbox.GetComponent<PassiveButton>();
                    button.OnClick.RemoveAllListeners();
                    int copiedIndex = i;
                    button.OnClick.AddListener((UnityEngine.Events.UnityAction)(() => gmKillOnClick(copiedIndex, __instance)));

                    renderers[i] = renderer;
                }
            }

            // Add Swapper Buttons
            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Swapper) && Swapper.numSwaps > 0 && !Swapper.swapper.Data.IsDead)
            {
                selections = new bool[__instance.playerStates.Length];
                renderers = new SpriteRenderer[__instance.playerStates.Length];

                for (int i = 0; i < __instance.playerStates.Length; i++)
                {
                    PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                    if (playerVoteArea.AmDead || (playerVoteArea.TargetPlayerId == Swapper.swapper.PlayerId && Swapper.canOnlySwapOthers) || (playerVoteArea.TargetPlayerId == GM.gm?.PlayerId)) continue;

                    GameObject template = playerVoteArea.Buttons.transform.Find("CancelButton").gameObject;
                    GameObject checkbox = UnityEngine.Object.Instantiate(template);
                    checkbox.transform.SetParent(playerVoteArea.transform);
                    checkbox.transform.position = template.transform.position;
                    checkbox.transform.localPosition = new Vector3(-0.95f, 0.03f, -20f);
                    SpriteRenderer renderer = checkbox.GetComponent<SpriteRenderer>();
                    renderer.sprite = Swapper.getCheckSprite();
                    renderer.color = Color.red;

                    PassiveButton button = checkbox.GetComponent<PassiveButton>();
                    button.OnClick.RemoveAllListeners();
                    int copiedIndex = i;
                    button.OnClick.AddListener((System.Action)(() => swapperOnClick(copiedIndex, __instance)));

                    selections[i] = false;
                    renderers[i] = renderer;
                }

            }

            // Add overlay for spelled players
            if (Witch.witch != null && Witch.futureSpelled != null)
            {
                foreach (PlayerVoteArea pva in __instance.playerStates)
                {
                    if (Witch.futureSpelled.Any(x => x.PlayerId == pva.TargetPlayerId))
                    {
                        SpriteRenderer rend = new GameObject().AddComponent<SpriteRenderer>();
                        rend.transform.SetParent(pva.transform);
                        rend.gameObject.layer = pva.Megaphone.gameObject.layer;
                        rend.transform.localPosition = new Vector3(-0.5f, -0.03f, -1f);
                        rend.sprite = Witch.getSpelledOverlaySprite();
                    }
                }
            }

            // トラックボタン
            bool isTrackerButton = EvilTracker.canSetTargetOnMeeting && EvilTracker.target == null && CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.EvilTracker) && CachedPlayer.LocalPlayer.PlayerControl.isAlive();
            if (isTrackerButton)
            {
                for (int i = 0; i < __instance.playerStates.Length; i++)
                {
                    PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                    if (playerVoteArea.AmDead || playerVoteArea.TargetPlayerId == CachedPlayer.LocalPlayer.PlayerControl.PlayerId) continue;
                    GameObject template = playerVoteArea.Buttons.transform.Find("CancelButton").gameObject;
                    GameObject targetBox = UnityEngine.Object.Instantiate(template, playerVoteArea.transform);
                    targetBox.name = "EvilTrackerButton";
                    targetBox.transform.localPosition = new Vector3(-0.95f, 0.03f, -1.3f);
                    SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
                    renderer.sprite = EvilTracker.getArrowSprite();
                    renderer.color = Palette.CrewmateBlue;
                    PassiveButton button = targetBox.GetComponent<PassiveButton>();
                    button.OnClick.RemoveAllListeners();
                    int copiedIndex = i;
                    button.OnClick.AddListener((System.Action)(() =>
                    {
                        PlayerControl focusedTarget = Helpers.playerById((byte)__instance.playerStates[copiedIndex].TargetPlayerId);
                        EvilTracker.target = focusedTarget;
                        // Reset the GUI
                        __instance.playerStates.ToList().ForEach(x => { if (x.transform.FindChild("EvilTrackerButton") != null) UnityEngine.Object.Destroy(x.transform.FindChild("EvilTrackerButton").gameObject); });
                        GameObject targetMark = UnityEngine.Object.Instantiate(template, playerVoteArea.transform);
                        targetMark.name = "EvilTrackerMark";
                        PassiveButton button = targetMark.GetComponent<PassiveButton>();
                        targetMark.transform.localPosition = new Vector3(1.1f, 0.03f, -20f);
                        GameObject.Destroy(button);
                        SpriteRenderer renderer = targetMark.GetComponent<SpriteRenderer>();
                        renderer.sprite = EvilTracker.getArrowSprite();
                        renderer.color = Palette.CrewmateBlue;

                        bool isGuesserButton = Guesser.isGuesser(CachedPlayer.LocalPlayer.PlayerControl.PlayerId) && CachedPlayer.LocalPlayer.PlayerControl.isAlive() && Guesser.remainingShots(CachedPlayer.LocalPlayer.PlayerControl) > 0;
                        bool isLastImpostorButton = CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.LastImpostor) && CachedPlayer.LocalPlayer.PlayerControl.isAlive() && LastImpostor.canGuess();
                        if (isGuesserButton || isLastImpostorButton)
                        {
                            createGuesserButton(__instance);
                        }
                    }));
                }
            }

            // Add Guesser Buttons
            bool isGuesserButton = !isTrackerButton && Guesser.isGuesser(CachedPlayer.LocalPlayer.PlayerControl.PlayerId) && CachedPlayer.LocalPlayer.PlayerControl.isAlive() && Guesser.remainingShots(CachedPlayer.LocalPlayer.PlayerControl) > 0;
            bool isLastImpostorButton = !isTrackerButton && CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.LastImpostor) && CachedPlayer.LocalPlayer.PlayerControl.isAlive() && LastImpostor.canGuess();
            if (isGuesserButton || isLastImpostorButton)
            {
                createGuesserButton(__instance);
            }

        }

        public static void createGuesserButton(MeetingHud __instance)
        {
            for (int i = 0; i < __instance.playerStates.Length; i++)
            {
                PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                if (playerVoteArea.AmDead || playerVoteArea.TargetPlayerId == CachedPlayer.LocalPlayer.PlayerControl.PlayerId || playerVoteArea.TargetPlayerId == GM.gm?.PlayerId) continue;

                GameObject template = playerVoteArea.Buttons.transform.Find("CancelButton").gameObject;
                GameObject targetBox = UnityEngine.Object.Instantiate(template, playerVoteArea.transform);
                targetBox.name = "ShootButton";
                targetBox.transform.localPosition = new Vector3(-0.95f, 0.03f, -1.3f);
                SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
                renderer.sprite = Guesser.getTargetSprite();
                PassiveButton button = targetBox.GetComponent<PassiveButton>();
                button.OnClick.RemoveAllListeners();
                int copiedIndex = i;
                button.OnClick.AddListener((System.Action)(() => guesserOnClick(copiedIndex, __instance)));
            }
        }

        public static void updateMeetingText(MeetingHud __instance)
        {
            // Uses remaining text for guesser/swapper
            if (meetingInfoText == null)
            {
                meetingInfoText = UnityEngine.Object.Instantiate(FastDestroyableSingleton<HudManager>.Instance.TaskText, __instance.transform);
                meetingInfoText.alignment = TMPro.TextAlignmentOptions.BottomLeft;
                meetingInfoText.transform.position = Vector3.zero;
                meetingInfoText.transform.localPosition = new Vector3(-3.07f, 3.33f, -20f);
                meetingInfoText.transform.localScale *= 1.1f;
                meetingInfoText.color = Palette.White;
                meetingInfoText.gameObject.SetActive(false);
            }

            meetingInfoText.text = "";
            meetingInfoText.gameObject.SetActive(false);

            if (MeetingHud.Instance.state is not MeetingHud.VoteStates.Voted and
                not MeetingHud.VoteStates.NotVoted and
                not MeetingHud.VoteStates.Discussion)
                return;

            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Swapper) && Swapper.numSwaps > 0 && !Swapper.swapper.Data.IsDead)
            {
                meetingInfoText.text = String.Format(ModTranslation.getString("swapperSwapsLeft"), Swapper.numSwaps);
                meetingInfoText.gameObject.SetActive(true);
            }

            int numGuesses = Guesser.remainingShots(CachedPlayer.LocalPlayer.PlayerControl);
            if ((Guesser.isGuesser(CachedPlayer.LocalPlayer.PlayerControl.PlayerId) || CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.LastImpostor)) && CachedPlayer.LocalPlayer.PlayerControl.isAlive() && numGuesses > 0)
            {
                meetingInfoText.text = String.Format(ModTranslation.getString("guesserGuessesLeft"), numGuesses);
                meetingInfoText.gameObject.SetActive(true);
            }

            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Shifter) && Shifter.futureShift != null)
            {
                meetingInfoText.text = String.Format(ModTranslation.getString("shifterTargetInfo"), Shifter.futureShift.Data.PlayerName);
                meetingInfoText.gameObject.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.ServerStart))]
        class MeetingServerStartPatch
        {
            static void Postfix(MeetingHud __instance, byte reporter)
            {
                // Helpers.log("ServerStart Postfix");
                // Helpers.log($"StackTrace: '{System.Environment.StackTrace}'");
                // populateButtonsPostfix(__instance);
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Deserialize))]
        class MeetingDeserializePatch
        {
            static void Postfix(MeetingHud __instance, [HarmonyArgument(0)] MessageReader reader, [HarmonyArgument(1)] bool initialState)
            {

                // Helpers.log("Deserialize Postfix");
                // Helpers.log($"StackTrace: '{System.Environment.StackTrace}'");
                // Add swapper buttons
                // if (initialState) {
                //     populateButtonsPostfix(__instance);
                // }
            }
        }

        public static void startMeeting()
        {
            animateSwap = false;
            CustomOverlays.showBlackBG();
            CustomOverlays.hideInfoOverlay();
            CustomOverlays.hideRoleOverlay();
            TheOtherRolesGM.OnMeetingStart();
            MapBehaviorPatch.shareRealTasks();
        }

        public static void populateButtons(MeetingHud __instance, byte reporter)
        {
            // 投票画面に人形遣いのダミーを表示させない
            // 会議に参加しないPlayerControlを持つRoleが増えたらこのListに追加
            // 特殊なplayerInfo.Role.Roleを設定することで自動的に無視できないか？もしくはフラグをplayerInfoのどこかに追加
            var playerControlesToBeIgnored = new List<PlayerControl>() { Puppeteer.dummy };
            playerControlesToBeIgnored.RemoveAll(x => x == null);
            var playerIdsToBeIgnored = playerControlesToBeIgnored.Select(x => x.PlayerId);
            // Generate PlayerVoteAreas
            __instance.playerStates = new PlayerVoteArea[GameData.Instance.PlayerCount - playerIdsToBeIgnored.Count()];
            int playerStatesCounter = 0;
            for (int i = 0; i < __instance.playerStates.Length + playerIdsToBeIgnored.Count(); i++)
            {
                if (playerIdsToBeIgnored.Contains(GameData.Instance.AllPlayers[i].PlayerId))
                {
                    continue;
                }
                GameData.PlayerInfo playerInfo = GameData.Instance.AllPlayers[i];
                PlayerVoteArea playerVoteArea = __instance.playerStates[playerStatesCounter] = __instance.CreateButton(playerInfo);
                playerVoteArea.Parent = __instance;
                playerVoteArea.SetTargetPlayerId(playerInfo.PlayerId);
                playerVoteArea.SetDead(reporter == playerInfo.PlayerId, playerInfo.Disconnected || playerInfo.IsDead, playerInfo.Role.Role == RoleTypes.GuardianAngel);
                playerVoteArea.UpdateOverlay();
                playerStatesCounter++;
            }
            foreach (PlayerVoteArea playerVoteArea2 in __instance.playerStates)
            {
                ControllerManager.Instance.AddSelectableUiElement(playerVoteArea2.PlayerButton, false);
            }
            __instance.SortButtons();
        }

        [HarmonyPatch(typeof(HudManager), nameof(HudManager.OpenMeetingRoom))]
        class OpenMeetingPatch
        {
            public static void Prefix(HudManager __instance)
            {
                startMeeting();
            }
        }
        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.PopulateButtons))]
        class MeetingHudPopulae
        {
            public static bool Prefix(MeetingHud __instance, byte reporter)
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
        class PlayerControlStartMeetingPatch
        {
            private static float delay { get { return CustomOptionHolder.delayBeforeMeeting.getFloat(); } }

            private static IEnumerator CoStartMeeting(PlayerControl reporter, GameData.PlayerInfo target)
            {
                // 既存処理の移植
                {
                    while (!MeetingHud.Instance)
                    {
                        yield return null;
                    }
                    MeetingRoomManager.Instance.RemoveSelf();
                    for (int i = 0; i < PlayerControl.AllPlayerControls.Count; i++)
                    {
                        PlayerControl playerControl = PlayerControl.AllPlayerControls[i];
                        if (playerControl != null)
                        {
                            playerControl.ResetForMeeting();
                        }
                    }
                    if (MapBehaviour.Instance)
                    {
                        MapBehaviour.Instance.Close();
                    }
                    if (Minigame.Instance)
                    {
                        Minigame.Instance.ForceClose();
                    }
                    MapUtilities.CachedShipStatus.OnMeetingCalled();
                    KillAnimation.SetMovement(reporter, true);
                }

                // 遅延処理追加そのままyield returnで待ちを入れるとロックしたのでHudManagerのコルーチンとして実行させる
                FastDestroyableSingleton<HudManager>.Instance.StartCoroutine(CoStartMeeting2(reporter, target).WrapToIl2Cpp());
                yield break;
            }
            private static IEnumerator CoStartMeeting2(PlayerControl reporter, GameData.PlayerInfo target)
            {
                // Modで追加する遅延処理
                {
                    // ボタンと同時に通報が入った場合のバグ対応、他のクライアントからキルイベントが飛んでくるのを待つ
                    // 見えては行けないものが見えるので暗転させる
                    MeetingHud.Instance.state = MeetingHud.VoteStates.Animating; //ゲッサーのキル用meetingupdateが呼ばれないようにするおまじない（呼ばれるとバグる）
                    HudManager hudManager = FastDestroyableSingleton<HudManager>.Instance;
                    var blackscreen = UnityEngine.Object.Instantiate(hudManager.FullScreen, hudManager.transform);
                    var greyscreen = UnityEngine.Object.Instantiate(hudManager.FullScreen, hudManager.transform);
                    blackscreen.color = Palette.Black;
                    blackscreen.transform.position = Vector3.zero;
                    blackscreen.transform.localPosition = new Vector3(0f, 0f, -910f);
                    blackscreen.transform.localScale = new Vector3(10f, 10f, 1f);
                    blackscreen.gameObject.SetActive(true);
                    blackscreen.enabled = true;
                    greyscreen.color = Palette.Black;
                    greyscreen.transform.position = Vector3.zero;
                    greyscreen.transform.localPosition = new Vector3(0f, 0f, -920f);
                    greyscreen.transform.localScale = new Vector3(10f, 10f, 1f);
                    greyscreen.gameObject.SetActive(true);
                    greyscreen.enabled = true;
                    TMPro.TMP_Text text;
                    RoomTracker roomTracker = FastDestroyableSingleton<HudManager>.Instance?.roomTracker;
                    GameObject gameObject = UnityEngine.Object.Instantiate(roomTracker.gameObject);
                    UnityEngine.Object.DestroyImmediate(gameObject.GetComponent<RoomTracker>());
                    gameObject.transform.SetParent(FastDestroyableSingleton<HudManager>.Instance.transform);
                    gameObject.transform.localPosition = new Vector3(0, 0, -930f);
                    gameObject.transform.localScale = Vector3.one * 5f;
                    text = gameObject.GetComponent<TMPro.TMP_Text>();
                    yield return Effects.Lerp(delay, new Action<float>((p) =>
                    { // Delayed action
                        greyscreen.color = new Color(1.0f, 1.0f, 1.0f, 0.5f - p / 2);
                        string message = (delay - (p * delay)).ToString("0.00");
                        if (message == "0") return;
                        string prefix = "<color=#FFFFFFFF>";
                        text.text = prefix + message + "</color>";
                        if (text != null) text.color = Color.white;
                    }));
                    // yield return new WaitForSeconds(2f);
                    UnityEngine.Object.Destroy(text.gameObject);
                    UnityEngine.Object.Destroy(blackscreen);
                    UnityEngine.Object.Destroy(greyscreen);

                    // ミーティング画面の並び替えを直す
                    populateButtons(MeetingHud.Instance, reporter.Data.PlayerId);
                    populateButtonsPostfix(MeetingHud.Instance);
                }

                // 既存処理の移植
                {
                    DeadBody[] array = UnityEngine.Object.FindObjectsOfType<DeadBody>();
                    GameData.PlayerInfo[] deadBodies = (from b in array
                                                        select GameData.Instance.GetPlayerById(b.ParentId)).ToArray<GameData.PlayerInfo>();
                    for (int j = 0; j < array.Length; j++)
                    {
                        if (array[j] != null && array[j].gameObject != null)
                        {
                            UnityEngine.Object.Destroy(array[j].gameObject);
                        }
                        else
                        {
                            Debug.LogError("Encountered a null Dead Body while destroying.");
                        }
                    }
                    ShapeshifterEvidence[] array2 = UnityEngine.Object.FindObjectsOfType<ShapeshifterEvidence>();
                    for (int k = 0; k < array2.Length; k++)
                    {
                        if (array2[k] != null && array2[k].gameObject != null)
                        {
                            UnityEngine.Object.Destroy(array2[k].gameObject);
                        }
                        else
                        {
                            Debug.LogError("Encountered a null Evidence while destroying.");
                        }
                    }
                    MeetingHud.Instance.StartCoroutine(MeetingHud.Instance.CoIntro(reporter.Data, target, deadBodies));
                }
                yield break;
            }
            private static void StartMeeting(PlayerControl reporter, GameData.PlayerInfo target)
            {
                ShipStatus.Instance.StartCoroutine(CoStartMeeting(reporter, target).WrapToIl2Cpp());
            }
            public static bool Prefix(PlayerControl __instance, GameData.PlayerInfo target)
            {
                // MOD追加処理
                {
                    Logger.info("ShipStatus.StartMeeting");
                    startMeeting();
                    // Safe AntiTeleport positions
                    AntiTeleport.position = CachedPlayer.LocalPlayer.PlayerControl.transform.position;
                    // Medium meeting start time
                    Medium.meetingStartTime = DateTime.UtcNow;
                    // Reset vampire bitten
                    Vampire.bitten = null;
                    // Count meetings
                    if (target == null) meetingsCount++;
                }

                // 既存処理の移植
                {
                    bool flag = target == null;
                    Telemetry.Instance.WriteMeetingStarted(flag);
                    StartMeeting(__instance, target); // 変更部分
                    if (__instance.AmOwner)
                    {
                        if (flag)
                        {
                            __instance.RemainingEmergencies--;
                            StatsManager.Instance.IncrementStat(StringNames.StatsEmergenciesCalled);
                            return false;
                        }
                        StatsManager.Instance.IncrementStat(StringNames.StatsBodiesReported);
                    }
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
        class MeetingHudClosePatch
        {
            static void Postfix(MeetingHud __instance)
            {
                if (PlayerControl.GameOptions.MapId == 2 && CustomOptionHolder.polusRandomSpawn.getBool())
                {
                    if (AmongUsClient.Instance.AmHost)
                    {
                        foreach (PlayerControl player in CachedPlayer.AllPlayers)
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
                }
            }
        }
        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        class MeetingHudStartPatch
        {
            public static void Prefix(MeetingHud __instance)
            {
                Logger.info("---------Meeting Start----------", "Phase");
            }
        }
        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.OnDestroy))]
        class MeetingHudOnDestroyPatch
        {
            public static void Postfix(MeetingHud __instance)
            {
                Logger.info("----------Meeting End-----------", "Phase");
            }
        }

        [HarmonyPatch(typeof(HudManager), nameof(HudManager.SetHudActive))]
        class HudManagerSetHudActive
        {
            public static void Postfix(HudManager __instance)
            {
                FastDestroyableSingleton<HudManager>.Instance.transform.FindChild("TaskDisplay").FindChild("TaskPanel").gameObject.SetActive(true);
            }
        }
    }
}
