using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using TheOtherRoles.Objects;
using UnityEngine;
using static TheOtherRoles.GameHistory;
using static TheOtherRoles.TheOtherRoles;
using static TheOtherRoles.TheOtherRolesGM;

namespace TheOtherRoles.Patches
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class PlayerControlFixedUpdatePatch
    {
        private static Dictionary<byte, bool> lastVisible = new();
        // Helpers

        public static PlayerControl setTarget(bool onlyCrewmates = false, bool targetPlayersInVents = false, List<PlayerControl> untargetablePlayers = null, PlayerControl targetingPlayer = null, int killDistance = 3)
        {
            PlayerControl result = null;
            int kd = killDistance == 3 ? PlayerControl.GameOptions.KillDistance : killDistance;
            float num = GameOptionsData.KillDistances[Mathf.Clamp(kd, 0, 2)];
            if (!MapUtilities.CachedShipStatus) return result;
            if (targetingPlayer == null) targetingPlayer = PlayerControl.LocalPlayer;
            if ((targetingPlayer.Data.IsDead && !targetingPlayer.isRole(RoleType.Puppeteer)) || targetingPlayer.inVent) return result;
            if (targetingPlayer.isGM()) return result;

            if (untargetablePlayers == null)
            {
                untargetablePlayers = new List<PlayerControl>();
            }

            // GM is untargetable by anything
            if (GM.gm != null)
            {
                untargetablePlayers.Add(GM.gm);
            }

            // Can't target stealthed ninjas if setting on
            if (!Ninja.canBeTargeted)
            {
                foreach (Ninja n in Ninja.players)
                {
                    if (n.stealthed) untargetablePlayers.Add(n.player);
                }
            }

            // Can't target stealthed Fox
            foreach (Fox f in Fox.players)
            {
                if (f.stealthed) untargetablePlayers.Add(f.player);
            }

            // 透明になっている人形使い or ダミーをターゲット不可にする
            if (Puppeteer.exists)
            {
                if (Puppeteer.stealthed)
                {
                    var puppeteer = Puppeteer.players.FirstOrDefault().player;
                    untargetablePlayers.Add(puppeteer);
                }
                else if (!Puppeteer.stealthed && Puppeteer.dummy != null)
                {
                    untargetablePlayers.Add(Puppeteer.dummy);
                }
            }


            Vector2 truePosition = targetingPlayer.GetTruePosition();
            foreach (var playerInfo in GameData.Instance.AllPlayers)
            {
                if (!playerInfo.Disconnected && playerInfo.PlayerId != targetingPlayer.PlayerId && !playerInfo.IsDead && (!onlyCrewmates || !playerInfo.Role.IsImpostor))
                {
                    PlayerControl @object = playerInfo.Object;
                    if (untargetablePlayers.Any(x => x == @object))
                    {
                        // if that player is not targetable: skip check
                        continue;
                    }

                    if (@object && (!@object.inVent || targetPlayersInVents))
                    {
                        Vector2 vector = @object.GetTruePosition() - truePosition;
                        float magnitude = vector.magnitude;
                        if (magnitude <= num && !PhysicsHelpers.AnyNonTriggersBetween(truePosition, vector.normalized, magnitude, Constants.ShipAndObjectsMask))
                        {
                            result = @object;
                            num = magnitude;
                        }
                    }
                }
            }
            return result;
        }

        public static void setPlayerOutline(PlayerControl target, Color color)
        {
            if (target == null || target.cosmetics?.currentBodySprite?.BodySprite == null) return;

            target.cosmetics?.currentBodySprite?.BodySprite.material.SetFloat("_Outline", 1f);
            target.cosmetics?.currentBodySprite?.BodySprite.material.SetColor("_OutlineColor", color);
        }

        // Update functions

        static void setBasePlayerOutlines()
        {
            foreach (PlayerControl target in CachedPlayer.AllPlayers)
            {
                if (target == null || target.cosmetics?.currentBodySprite?.BodySprite == null) continue;

                bool isMorphedMorphling = target == Morphling.morphling && Morphling.morphTarget != null && Morphling.morphTimer > 0f;
                bool hasVisibleShield = false;
                if (Camouflager.camouflageTimer <= 0f && Medic.shielded != null && ((target == Medic.shielded && !isMorphedMorphling) || (isMorphedMorphling && Morphling.morphTarget == Medic.shielded)))
                {
                    hasVisibleShield = Medic.showShielded == 0 // Everyone
                        || (Medic.showShielded == 1 && (CachedPlayer.LocalPlayer.PlayerControl == Medic.shielded || CachedPlayer.LocalPlayer.PlayerControl == Medic.medic)) // Shielded + Medic
                        || (Medic.showShielded == 2 && CachedPlayer.LocalPlayer.PlayerControl == Medic.medic); // Medic only
                }

                if (hasVisibleShield)
                {
                    target.cosmetics?.currentBodySprite?.BodySprite.material.SetFloat("_Outline", 1f);
                    target.cosmetics?.currentBodySprite?.BodySprite.material.SetColor("_OutlineColor", Medic.shieldedColor);
                }
                else
                {
                    target.cosmetics?.currentBodySprite?.BodySprite.material.SetFloat("_Outline", 0f);
                }
            }
        }

        public static void bendTimeUpdate()
        {
            if (TimeMaster.isRewinding)
            {
                if (localPlayerPositions.Count > 0)
                {
                    // Set position
                    var next = localPlayerPositions[0];
                    if (next.Item2 == true)
                    {
                        // Exit current vent if necessary
                        if (CachedPlayer.LocalPlayer.PlayerControl.inVent)
                        {
                            foreach (Vent vent in MapUtilities.CachedShipStatus.AllVents)
                            {
                                vent.CanUse(CachedPlayer.LocalPlayer.PlayerControl.Data, out bool canUse, out bool couldUse);
                                if (canUse)
                                {
                                    CachedPlayer.LocalPlayer.PlayerControl.MyPhysics.RpcExitVent(vent.Id);
                                    vent.SetButtons(false);
                                }
                            }
                        }
                        // Set position
                        CachedPlayer.LocalPlayer.PlayerControl.transform.position = next.Item1;
                    }
                    else if (localPlayerPositions.Any(x => x.Item2 == true))
                    {
                        CachedPlayer.LocalPlayer.PlayerControl.transform.position = next.Item1;
                    }
                    if (SubmergedCompatibility.isSubmerged())
                    {
                        SubmergedCompatibility.ChangeFloor(next.Item1.y > -7);
                    }

                    localPlayerPositions.RemoveAt(0);

                    if (localPlayerPositions.Count > 1) localPlayerPositions.RemoveAt(0); // Skip every second position to rewinde twice as fast, but never skip the last position
                }
                else
                {
                    TimeMaster.isRewinding = false;
                    CachedPlayer.LocalPlayer.PlayerControl.moveable = true;
                }
            }
            else
            {
                while (localPlayerPositions.Count >= Mathf.Round(TimeMaster.rewindTime / Time.fixedDeltaTime)) localPlayerPositions.RemoveAt(localPlayerPositions.Count - 1);
                localPlayerPositions.Insert(0, new Tuple<Vector3, bool>(CachedPlayer.LocalPlayer.PlayerControl.transform.position, CachedPlayer.LocalPlayer.PlayerControl.CanMove)); // CanMove = CanMove
            }
        }

        static void medicSetTarget()
        {
            if (Medic.medic == null || Medic.medic != CachedPlayer.LocalPlayer.PlayerControl) return;
            Medic.currentTarget = setTarget();
            if (!Medic.usedShield) setPlayerOutline(Medic.currentTarget, Medic.shieldedColor);
        }

        static void shifterUpdate()
        {
            if (Shifter.shifter == null || Shifter.shifter != CachedPlayer.LocalPlayer.PlayerControl) return;

            List<PlayerControl> blockShift = null;
            if (Shifter.isNeutral && !Shifter.shiftPastShifters)
            {
                blockShift = new List<PlayerControl>();
                foreach (var playerId in Shifter.pastShifters)
                {
                    blockShift.Add(Helpers.playerById((byte)playerId));
                }
            }

            Shifter.currentTarget = setTarget(untargetablePlayers: blockShift);
            if (Shifter.futureShift == null) setPlayerOutline(Shifter.currentTarget, Shifter.color);
        }

        static void morphlingSetTarget()
        {
            if (Morphling.morphling == null || Morphling.morphling != CachedPlayer.LocalPlayer.PlayerControl) return;
            Morphling.currentTarget = setTarget();
            setPlayerOutline(Morphling.currentTarget, Morphling.color);
        }

        static void evilHackerSetTarget()
        {
            if (EvilHacker.evilHacker == null || EvilHacker.evilHacker != CachedPlayer.LocalPlayer.PlayerControl) return;
            EvilHacker.currentTarget = setTarget(true);
            setPlayerOutline(EvilHacker.currentTarget, EvilHacker.color);
        }

        static void trackerSetTarget()
        {
            if (Tracker.tracker == null || Tracker.tracker != CachedPlayer.LocalPlayer.PlayerControl) return;
            Tracker.currentTarget = setTarget();
            if (!Tracker.usedTracker) setPlayerOutline(Tracker.currentTarget, Tracker.color);
        }

        static void detectiveUpdateFootPrints()
        {
            if (Detective.detective == null || Detective.detective != CachedPlayer.LocalPlayer.PlayerControl) return;

            Detective.timer -= Time.fixedDeltaTime;
            if (Detective.timer <= 0f)
            {
                Detective.timer = Detective.footprintInterval;
                foreach (PlayerControl player in CachedPlayer.AllPlayers)
                {
                    if (player != null && player != CachedPlayer.LocalPlayer.PlayerControl && !player.Data.IsDead && !player.inVent && !player.isGM())
                    {
                        new Footprint(Detective.footprintDuration, Detective.anonymousFootprints, player);
                    }
                }
            }
        }

        static void vampireSetTarget()
        {
            if (Vampire.vampire == null || Vampire.vampire != CachedPlayer.LocalPlayer.PlayerControl) return;

            PlayerControl target = null;
            if (Spy.spy != null)
            {
                if (Spy.impostorsCanKillAnyone)
                {
                    target = setTarget(false, true);
                }
                else
                {
                    var listp = new List<PlayerControl>
                    {
                        Spy.spy
                    };
                    if (Sidekick.wasTeamRed) listp.Add(Sidekick.sidekick);
                    if (Jackal.wasTeamRed) listp.Add(Jackal.jackal);
                    target = setTarget(true, true, listp);
                }
            }
            else
            {
                target = setTarget(true, true);
            }

            bool targetNearGarlic = false;
            if (target != null)
            {
                foreach (Garlic garlic in Garlic.garlics)
                {
                    if (Vector2.Distance(garlic.garlic.transform.position, target.transform.position) <= 1.91f)
                    {
                        targetNearGarlic = true;
                    }
                }
            }
            Vampire.targetNearGarlic = targetNearGarlic;
            Vampire.currentTarget = target;
            setPlayerOutline(Vampire.currentTarget, Vampire.color);
        }

        static void jackalSetTarget()
        {
            if (Jackal.jackal == null || Jackal.jackal != CachedPlayer.LocalPlayer.PlayerControl) return;
            var untargetablePlayers = new List<PlayerControl>();
            if (Jackal.canCreateSidekickFromImpostor)
            {
                // Only exclude sidekick from beeing targeted if the jackal can create sidekicks from impostors
                if (Sidekick.sidekick != null) untargetablePlayers.Add(Sidekick.sidekick);
            }
            foreach (var mini in Mini.players)
            {
                if (!Mini.isGrownUp(mini.player))
                {
                    untargetablePlayers.Add(mini.player);
                }
            }
            Jackal.currentTarget = setTarget(untargetablePlayers: untargetablePlayers);
            setPlayerOutline(Jackal.currentTarget, Palette.ImpostorRed);
        }

        static void sidekickSetTarget()
        {
            if (Sidekick.sidekick == null || Sidekick.sidekick != CachedPlayer.LocalPlayer.PlayerControl) return;
            var untargetablePlayers = new List<PlayerControl>();
            if (Jackal.jackal != null) untargetablePlayers.Add(Jackal.jackal);
            foreach (var mini in Mini.players)
            {
                if (!Mini.isGrownUp(mini.player))
                {
                    untargetablePlayers.Add(mini.player);
                }
            }
            Sidekick.currentTarget = setTarget(untargetablePlayers: untargetablePlayers);
            if (Sidekick.canKill) setPlayerOutline(Sidekick.currentTarget, Palette.ImpostorRed);
        }

        static void sidekickCheckPromotion()
        {
            // If LocalPlayer is Sidekick, the Jackal is disconnected and Sidekick promotion is enabled, then trigger promotion
            if (Sidekick.promotesToJackal &&
                CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Sidekick) &&
                CachedPlayer.LocalPlayer.PlayerControl.isAlive() &&
                (Jackal.jackal == null || Jackal.jackal.Data.Disconnected))
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.SidekickPromotes, Hazel.SendOption.Reliable, -1);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                RPCProcedure.sidekickPromotes();
            }
        }

        static void eraserSetTarget()
        {
            if (Eraser.eraser == null || Eraser.eraser != CachedPlayer.LocalPlayer.PlayerControl) return;

            List<PlayerControl> untargetables = new();
            if (Spy.spy != null) untargetables.Add(Spy.spy);
            if (Sidekick.wasTeamRed) untargetables.Add(Sidekick.sidekick);
            if (Jackal.wasTeamRed) untargetables.Add(Jackal.jackal);
            Eraser.currentTarget = setTarget(onlyCrewmates: !Eraser.canEraseAnyone, untargetablePlayers: Eraser.canEraseAnyone ? new List<PlayerControl>() : untargetables);
            setPlayerOutline(Eraser.currentTarget, Eraser.color);
        }

        static void engineerUpdate()
        {
            if (Engineer.engineer == null) return;

            bool jackalHighlight = Engineer.highlightForTeamJackal && (CachedPlayer.LocalPlayer.PlayerControl == Jackal.jackal || CachedPlayer.LocalPlayer.PlayerControl == Sidekick.sidekick);
            bool impostorHighlight = Engineer.highlightForImpostors && CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor;
            if ((jackalHighlight || impostorHighlight) && MapUtilities.CachedShipStatus?.AllVents != null)
            {
                foreach (Vent vent in MapUtilities.CachedShipStatus.AllVents)
                {
                    try
                    {
                        if (vent?.myRend?.material != null)
                        {
                            if (Engineer.engineer.inVent)
                            {
                                vent.myRend.material.SetFloat("_Outline", 1f);
                                vent.myRend.material.SetColor("_OutlineColor", Engineer.color);
                            }
                            else if (vent.myRend.material.GetColor("_AddColor").a == 0f)
                            {
                                vent.myRend.material.SetFloat("_Outline", 0);
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        static void impostorSetTarget()
        {
            if (!CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor || !CachedPlayer.LocalPlayer.PlayerControl.CanMove || CachedPlayer.LocalPlayer.PlayerControl.Data.IsDead)
            { // !isImpostor || !canMove || isDead
                FastDestroyableSingleton<HudManager>.Instance.KillButton.SetTarget(null);
                return;
            }

            PlayerControl target = null;
            if (Spy.spy != null || Sidekick.wasSpy || Jackal.wasSpy)
            {
                if (Spy.impostorsCanKillAnyone)
                {
                    target = setTarget(false, true);
                }
                else
                {
                    var listp = new List<PlayerControl>
                    {
                        Spy.spy
                    };
                    if (Sidekick.wasTeamRed) listp.Add(Sidekick.sidekick);
                    if (Jackal.wasTeamRed) listp.Add(Jackal.jackal);
                    target = setTarget(true, true, listp);
                }
            }
            else
            {
                target = setTarget(true, true);
            }

            FastDestroyableSingleton<HudManager>.Instance.KillButton.SetTarget(target); // Includes setPlayerOutline(target, Palette.ImpstorRed);
        }

        static void warlockSetTarget()
        {
            if (Warlock.warlock == null || Warlock.warlock != CachedPlayer.LocalPlayer.PlayerControl) return;
            if (Warlock.curseVictim != null && (Warlock.curseVictim.Data.Disconnected || Warlock.curseVictim.Data.IsDead))
            {
                // If the cursed victim is disconnected or dead reset the curse so a new curse can be applied
                Warlock.resetCurse();
            }
            if (Warlock.curseVictim == null)
            {
                Warlock.currentTarget = setTarget();
                setPlayerOutline(Warlock.currentTarget, Warlock.color);
            }
            else
            {
                Warlock.curseVictimTarget = setTarget(targetingPlayer: Warlock.curseVictim);
                setPlayerOutline(Warlock.curseVictimTarget, Warlock.color);
            }
        }
        static void assassinUpdate()
        {
            if (Assassin.arrow?.arrow != null)
            {
                if (Assassin.assassin == null || Assassin.assassin != CachedPlayer.LocalPlayer.PlayerControl || !Assassin.knowsTargetLocation)
                {
                    Assassin.arrow.arrow.SetActive(false);
                    return;
                }
                if (Assassin.assassinMarked != null && !CachedPlayer.LocalPlayer.PlayerControl.Data.IsDead)
                {
                    bool trackedOnMap = !Assassin.assassinMarked.Data.IsDead;
                    Vector3 position = Assassin.assassinMarked.transform.position;
                    if (!trackedOnMap)
                    { // Check for dead body
                        DeadBody body = UnityEngine.Object.FindObjectsOfType<DeadBody>().FirstOrDefault(b => b.ParentId == Assassin.assassinMarked.PlayerId);
                        if (body != null)
                        {
                            trackedOnMap = true;
                            position = body.transform.position;
                        }
                    }
                    Assassin.arrow.Update(position);
                    Assassin.arrow.arrow.SetActive(trackedOnMap);
                }
                else
                {
                    Assassin.arrow.arrow.SetActive(false);
                }
            }
        }

        static void trackerUpdate()
        {
            // Handle player tracking
            if (Tracker.arrow?.arrow != null)
            {
                if (Tracker.tracker == null || CachedPlayer.LocalPlayer.PlayerControl != Tracker.tracker)
                {
                    Tracker.arrow.arrow.SetActive(false);
                    return;
                }

                if (Tracker.tracker != null && Tracker.tracked != null && CachedPlayer.LocalPlayer.PlayerControl == Tracker.tracker && !Tracker.tracker.Data.IsDead)
                {
                    Tracker.timeUntilUpdate -= Time.fixedDeltaTime;

                    if (Tracker.timeUntilUpdate <= 0f)
                    {
                        bool trackedOnMap = !Tracker.tracked.Data.IsDead;
                        Vector3 position = Tracker.tracked.transform.position;
                        if (!trackedOnMap)
                        { // Check for dead body
                            DeadBody body = UnityEngine.Object.FindObjectsOfType<DeadBody>().FirstOrDefault(b => b.ParentId == Tracker.tracked.PlayerId);
                            if (body != null)
                            {
                                trackedOnMap = true;
                                position = body.transform.position;
                            }
                        }

                        Tracker.arrow.Update(position);
                        Tracker.arrow.arrow.SetActive(trackedOnMap);
                        Tracker.timeUntilUpdate = Tracker.updateInterval;
                    }
                    else
                    {
                        Tracker.arrow.Update();
                    }
                }
            }

            // Handle corpses tracking
            if (Tracker.tracker != null && Tracker.tracker == CachedPlayer.LocalPlayer.PlayerControl && Tracker.corpsesTrackingTimer >= 0f && !Tracker.tracker.Data.IsDead)
            {
                bool arrowsCountChanged = Tracker.localArrows.Count != Tracker.deadBodyPositions.Count();
                int index = 0;

                if (arrowsCountChanged)
                {
                    foreach (Arrow arrow in Tracker.localArrows) UnityEngine.Object.Destroy(arrow.arrow);
                    Tracker.localArrows = new List<Arrow>();
                }
                foreach (Vector3 position in Tracker.deadBodyPositions)
                {
                    if (arrowsCountChanged)
                    {
                        Tracker.localArrows.Add(new Arrow(Tracker.color));
                        Tracker.localArrows[index].arrow.SetActive(true);
                    }
                    if (Tracker.localArrows[index] != null) Tracker.localArrows[index].Update(position);
                    index++;
                }
            }
            else if (Tracker.localArrows.Count > 0)
            {
                foreach (Arrow arrow in Tracker.localArrows) UnityEngine.Object.Destroy(arrow.arrow);
                Tracker.localArrows = new List<Arrow>();
            }
        }

        public static void playerSizeUpdate(PlayerControl p)
        {
            // Set default player size
            CircleCollider2D collider = p.GetComponent<CircleCollider2D>();

            p.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            collider.radius = Mini.defaultColliderRadius;
            collider.offset = Mini.defaultColliderOffset * Vector2.down;

            // Set adapted player size to Mini and Morphling
            if (Camouflager.camouflageTimer > 0f) return;

            foreach (var mini in Mini.players)
            {
                float growingProgress = mini.growingProgress();
                float scale = growingProgress * 0.35f + 0.35f;
                float correctedColliderRadius = Mini.defaultColliderRadius * 0.7f / scale; // scale / 0.7f is the factor by which we decrease the player size, hence we need to increase the collider size by 0.7f / scale

                if (p.hasModifier(ModifierType.Mini))
                {
                    p.transform.localScale = new Vector3(scale, scale, 1f);
                    collider.radius = correctedColliderRadius;
                }
                if (Morphling.morphling != null && p == Morphling.morphling && Morphling.morphTarget.hasModifier(ModifierType.Mini) && Morphling.morphTimer > 0f)
                {
                    p.transform.localScale = new Vector3(scale, scale, 1f);
                    collider.radius = correctedColliderRadius;
                }
            }
        }

        public static void updatePlayerInfo()
        {
            bool commsActive = false;
            foreach (PlayerTask t in CachedPlayer.LocalPlayer.PlayerControl.myTasks)
            {
                if (t.TaskType == TaskTypes.FixComms)
                {
                    commsActive = true;
                    break;
                }
            }

            var canSeeEverything = CachedPlayer.LocalPlayer.PlayerControl.isDead() || CachedPlayer.LocalPlayer.PlayerControl.isGM();
            foreach (PlayerControl p in CachedPlayer.AllPlayers)
            {
                if (p == null) continue;

                bool isAkujo = Akujo.isPartner(CachedPlayer.LocalPlayer.PlayerControl, p);

                var canSeeInfo =
                    canSeeEverything || isAkujo ||
                    p == CachedPlayer.LocalPlayer.PlayerControl || p.isGM() ||
                    (Lawyer.lawyerKnowsRole && CachedPlayer.LocalPlayer.PlayerControl == Lawyer.lawyer && p == Lawyer.target);

                if (canSeeInfo)
                {
                    Transform playerInfoTransform = p.cosmetics.nameText.transform.parent.FindChild("Info");
                    TMPro.TextMeshPro playerInfo = playerInfoTransform != null ? playerInfoTransform.GetComponent<TMPro.TextMeshPro>() : null;
                    if (playerInfo == null)
                    {
                        playerInfo = UnityEngine.Object.Instantiate(p.cosmetics.nameText, p.cosmetics.nameText.transform.parent);
                        playerInfo.fontSize *= 0.75f;
                        playerInfo.gameObject.name = "Info";
                    }

                    // Set the position every time bc it sometimes ends up in the wrong place due to camoflauge
                    playerInfo.transform.localPosition = p.cosmetics.nameText.transform.localPosition + Vector3.up * 0.5f;

                    PlayerVoteArea playerVoteArea = MeetingHud.Instance?.playerStates?.FirstOrDefault(x => x.TargetPlayerId == p.PlayerId);
                    Transform meetingInfoTransform = playerVoteArea != null ? playerVoteArea.NameText.transform.parent.FindChild("Info") : null;
                    TMPro.TextMeshPro meetingInfo = meetingInfoTransform != null ? meetingInfoTransform.GetComponent<TMPro.TextMeshPro>() : null;
                    if (meetingInfo == null && playerVoteArea != null)
                    {
                        meetingInfo = UnityEngine.Object.Instantiate(playerVoteArea.NameText, playerVoteArea.NameText.transform.parent);
                        meetingInfo.transform.localPosition += Vector3.down * 0.10f;
                        meetingInfo.fontSize *= 0.60f;
                        meetingInfo.gameObject.name = "Info";
                    }

                    // Set player name higher to align in middle
                    if (meetingInfo != null && playerVoteArea != null)
                    {
                        var playerName = playerVoteArea.NameText;
                        playerName.transform.localPosition = new Vector3(0.3384f, 0.0311f + 0.0683f, -0.1f);
                    }

                    var (tasksCompleted, tasksTotal) = TasksHandler.taskInfo(p.Data);
                    string roleNames = RoleInfo.GetRolesString(p, true, new RoleType[] { RoleType.Lovers });
                    string roleNamesFull = RoleInfo.GetRolesString(p, true, new RoleType[] { RoleType.Lovers }, true);


                    var completedStr = commsActive ? "?" : tasksCompleted.ToString();
                    string taskInfo = tasksTotal > 0 ? $"<color=#FAD934FF>({completedStr}/{tasksTotal})</color>" : "";

                    string playerInfoText = "";
                    string meetingInfoText = "";
                    if (p == CachedPlayer.LocalPlayer.PlayerControl)
                    {
                        playerInfoText = $"{roleNames}";
                        if (DestroyableSingleton<TaskPanelBehaviour>.InstanceExists)
                        {
                            TMPro.TextMeshPro tabText = FastDestroyableSingleton<TaskPanelBehaviour>.Instance.tab.transform.FindChild("TabText_TMP").GetComponent<TMPro.TextMeshPro>();
                            tabText.SetText($"{TranslationController.Instance.GetString(StringNames.Tasks)} {taskInfo}");
                        }
                        meetingInfoText = $"{roleNames} {taskInfo}".Trim();
                    }
                    else if (CachedPlayer.LocalPlayer.PlayerControl.isAlive() && isAkujo)
                    {
                        if (Akujo.knowsRoles)
                        {
                            playerInfoText = roleNamesFull;
                            meetingInfoText = roleNamesFull;
                        }
                        else if (p.hasModifier(ModifierType.AkujoHonmei))
                        {
                            playerInfoText = Helpers.cs(Akujo.color, ModTranslation.getString("akujoHonmei"));
                        }
                        else if (p.hasModifier(ModifierType.AkujoKeep))
                        {
                            playerInfoText = Helpers.cs(Akujo.color, ModTranslation.getString("akujoKeep"));
                        }
                    }
                    else if (MapOptions.ghostsSeeRoles && MapOptions.ghostsSeeTasks)
                    {
                        playerInfoText = $"{roleNames} {taskInfo}".Trim();
                        meetingInfoText = playerInfoText;
                    }
                    else if (MapOptions.ghostsSeeTasks)
                    {
                        playerInfoText = $"{taskInfo}".Trim();
                        meetingInfoText = playerInfoText;
                    }
                    else if (MapOptions.ghostsSeeRoles || (Lawyer.lawyerKnowsRole && CachedPlayer.LocalPlayer.PlayerControl == Lawyer.lawyer && p == Lawyer.target))
                    {
                        playerInfoText = $"{roleNames}";
                        meetingInfoText = playerInfoText;
                    }
                    else if (p.isGM() || CachedPlayer.LocalPlayer.PlayerControl.isGM())
                    {
                        playerInfoText = $"{roleNames} {taskInfo}".Trim();
                        meetingInfoText = playerInfoText;
                    }

                    playerInfo.text = playerInfoText;
                    playerInfo.gameObject.SetActive(p.Visible && !Helpers.hidePlayerName(p));
                    if (meetingInfo != null) meetingInfo.text = MeetingHud.Instance.state == MeetingHud.VoteStates.Results ? "" : meetingInfoText;
                }
            }
        }

        public static void securityGuardSetTarget()
        {
            if (SecurityGuard.securityGuard == null || SecurityGuard.securityGuard != CachedPlayer.LocalPlayer.PlayerControl || MapUtilities.CachedShipStatus == null || MapUtilities.CachedShipStatus.AllVents == null) return;

            Vent target = null;
            Vector2 truePosition = CachedPlayer.LocalPlayer.PlayerControl.GetTruePosition();
            float closestDistance = float.MaxValue;
            for (int i = 0; i < MapUtilities.CachedShipStatus.AllVents.Length; i++)
            {
                Vent vent = MapUtilities.CachedShipStatus.AllVents[i];
                if (vent.gameObject.name.StartsWith("JackInTheBoxVent_") || vent.gameObject.name.StartsWith("SealedVent_") || vent.gameObject.name.StartsWith("FutureSealedVent_")) continue;
                float distance = Vector2.Distance(vent.transform.position, truePosition);
                if (distance <= vent.UsableDistance && distance < closestDistance)
                {
                    closestDistance = distance;
                    target = vent;
                }
            }
            SecurityGuard.ventTarget = target;
        }

        public static void securityGuardUpdate()
        {
            if (SecurityGuard.securityGuard == null || CachedPlayer.LocalPlayer.PlayerControl != SecurityGuard.securityGuard || SecurityGuard.securityGuard.Data.IsDead) return;
            var (playerCompleted, _) = TasksHandler.taskInfo(SecurityGuard.securityGuard.Data);
            if (playerCompleted == SecurityGuard.rechargedTasks)
            {
                SecurityGuard.rechargedTasks += SecurityGuard.rechargeTasksNumber;
                if (SecurityGuard.maxCharges > SecurityGuard.charges) SecurityGuard.charges++;
            }
        }

        public static void arsonistSetTarget()
        {
            if (Arsonist.arsonist == null || Arsonist.arsonist != CachedPlayer.LocalPlayer.PlayerControl) return;
            List<PlayerControl> untargetables;
            if (Arsonist.douseTarget != null)
                untargetables = PlayerControl.AllPlayerControls.GetFastEnumerator().ToArray().Where(x => x.PlayerId != Arsonist.douseTarget.PlayerId).ToList();
            else
                untargetables = Arsonist.dousedPlayers;
            Arsonist.currentTarget = setTarget(untargetablePlayers: untargetables);
            if (Arsonist.currentTarget != null) setPlayerOutline(Arsonist.currentTarget, Arsonist.color);
        }

        static void snitchUpdate()
        {
            if (Snitch.localArrows == null) return;

            foreach (Arrow arrow in Snitch.localArrows) arrow.arrow.SetActive(false);

            if (Snitch.snitch == null || Snitch.snitch.Data.IsDead) return;

            var (playerCompleted, playerTotal) = TasksHandler.taskInfo(Snitch.snitch.Data);
            int numberOfTasks = playerTotal - playerCompleted;

            if (numberOfTasks <= Snitch.taskCountForReveal && (CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor || (Snitch.includeTeamJackal && (CachedPlayer.LocalPlayer.PlayerControl == Jackal.jackal || CachedPlayer.LocalPlayer.PlayerControl == Sidekick.sidekick))))
            {
                if (Snitch.localArrows.Count == 0) Snitch.localArrows.Add(new Arrow(Color.blue));
                if (Snitch.localArrows.Count != 0 && Snitch.localArrows[0] != null)
                {
                    Snitch.localArrows[0].arrow.SetActive(true);
                    Snitch.localArrows[0].image.color = Color.blue;
                    Snitch.localArrows[0].Update(Snitch.snitch.transform.position);
                }
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl == Snitch.snitch && numberOfTasks == 0)
            {
                int arrowIndex = 0;
                foreach (PlayerControl p in CachedPlayer.AllPlayers)
                {
                    bool arrowForImp = p.Data.Role.IsImpostor;
                    bool arrowForTeamJackal = Snitch.includeTeamJackal && (p == Jackal.jackal || p == Sidekick.sidekick);
                    bool arrowForFox = p.isRole(RoleType.Fox) || p.isRole(RoleType.Immoralist);

                    // Update the arrows' color every time bc things go weird when you add a sidekick or someone dies
                    Color c = Palette.ImpostorRed;
                    if (arrowForTeamJackal)
                    {
                        c = Jackal.color;
                    }
                    else if (arrowForFox)
                    {
                        c = Fox.color;
                    }
                    if (!p.Data.IsDead && (arrowForImp || arrowForTeamJackal || arrowForFox))
                    {
                        if (arrowIndex >= Snitch.localArrows.Count)
                        {
                            Snitch.localArrows.Add(new Arrow(c));
                        }
                        if (arrowIndex < Snitch.localArrows.Count && Snitch.localArrows[arrowIndex] != null)
                        {
                            Snitch.localArrows[arrowIndex].image.color = c;
                            Snitch.localArrows[arrowIndex].arrow.SetActive(true);
                            Snitch.localArrows[arrowIndex].Update(p.transform.position, c);
                        }
                        arrowIndex++;
                    }
                }
            }
        }

        static void bountyHunterUpdate()
        {
            if (BountyHunter.bountyHunter == null || CachedPlayer.LocalPlayer.PlayerControl != BountyHunter.bountyHunter) return;

            if (BountyHunter.bountyHunter.Data.IsDead)
            {
                if (BountyHunter.arrow != null || BountyHunter.arrow.arrow != null) UnityEngine.Object.Destroy(BountyHunter.arrow.arrow);
                BountyHunter.arrow = null;
                if (BountyHunter.cooldownText != null && BountyHunter.cooldownText.gameObject != null) UnityEngine.Object.Destroy(BountyHunter.cooldownText.gameObject);
                BountyHunter.cooldownText = null;
                BountyHunter.bounty = null;
                foreach (PoolablePlayer p in MapOptions.playerIcons.Values)
                {
                    if (p != null && p.gameObject != null) p.gameObject.SetActive(false);
                }
                return;
            }

            BountyHunter.arrowUpdateTimer -= Time.fixedDeltaTime;
            BountyHunter.bountyUpdateTimer -= Time.fixedDeltaTime;

            if (BountyHunter.bounty == null || BountyHunter.bountyUpdateTimer <= 0f)
            {
                // Set new bounty
                BountyHunter.bounty = null;
                BountyHunter.arrowUpdateTimer = 0f; // Force arrow to update
                BountyHunter.bountyUpdateTimer = BountyHunter.bountyDuration;
                var possibleTargets = new List<PlayerControl>();
                foreach (PlayerControl p in CachedPlayer.AllPlayers)
                {
                    if (!p.Data.IsDead && !p.Data.Disconnected && !p.Data.Role.IsImpostor && p != Spy.spy
                    && (p != Sidekick.sidekick || !Sidekick.wasTeamRed)
                    && (p != Jackal.jackal || !Jackal.wasTeamRed)
                    && !(p.hasModifier(ModifierType.Mini) && !Mini.isGrownUp(p))
                    && !p.isGM()
                    && BountyHunter.bountyHunter.getPartner() != p)
                    {
                        possibleTargets.Add(p);
                    }
                }
                BountyHunter.bounty = possibleTargets[TheOtherRoles.rnd.Next(0, possibleTargets.Count)];
                if (BountyHunter.bounty == null) return;

                // Show poolable player
                if (FastDestroyableSingleton<HudManager>.Instance != null && FastDestroyableSingleton<HudManager>.Instance.UseButton != null)
                {
                    foreach (PoolablePlayer pp in MapOptions.playerIcons.Values) pp.gameObject.SetActive(false);
                    if (MapOptions.playerIcons.ContainsKey(BountyHunter.bounty.PlayerId) && MapOptions.playerIcons[BountyHunter.bounty.PlayerId].gameObject != null)
                        MapOptions.playerIcons[BountyHunter.bounty.PlayerId].gameObject.SetActive(true);
                }
            }

            // Update Cooldown Text
            if (BountyHunter.cooldownText != null)
            {
                BountyHunter.cooldownText.text = Mathf.CeilToInt(Mathf.Clamp(BountyHunter.bountyUpdateTimer, 0, BountyHunter.bountyDuration)).ToString();
            }

            // Update Arrow
            if (BountyHunter.showArrow && BountyHunter.bounty != null)
            {
                if (BountyHunter.arrow == null) BountyHunter.arrow = new Arrow(Color.red);
                if (BountyHunter.arrowUpdateTimer <= 0f)
                {
                    BountyHunter.arrow.Update(BountyHunter.bounty.transform.position);
                    BountyHunter.arrowUpdateTimer = BountyHunter.arrowUpdateInterval;
                }
                BountyHunter.arrow.Update();
            }
        }
        static void assassinSetTarget()
        {
            if (Assassin.assassin == null || Assassin.assassin != CachedPlayer.LocalPlayer.PlayerControl) return;
            List<PlayerControl> untargetables = new();
            if (Spy.spy != null && !Spy.impostorsCanKillAnyone) untargetables.Add(Spy.spy);
            foreach (var mini in Mini.players)
            {
                untargetables.Add(mini.player);
            }
            if (Sidekick.wasTeamRed && !Spy.impostorsCanKillAnyone) untargetables.Add(Sidekick.sidekick);
            if (Jackal.wasTeamRed && !Spy.impostorsCanKillAnyone) untargetables.Add(Jackal.jackal);
            Assassin.currentTarget = setTarget(onlyCrewmates: true, untargetablePlayers: untargetables);
            setPlayerOutline(Assassin.currentTarget, Assassin.color);
        }

        static void baitUpdate()
        {
            if (Bait.bait == null || Bait.bait != CachedPlayer.LocalPlayer.PlayerControl) return;

            // Bait report
            if (Bait.bait.Data.IsDead && !Bait.reported)
            {
                Bait.reportDelay -= Time.fixedDeltaTime;
                DeadPlayer deadPlayer = deadPlayers?.Where(x => x.player?.PlayerId == Bait.bait.PlayerId)?.FirstOrDefault();
                if (deadPlayer.killerIfExisting != null && Bait.reportDelay <= 0f)
                {
                    Helpers.handleVampireBiteOnBodyReport(); // Manually call Vampire handling, since the CmdReportDeadBody Prefix won't be called

                    byte reporter = deadPlayer.killerIfExisting.PlayerId;
                    if (Bait.bait.hasModifier(ModifierType.Madmate))
                    {
                        var candidates = PlayerControl.AllPlayerControls.GetFastEnumerator().ToArray().Where(x => x.isAlive() && !x.isImpostor() && !x.isDummy).ToList();
                        int i = rnd.Next(0, candidates.Count);
                        reporter = candidates.Count > 0 ? candidates[i].PlayerId : deadPlayer.killerIfExisting.PlayerId;
                    }

                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.UncheckedCmdReportDeadBody, Hazel.SendOption.Reliable, -1);
                    writer.Write(reporter);
                    writer.Write(Bait.bait.PlayerId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    RPCProcedure.uncheckedCmdReportDeadBody(reporter, Bait.bait.PlayerId);
                    Bait.reported = true;
                }
            }

            // Bait Vents
            if (MapUtilities.CachedShipStatus?.AllVents != null)
            {
                var ventsWithPlayers = new List<int>();
                foreach (PlayerControl player in CachedPlayer.AllPlayers)
                {
                    if (player == null) continue;

                    if (player.inVent)
                    {
                        Vent target = MapUtilities.CachedShipStatus.AllVents.OrderBy(x => Vector2.Distance(x.transform.position, player.GetTruePosition())).FirstOrDefault();
                        if (target != null) ventsWithPlayers.Add(target.Id);
                    }
                }

                foreach (Vent vent in MapUtilities.CachedShipStatus.AllVents)
                {
                    if (vent.myRend == null || vent.myRend.material == null) continue;
                    if (ventsWithPlayers.Contains(vent.Id) || (ventsWithPlayers.Count > 0 && Bait.highlightAllVents))
                    {
                        vent.myRend.material.SetFloat("_Outline", 1f);
                        vent.myRend.material.SetColor("_OutlineColor", Color.yellow);
                    }
                    else
                    {
                        vent.myRend.material.SetFloat("_Outline", 0);
                    }
                }
            }
        }

        static void vultureUpdate()
        {
            if (Vulture.vulture == null || CachedPlayer.LocalPlayer.PlayerControl != Vulture.vulture || Vulture.localArrows == null || !Vulture.showArrows) return;
            if (Vulture.vulture.Data.IsDead)
            {
                foreach (Arrow arrow in Vulture.localArrows) UnityEngine.Object.Destroy(arrow.arrow);
                Vulture.localArrows = new List<Arrow>();
                return;
            }

            DeadBody[] deadBodies = UnityEngine.Object.FindObjectsOfType<DeadBody>();
            bool arrowUpdate = Vulture.localArrows.Count != deadBodies.Count();
            int index = 0;

            if (arrowUpdate)
            {
                foreach (Arrow arrow in Vulture.localArrows) UnityEngine.Object.Destroy(arrow.arrow);
                Vulture.localArrows = new List<Arrow>();
            }

            foreach (DeadBody db in deadBodies)
            {
                if (arrowUpdate)
                {
                    Vulture.localArrows.Add(new Arrow(Color.blue));
                    Vulture.localArrows[index].arrow.SetActive(true);
                }
                if (Vulture.localArrows[index] != null) Vulture.localArrows[index].Update(db.transform.position);
                index++;
            }
        }

        public static void mediumSetTarget()
        {
            if (Medium.medium == null || Medium.medium != CachedPlayer.LocalPlayer.PlayerControl || Medium.medium.Data.IsDead || Medium.deadBodies == null || MapUtilities.CachedShipStatus?.AllVents == null) return;

            DeadPlayer target = null;
            Vector2 truePosition = CachedPlayer.LocalPlayer.PlayerControl.GetTruePosition();
            float closestDistance = float.MaxValue;
            float usableDistance = MapUtilities.CachedShipStatus.AllVents.FirstOrDefault().UsableDistance;
            foreach ((DeadPlayer dp, Vector3 ps) in Medium.deadBodies)
            {
                float distance = Vector2.Distance(ps, truePosition);
                if (distance <= usableDistance && distance < closestDistance)
                {
                    closestDistance = distance;
                    target = dp;
                }
            }
            Medium.target = target;
        }


        static void gmUpdate()
        {
            if (GM.gm == null || GM.gm != CachedPlayer.LocalPlayer.PlayerControl) return;

            bool showIcon = (GM.canWarp || GM.canKill) && MeetingHud.Instance == null;

            foreach (byte playerID in MapOptions.playerIcons.Keys)
            {
                PlayerControl pc = Helpers.playerById(playerID);
                PoolablePlayer pp = MapOptions.playerIcons[playerID];
                if (pc.Data.Disconnected)
                {
                    pp.gameObject.SetActive(false);
                    continue;
                }

                pp.gameObject.SetActive(showIcon);
                if (pc.Data.IsDead)
                {
                    pp.setSemiTransparent(true);
                }
                else
                {
                    pp.setSemiTransparent(false);
                }
            }

            if (TaskPanelBehaviour.InstanceExists)
            {
                TaskPanelBehaviour.Instance.enabled = false;
                TaskPanelBehaviour.Instance.background.enabled = false;
                TaskPanelBehaviour.Instance.tab.enabled = false;
                TaskPanelBehaviour.Instance.TaskText.enabled = false;
                TaskPanelBehaviour.Instance.tab.transform.FindChild("TabText_TMP").GetComponent<TMPro.TextMeshPro>().SetText("");
                //TaskPanelBehaviour.Instance.transform.localPosition = Vector3.negativeInfinityVector;
            }

        }

        public static void lawyerUpdate()
        {
            if (Lawyer.lawyer == null || Lawyer.lawyer != CachedPlayer.LocalPlayer.PlayerControl) return;

            // Meeting win
            if (Lawyer.winsAfterMeetings && Lawyer.neededMeetings == Lawyer.meetings && Lawyer.target != null && !Lawyer.target.Data.IsDead)
            {
                Lawyer.winsAfterMeetings = false; // Avoid sending mutliple RPCs until the host finshes the game
                MessageWriter winWriter = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.LawyerWin, Hazel.SendOption.Reliable, -1);
                AmongUsClient.Instance.FinishRpcImmediately(winWriter);
                RPCProcedure.lawyerWin();
                return;
            }

            // Promote to Pursuer
            if (Lawyer.target != null && Lawyer.target.Data.Disconnected)
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.LawyerPromotesToPursuer, Hazel.SendOption.Reliable, -1);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                RPCProcedure.lawyerPromotesToPursuer();
                return;
            }
        }

        public static void hackerUpdate()
        {
            if (Hacker.hacker == null || CachedPlayer.LocalPlayer.PlayerControl != Hacker.hacker || Hacker.hacker.Data.IsDead) return;
            var (playerCompleted, _) = TasksHandler.taskInfo(Hacker.hacker.Data);
            if (playerCompleted == Hacker.rechargedTasks)
            {
                Hacker.rechargedTasks += Hacker.rechargeTasksNumber;
                if (Hacker.toolsNumber > Hacker.chargesVitals) Hacker.chargesVitals++;
                if (Hacker.toolsNumber > Hacker.chargesAdminTable) Hacker.chargesAdminTable++;
            }
        }

        static void pursuerSetTarget()
        {
            if (Pursuer.pursuer == null || Pursuer.pursuer != CachedPlayer.LocalPlayer.PlayerControl) return;
            Pursuer.target = setTarget();
            setPlayerOutline(Pursuer.target, Pursuer.color);
        }

        static void witchSetTarget()
        {
            if (Witch.witch == null || Witch.witch != CachedPlayer.LocalPlayer.PlayerControl) return;
            List<PlayerControl> untargetables;
            if (Witch.spellCastingTarget != null)
                untargetables = PlayerControl.AllPlayerControls.GetFastEnumerator().ToArray().Where(x => x.PlayerId != Witch.spellCastingTarget.PlayerId).ToList(); // Don't switch the target from the the one you're currently casting a spell on
            else
            {
                untargetables = new List<PlayerControl>(); // Also target players that have already been spelled, to hide spells that were blanks/blocked by shields
                if (Spy.spy != null && !Witch.canSpellAnyone) untargetables.Add(Spy.spy);
                if (Sidekick.wasTeamRed && !Witch.canSpellAnyone) untargetables.Add(Sidekick.sidekick);
                if (Jackal.wasTeamRed && !Witch.canSpellAnyone) untargetables.Add(Jackal.jackal);
            }
            Witch.currentTarget = setTarget(onlyCrewmates: !Witch.canSpellAnyone, untargetablePlayers: untargetables);
            setPlayerOutline(Witch.currentTarget, Witch.color);
        }

        private static void StopCooldown(PlayerControl __instance)
        {
            if (CustomOptionHolder.exceptOnTask.getBool())
            {
                if (Patches.ElectricPatch.isOntask())
                {
                    __instance.SetKillTimer(__instance.killTimer + Time.fixedDeltaTime);
                }
            }
        }

        public static void Postfix(PlayerControl __instance)
        {
            if (lastVisible.TryGetValue(__instance.PlayerId, out bool visible) && __instance.Visible != visible)
                Logger.info($"Visible Status Change to {__instance.Visible} for {__instance.getNameWithRole()}", "BodySprite");
            lastVisible[__instance.PlayerId] = __instance.Visible;
            if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started) return;

            // Mini and Morphling shrink
            playerSizeUpdate(__instance);

            if (CachedPlayer.LocalPlayer.PlayerControl == __instance)
            {
                // Update player outlines
                setBasePlayerOutlines();

                // Update Role Description
                Helpers.refreshRoleDescription(__instance);

                // Update Player Info
                updatePlayerInfo();

                // Time Master
                bendTimeUpdate();
                // Morphling
                morphlingSetTarget();
                // Medic
                medicSetTarget();
                // Shifter
                shifterUpdate();
                // Detective
                detectiveUpdateFootPrints();
                // Tracker
                trackerSetTarget();
                // Impostor
                impostorSetTarget();
                // Vampire
                vampireSetTarget();
                Garlic.UpdateAll();
                // Eraser
                eraserSetTarget();
                // Engineer
                engineerUpdate();
                // Tracker
                trackerUpdate();
                // EvilHacker
                evilHackerSetTarget();
                // Jackal
                jackalSetTarget();
                // Sidekick
                sidekickSetTarget();
                // Warlock
                warlockSetTarget();
                // Check for sidekick promotion on Jackal disconnect
                sidekickCheckPromotion();
                // SecurityGuard
                securityGuardSetTarget();
                securityGuardUpdate();
                // Arsonist
                arsonistSetTarget();
                // Snitch
                snitchUpdate();
                // BountyHunter
                bountyHunterUpdate();
                // Bait
                baitUpdate();
                // GM
                gmUpdate();
                // Vulture
                vultureUpdate();
                // Medium
                mediumSetTarget();
                // Lawyer
                lawyerUpdate();
                // Pursuer
                pursuerSetTarget();
                // Witch
                witchSetTarget();
                // Assassin
                assassinSetTarget();
                AssassinTrace.UpdateAll();
                assassinUpdate();

                hackerUpdate();
                // Bomber
                BombEffect.UpdateAll();

                SoulPlayer.FixedUpdate();
            }

            TheOtherRolesGM.FixedUpdate(__instance);
            StopCooldown(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.WalkPlayerTo))]
    class PlayerPhysicsWalkPlayerToPatch
    {
        private static Vector2 offset = Vector2.zero;
        public static void Prefix(PlayerPhysics __instance)
        {
            bool correctOffset = Camouflager.camouflageTimer <= 0f && (__instance.myPlayer.hasModifier(ModifierType.Mini) || (Morphling.morphling != null && __instance.myPlayer == Morphling.morphling && Morphling.morphTarget.hasModifier(ModifierType.Mini) && Morphling.morphTimer > 0f));
            if (correctOffset)
            {
                Mini mini = Mini.players.First(x => x.player == __instance.myPlayer);
                if (mini == null) return;
                float currentScaling = (mini.growingProgress() + 1) * 0.5f;
                __instance.myPlayer.Collider.offset = currentScaling * Mini.defaultColliderOffset * Vector2.down;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
    class PlayerControlCmdReportDeadBodyPatch
    {
        public static bool Prefix(PlayerControl __instance)
        {
            Helpers.handleVampireBiteOnBodyReport();

            if (__instance.isGM())
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(CachedPlayer.LocalPlayer.PlayerControl.CmdReportDeadBody))]
    class BodyReportPatch
    {
        static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] GameData.PlayerInfo target)
        {
            if(Moriarty.brainwashed.FindAll(x => x.PlayerId == __instance.PlayerId).Count > 0)
            {
                return false;
            }
            return true;
        }
        static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] GameData.PlayerInfo target)
        {
            Logger.info($"{__instance.getNameWithRole()} => {target?.getNameWithRole() ?? "null"}", "ReportDeadBody");
            // Medic or Detective report
            bool isMedicReport = Medic.medic != null && Medic.medic == CachedPlayer.LocalPlayer.PlayerControl && __instance.PlayerId == Medic.medic.PlayerId;
            bool isDetectiveReport = Detective.detective != null && Detective.detective == CachedPlayer.LocalPlayer.PlayerControl && __instance.PlayerId == Detective.detective.PlayerId;
            if (isMedicReport || isDetectiveReport)
            {
                DeadPlayer deadPlayer = deadPlayers?.Where(x => x.player?.PlayerId == target?.PlayerId)?.FirstOrDefault();

                if (deadPlayer != null && deadPlayer.killerIfExisting != null)
                {
                    float timeSinceDeath = (float)(DateTime.UtcNow - deadPlayer.timeOfDeath).TotalMilliseconds;
                    string msg = "";

                    if (isMedicReport)
                    {
                        msg = String.Format(ModTranslation.getString("medicReport"), Math.Round(timeSinceDeath / 1000));
                    }
                    else if (isDetectiveReport)
                    {
                        if (timeSinceDeath < Detective.reportNameDuration * 1000)
                        {
                            msg = String.Format(ModTranslation.getString("detectiveReportName"), deadPlayer.killerIfExisting.Data.PlayerName);
                        }
                        else if (timeSinceDeath < Detective.reportColorDuration * 1000)
                        {
                            var typeOfColor = Helpers.isLighterColor(deadPlayer.killerIfExisting.Data.DefaultOutfit.ColorId) ?
                                ModTranslation.getString("detectiveColorLight") :
                                ModTranslation.getString("detectiveColorDark");
                            msg = String.Format(ModTranslation.getString("detectiveReportColor"), typeOfColor);
                        }
                        else
                        {
                            msg = ModTranslation.getString("detectiveReportNone");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        if (AmongUsClient.Instance.AmClient && FastDestroyableSingleton<HudManager>.Instance)
                        {
                            FastDestroyableSingleton<HudManager>.Instance.Chat.AddChat(CachedPlayer.LocalPlayer.PlayerControl, msg);
                        }
                        if (msg.IndexOf("who", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            FastDestroyableSingleton<Assets.CoreScripts.Telemetry>.Instance.SendWho();
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    public static class MurderPlayerPatch
    {
        public static bool resetToCrewmate = false;
        public static bool resetToDead = false;

        public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            // Allow everyone to murder players
            resetToCrewmate = !__instance.Data.Role.IsImpostor;
            resetToDead = __instance.Data.IsDead;
            __instance.Data.Role.TeamType = RoleTeamTypes.Impostor;
            __instance.Data.IsDead = false;

            if (Morphling.morphling != null && target == Morphling.morphling)
            {
                Morphling.resetMorph();
            }

            target.resetMorph();
        }

        public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            Logger.info($"{__instance.getNameWithRole()} => {target.getNameWithRole()}", "MurderPlayer");
            // Collect dead player info
            DeadPlayer deadPlayer = new(target, DateTime.UtcNow, DeathReason.Kill, __instance);
            GameHistory.deadPlayers.Add(deadPlayer);

            // Reset killer to crewmate if resetToCrewmate
            if (resetToCrewmate) __instance.Data.Role.TeamType = RoleTeamTypes.Crewmate;
            if (resetToDead) __instance.Data.IsDead = true;

            // Remove fake tasks when player dies
            if (target.hasFakeTasks())
                target.clearAllTasks();

            // Sidekick promotion trigger on murder
            if (Sidekick.promotesToJackal && Sidekick.sidekick != null && !Sidekick.sidekick.Data.IsDead && target == Jackal.jackal && Jackal.jackal == CachedPlayer.LocalPlayer.PlayerControl)
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.SidekickPromotes, Hazel.SendOption.Reliable, -1);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                RPCProcedure.sidekickPromotes();
            }

            // Pursuer promotion trigger on murder (the host sends the call such that everyone receives the update before a possible game End)
            if (target == Lawyer.target && AmongUsClient.Instance.AmHost)
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.LawyerPromotesToPursuer, Hazel.SendOption.Reliable, -1);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                RPCProcedure.lawyerPromotesToPursuer();
            }

            // Cleaner Button Sync
            if (Cleaner.cleaner != null && CachedPlayer.LocalPlayer.PlayerControl == Cleaner.cleaner && __instance == Cleaner.cleaner && HudManagerStartPatch.cleanerCleanButton != null)
                HudManagerStartPatch.cleanerCleanButton.Timer = Cleaner.cleaner.killTimer;


            // Witch Button Sync
            if (Witch.triggerBothCooldowns && Witch.witch != null && CachedPlayer.LocalPlayer.PlayerControl == Witch.witch && __instance == Witch.witch && HudManagerStartPatch.witchSpellButton != null)
                HudManagerStartPatch.witchSpellButton.Timer = HudManagerStartPatch.witchSpellButton.MaxTimer;

            // Warlock Button Sync
            if (Warlock.warlock != null && CachedPlayer.LocalPlayer.PlayerControl == Warlock.warlock && __instance == Warlock.warlock && HudManagerStartPatch.warlockCurseButton != null)
            {
                if (Warlock.warlock.killTimer > HudManagerStartPatch.warlockCurseButton.Timer)
                {
                    HudManagerStartPatch.warlockCurseButton.Timer = Warlock.warlock.killTimer;
                }
            }

            // Assassin Button Sync
            if (Assassin.assassin != null && CachedPlayer.LocalPlayer.PlayerControl == Assassin.assassin && __instance == Assassin.assassin && HudManagerStartPatch.assassinButton != null)
                HudManagerStartPatch.assassinButton.Timer = HudManagerStartPatch.assassinButton.MaxTimer;

            // Seer show flash and add dead player position
            if (Seer.seer != null && CachedPlayer.LocalPlayer.PlayerControl == Seer.seer && !Seer.seer.Data.IsDead && Seer.seer != target && Seer.mode <= 1)
            {
                Helpers.showFlash(new Color(42f / 255f, 187f / 255f, 245f / 255f));
            }
            if (Seer.deadBodyPositions != null) Seer.deadBodyPositions.Add(target.transform.position);

            // Tracker store body positions
            if (Tracker.deadBodyPositions != null) Tracker.deadBodyPositions.Add(target.transform.position);

            // Medium add body
            if (Medium.deadBodies != null)
            {
                Medium.featureDeadBodies.Add(new Tuple<DeadPlayer, Vector3>(deadPlayer, target.transform.position));
            }

            // Mini set adapted kill cooldown
            if (CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.Mini) && CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor && CachedPlayer.LocalPlayer.PlayerControl == __instance)
            {
                var multiplier = Mini.isGrownUp(CachedPlayer.LocalPlayer.PlayerControl) ? 0.66f : 2f;
                CachedPlayer.LocalPlayer.PlayerControl.SetKillTimer(PlayerControl.GameOptions.KillCooldown * multiplier);
            }

            // Set bountyHunter cooldown
            if (BountyHunter.bountyHunter != null && CachedPlayer.LocalPlayer.PlayerControl == BountyHunter.bountyHunter && __instance == BountyHunter.bountyHunter)
            {
                if (target == BountyHunter.bounty)
                {
                    BountyHunter.bountyHunter.SetKillTimer(BountyHunter.bountyKillCooldown);
                    BountyHunter.bountyUpdateTimer = 0f; // Force bounty update
                }
                else
                    BountyHunter.bountyHunter.SetKillTimer(PlayerControl.GameOptions.KillCooldown + BountyHunter.punishmentTime);
            }

            // Update arsonist status
            Arsonist.updateStatus();

            // Show flash on bait kill to the killer if enabled
            if (Bait.bait != null && target == Bait.bait && Bait.showKillFlash && __instance != Bait.bait && __instance == CachedPlayer.LocalPlayer.PlayerControl)
            {
                Helpers.showFlash(new Color(42f / 255f, 187f / 255f, 245f / 255f));
            }

            // impostor promote to last impostor
            if (target.isImpostor() && AmongUsClient.Instance.AmHost)
            {
                LastImpostor.promoteToLastImpostor();
            }

            // 人形使いのダミー死亡処理
            if (target == Puppeteer.dummy)
            {
                // 蘇生する
                target.Revive();
                // 死体を消す
                DeadBody[] array = UnityEngine.Object.FindObjectsOfType<DeadBody>();
                for (int i = 0; i < array.Length; i++)
                {
                    if (GameData.Instance.GetPlayerById(array[i].ParentId).PlayerId == target.PlayerId)
                    {
                        array[i].gameObject.active = false;
                    }
                }
                Puppeteer.OnDummyDeath(__instance);
            }

            __instance.OnKill(target);
            Sherlock.recordKillLog(__instance, target);
            target.OnDeath(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetKillTimer))]
    static class PlayerControlSetCoolDownPatch
    {
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] float time)
        {
            if (PlayerControl.GameOptions.KillCooldown <= 0f) return false;
            float multiplier = 1f;
            float addition = 0f;
            if (CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.Mini) && CachedPlayer.LocalPlayer.PlayerControl.isImpostor()) multiplier = Mini.isGrownUp(CachedPlayer.LocalPlayer.PlayerControl) ? 0.66f : 2f;
            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.BountyHunter)) addition = BountyHunter.punishmentTime;
            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Ninja) && Ninja.isPenalized(CachedPlayer.LocalPlayer.PlayerControl)) addition = Ninja.killPenalty;

            float max = Mathf.Max(PlayerControl.GameOptions.KillCooldown * multiplier + addition, __instance.killTimer);
            __instance.SetKillTimerUnchecked(Mathf.Clamp(time, 0f, max), max);
            return false;
        }

        public static void SetKillTimerUnchecked(this PlayerControl player, float time, float max = float.NegativeInfinity)
        {
            if (max == float.NegativeInfinity) max = time;

            player.killTimer = time;
            FastDestroyableSingleton<HudManager>.Instance.KillButton.SetCoolDown(time, max);
        }
    }

    [HarmonyPatch(typeof(KillAnimation), nameof(KillAnimation.CoPerformKill))]
    class KillAnimationCoPerformKillPatch
    {
        public static bool hideNextAnimation = false;

        public static void Prefix(KillAnimation __instance, [HarmonyArgument(0)] ref PlayerControl source, [HarmonyArgument(1)] ref PlayerControl target)
        {
            if (hideNextAnimation)
                source = target;
            hideNextAnimation = false;
        }
    }

    [HarmonyPatch(typeof(KillAnimation), nameof(KillAnimation.SetMovement))]
    class KillAnimationSetMovementPatch
    {
        private static int? colorId = null;
        public static void Prefix(PlayerControl source, bool canMove)
        {
            Color color = source.cosmetics.currentBodySprite.BodySprite.material.GetColor("_BodyColor");
            if (color != null && Morphling.morphling != null && source.Data.PlayerId == Morphling.morphling.PlayerId)
            {
                var index = Palette.PlayerColors.IndexOf(color);
                if (index != -1) colorId = index;
            }
        }

        public static void Postfix(PlayerControl source, bool canMove)
        {
            if (colorId.HasValue) source.RawSetColor(colorId.Value);
            colorId = null;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Exiled))]
    public static class ExilePlayerPatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            // Collect dead player info
            DeadPlayer deadPlayer = new(__instance, DateTime.UtcNow, DeathReason.Exile, null);
            GameHistory.deadPlayers.Add(deadPlayer);

            // Remove fake tasks when player dies
            if (__instance.hasFakeTasks())
                __instance.clearAllTasks();

            __instance.OnDeath(killer: null);

            // Sidekick promotion trigger on exile
            if (Sidekick.promotesToJackal && Sidekick.sidekick != null && !Sidekick.sidekick.Data.IsDead && __instance == Jackal.jackal && Jackal.jackal == CachedPlayer.LocalPlayer.PlayerControl)
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.SidekickPromotes, Hazel.SendOption.Reliable, -1);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                RPCProcedure.sidekickPromotes();
            }

            // Pursuer promotion trigger on exile (the host sends the call such that everyone receives the update before a possible game End)
            if (__instance == Lawyer.target && AmongUsClient.Instance.AmHost)
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.LawyerPromotesToPursuer, Hazel.SendOption.Reliable, -1);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                RPCProcedure.lawyerPromotesToPursuer();
            }

            // impostor promote to last impostor
            if (__instance.isImpostor() && AmongUsClient.Instance.AmHost)
            {
                LastImpostor.promoteToLastImpostor();
            }

        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CanMove), MethodType.Getter)]
    class PlayerControlCanMovePatch
    {
        public static bool Prefix(PlayerControl __instance, ref bool __result)
        {
            __result = __instance.moveable &&
                !Minigame.Instance &&
                (!DestroyableSingleton<HudManager>.InstanceExists || (!FastDestroyableSingleton<HudManager>.Instance.Chat.IsOpen && !FastDestroyableSingleton<HudManager>.Instance.KillOverlay.IsOpen && !FastDestroyableSingleton<HudManager>.Instance.GameMenu.IsOpen)) &&
                (!MapBehaviour.Instance || !MapBehaviour.Instance.IsOpenStopped) &&
                !MeetingHud.Instance &&
                !ExileController.Instance &&
                !IntroCutscene.Instance;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckName))]
    class PlayerControlCheckNamePatch
    {
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] string name)
        {
            if (CustomOptionHolder.uselessOptions.getBool() && CustomOptionHolder.playerNameDupes.getBool())
            {
                __instance.RpcSetName(name);
                GameData.Instance.UpdateName(__instance.PlayerId, name, false);
                return false;
            }

            return true;
        }
    }
}
