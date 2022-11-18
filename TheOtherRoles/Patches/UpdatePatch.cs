using System.Collections.Generic;
using HarmonyLib;
using TheOtherRoles.Objects;
using UnityEngine;
using static TheOtherRoles.TheOtherRoles;
using static TheOtherRoles.TheOtherRolesGM;
using System.Linq;

namespace TheOtherRoles.Patches
{
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    class HudManagerUpdatePatch
    {
        static void resetNameTagsAndColors()
        {
            Dictionary<byte, PlayerControl> playersById = Helpers.allPlayersById();

            foreach (PlayerControl player in CachedPlayer.AllPlayers)
            {
                player.cosmetics.nameText.text = Helpers.hidePlayerName(CachedPlayer.LocalPlayer.PlayerControl, player) ? "" : player.CurrentOutfit.PlayerName;
                if (CachedPlayer.LocalPlayer.PlayerControl.isImpostor() && player.isImpostor())
                {
                    player.cosmetics.nameText.color = Palette.ImpostorRed;
                }
                else
                {
                    player.cosmetics.nameText.color = Color.white;
                }
            }

            if (MeetingHud.Instance != null)
            {
                foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
                {
                    PlayerControl playerControl = playersById.ContainsKey((byte)player.TargetPlayerId) ? playersById[(byte)player.TargetPlayerId] : null;
                    if (playerControl != null)
                    {
                        player.NameText.text = playerControl.Data.PlayerName;
                        if (CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor && playerControl.Data.Role.IsImpostor)
                        {
                            player.NameText.color = Palette.ImpostorRed;
                        }
                        else
                        {
                            player.NameText.color = Color.white;
                        }
                    }
                }
            }
        }

        static void setPlayerNameColor(PlayerControl p, Color color)
        {
            p.cosmetics.nameText.color = color;
            if (MeetingHud.Instance != null)
                foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
                    if (player.NameText != null && p.PlayerId == player.TargetPlayerId)
                        player.NameText.color = color;
        }

        static void setNameColors()
        {
            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Jester))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Jester.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Mayor))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Mayor.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Engineer))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Engineer.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Sheriff))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Sheriff.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Lighter))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Lighter.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Detective))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Detective.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.TimeMaster))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, TimeMaster.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Medic))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Medic.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Shifter))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Shifter.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Swapper))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Swapper.swapper.Data.Role.IsImpostor ? Palette.ImpostorRed : Swapper.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Seer))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Seer.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Hacker))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Hacker.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Tracker))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Tracker.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Snitch))
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Snitch.color);
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Jackal))
            {
                // Jackal can see his sidekick
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Jackal.color);
                if (Sidekick.sidekick != null)
                {
                    setPlayerNameColor(Sidekick.sidekick, Jackal.color);
                }
                if (Jackal.fakeSidekick != null)
                {
                    setPlayerNameColor(Jackal.fakeSidekick, Jackal.color);
                }
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Spy))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Spy.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.SecurityGuard))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, SecurityGuard.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Arsonist))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Arsonist.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.NiceGuesser))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Guesser.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.EvilGuesser))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Palette.ImpostorRed);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Bait))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Bait.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Opportunist))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Opportunist.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Vulture))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Vulture.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Medium))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Medium.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Lawyer))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Lawyer.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Pursuer))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Pursuer.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.PlagueDoctor))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, PlagueDoctor.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Fox))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Fox.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Immoralist))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Immoralist.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.FortuneTeller) && (FortuneTeller.isCompletedNumTasks(CachedPlayer.LocalPlayer.PlayerControl) || CachedPlayer.LocalPlayer.PlayerControl.Data.IsDead))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, FortuneTeller.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Akujo))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Akujo.color);
            }
            else if (PlayerControl.LocalPlayer.isRole(RoleType.Sherlock))
            {
                setPlayerNameColor(PlayerControl.LocalPlayer, Sherlock.color);
            }
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Cupid) && CachedPlayer.LocalPlayer.PlayerControl.isAlive())
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Cupid.color);
                var cupid = Cupid.allRoles.FirstOrDefault(x => x.player == PlayerControl.LocalPlayer) as Cupid;
                bool meetingShow = MeetingHud.Instance != null &&
                    (MeetingHud.Instance.state == MeetingHud.VoteStates.Voted ||
                    MeetingHud.Instance.state == MeetingHud.VoteStates.NotVoted ||
                    MeetingHud.Instance.state == MeetingHud.VoteStates.Discussion);
                string suffix = Helpers.cs(Cupid.color, " ♥");
                foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
                {
                    if (p == cupid.lovers1 || p == cupid.lovers2)
                    {
                        p.cosmetics.nameText.text += suffix;
                        if (meetingShow)
                        {
                            foreach (PlayerVoteArea pva in MeetingHud.Instance.playerStates)
                            {
                                if (p.PlayerId == pva.TargetPlayerId)
                                {
                                    pva.NameText.text += suffix;
                                }
                            }
                        }
                    }
                }
            }

            if (CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.Madmate))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Madmate.color);

                if (Madmate.knowsImpostors(CachedPlayer.LocalPlayer.PlayerControl))
                {
                    foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
                    {
                        if (p.isImpostor() || p.isRole(RoleType.Spy) || (p.isRole(RoleType.Jackal) && Jackal.wasTeamRed) || (p.isRole(RoleType.Sidekick) && Sidekick.wasTeamRed))
                        {
                            setPlayerNameColor(p, Palette.ImpostorRed);
                        }
                    }
                }
            }

            else if (CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.CreatedMadmate))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Madmate.color);

                if (CreatedMadmate.knowsImpostors(CachedPlayer.LocalPlayer.PlayerControl))
                {
                    foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
                    {
                        if (p.isImpostor() || p.isRole(RoleType.Spy) || (p.isRole(RoleType.Jackal) && Jackal.wasTeamRed) || (p.isRole(RoleType.Sidekick) && Sidekick.wasTeamRed))
                        {
                            setPlayerNameColor(p, Palette.ImpostorRed);
                        }
                    }
                }
            }

            else if (CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.LastImpostor))
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, LastImpostor.color);
            }

            else if (CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.Munou) && CachedPlayer.LocalPlayer.PlayerControl.Data.IsDead)
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, Munou.color);
            }

            else if (CachedPlayer.LocalPlayer.PlayerControl.hasModifier(ModifierType.AntiTeleport) && CachedPlayer.LocalPlayer.PlayerControl.Data.IsDead)
            {
                setPlayerNameColor(CachedPlayer.LocalPlayer.PlayerControl, AntiTeleport.color);
            }

            if (GM.gm != null)
            {
                setPlayerNameColor(GM.gm, GM.color);
            }

            // No else if here, as a Lover of team Jackal needs the colors
            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Sidekick))
            {
                // Sidekick can see the jackal
                setPlayerNameColor(Sidekick.sidekick, Sidekick.color);
                if (Jackal.jackal != null)
                {
                    setPlayerNameColor(Jackal.jackal, Jackal.color);
                }
            }

            // No else if here, as the Impostors need the Spy name to be colored
            if (Spy.spy != null && CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor)
            {
                setPlayerNameColor(Spy.spy, Spy.color);
            }
            if (Sidekick.sidekick != null && Sidekick.wasTeamRed && CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor)
            {
                setPlayerNameColor(Sidekick.sidekick, Spy.color);
            }
            if (Jackal.jackal != null && Jackal.wasTeamRed && CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor)
            {
                setPlayerNameColor(Jackal.jackal, Spy.color);
            }

            if (Immoralist.exists && CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Fox))
            {
                foreach (var immoralist in Immoralist.allPlayers)
                {
                    setPlayerNameColor(immoralist, Immoralist.color);
                }
            }

            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Immoralist))
            {
                foreach (var fox in Fox.allPlayers)
                {
                    setPlayerNameColor(fox, Fox.color);
                }
            }

            PlayerControl player = CachedPlayer.LocalPlayer.PlayerControl;
            bool impostorFlag = player.isRole(RoleType.SchrodingersCat) || player.isImpostor();
            bool jackalFlag = player.isRole(RoleType.SchrodingersCat) || player.isRole(RoleType.Jackal) || player.isRole(RoleType.Sidekick);
            bool jekyllAndHydeFlag = player.isRole(RoleType.SchrodingersCat) || player.isRole(RoleType.JekyllAndHyde);
            bool moriartyFlag = player.isRole(RoleType.SchrodingersCat) || player.isRole(RoleType.Moriarty);
            if (!SchrodingersCat.hasTeam() && SchrodingersCat.hideRole && CachedPlayer.LocalPlayer.PlayerControl.isAlive())
            {
                // 何もしない
            }
            else if (SchrodingersCat.team == SchrodingersCat.Team.Crew)
            {
                foreach (var p in SchrodingersCat.allPlayers)
                {
                    setPlayerNameColor(p, Color.white);
                }
            }
            else if (SchrodingersCat.team == SchrodingersCat.Team.Impostor && impostorFlag)
            {
                foreach (var p in SchrodingersCat.allPlayers)
                {
                    setPlayerNameColor(p, Palette.ImpostorRed);
                }
                if (player.isRole(RoleType.SchrodingersCat))
                {
                    foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
                    {
                        if (p.isImpostor()) setPlayerNameColor(p, Palette.ImpostorRed);
                    }
                }
            }
            else if (SchrodingersCat.team == SchrodingersCat.Team.Jackal && jackalFlag)
            {
                foreach (var p in SchrodingersCat.allPlayers)
                {
                    setPlayerNameColor(p, Jackal.color);
                }
                if (player.isRole(RoleType.SchrodingersCat))
                {
                    setPlayerNameColor(Jackal.jackal, Jackal.color);
                    if (Sidekick.sidekick != null) setPlayerNameColor(Sidekick.sidekick, Sidekick.color);
                }
            }
            else if (SchrodingersCat.team == SchrodingersCat.Team.JekyllAndHyde && jekyllAndHydeFlag)
            {
                foreach (var p in SchrodingersCat.allPlayers)
                {
                    setPlayerNameColor(p, JekyllAndHyde.color);
                }
                if (player.isRole(RoleType.SchrodingersCat))
                {
                    foreach (var p in JekyllAndHyde.allPlayers)
                    {
                        setPlayerNameColor(p, JekyllAndHyde.color);
                    }
                }
            }
            else if (SchrodingersCat.team == SchrodingersCat.Team.Moriarty && moriartyFlag)
            {
                foreach (var p in SchrodingersCat.allPlayers)
                {
                    setPlayerNameColor(p, Moriarty.color);
                }
                if (player.isRole(RoleType.SchrodingersCat))
                {
                    foreach (var p in Moriarty.allPlayers)
                    {
                        setPlayerNameColor(p, Moriarty.color);
                    }
                }
            }
            else if (player.isRole(RoleType.SchrodingersCat))
            {
                setPlayerNameColor(player, SchrodingersCat.color);
            }

            // Crewmate roles with no changes: Mini
            // Impostor roles with no changes: Morphling, Camouflager, Vampire, Godfather, Eraser, Janitor, Cleaner, Warlock, BountyHunter,  Witch and Mafioso

        }

        static void setNameTags()
        {
            // Mafia
            if (CachedPlayer.LocalPlayer.PlayerControl != null && CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor)
            {
                foreach (PlayerControl player in CachedPlayer.AllPlayers)
                {
                    if (player.cosmetics.nameText.text == "") continue;
                    if (Godfather.godfather != null && Godfather.godfather == player)
                        player.cosmetics.nameText.text = player.Data.PlayerName + $" ({ModTranslation.getString("mafiaG")})";
                    else if (Mafioso.mafioso != null && Mafioso.mafioso == player)
                        player.cosmetics.nameText.text = player.Data.PlayerName + $" ({ModTranslation.getString("mafiaM")})";
                    else if (Janitor.janitor != null && Janitor.janitor == player)
                        player.cosmetics.nameText.text = player.Data.PlayerName + $" ({ModTranslation.getString("mafiaJ")})";
                }
                if (MeetingHud.Instance != null)
                    foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
                        if (Godfather.godfather != null && Godfather.godfather.PlayerId == player.TargetPlayerId)
                            player.NameText.text = Godfather.godfather.Data.PlayerName + $" ({ModTranslation.getString("mafiaG")})";
                        else if (Mafioso.mafioso != null && Mafioso.mafioso.PlayerId == player.TargetPlayerId)
                            player.NameText.text = Mafioso.mafioso.Data.PlayerName + $" ({ModTranslation.getString("mafiaM")})";
                        else if (Janitor.janitor != null && Janitor.janitor.PlayerId == player.TargetPlayerId)
                            player.NameText.text = Janitor.janitor.Data.PlayerName + $" ({ModTranslation.getString("mafiaJ")})";
            }

            bool meetingShow = MeetingHud.Instance != null &&
                (MeetingHud.Instance.state == MeetingHud.VoteStates.Voted ||
                 MeetingHud.Instance.state == MeetingHud.VoteStates.NotVoted ||
                 MeetingHud.Instance.state == MeetingHud.VoteStates.Discussion);

            // Lovers
            if (CachedPlayer.LocalPlayer.PlayerControl.isLovers() && CachedPlayer.LocalPlayer.PlayerControl.isAlive())
            {
                string suffix = Lovers.getIcon(CachedPlayer.LocalPlayer.PlayerControl);
                if (Cupid.isCupidLovers(CachedPlayer.LocalPlayer))
                {
                    suffix = Helpers.cs(Cupid.color, " ♥");
                }
                var lover1 = CachedPlayer.LocalPlayer.PlayerControl;
                var lover2 = CachedPlayer.LocalPlayer.PlayerControl.getPartner();

                lover1.cosmetics.nameText.text += suffix;
                if (!Helpers.hidePlayerName(lover2))
                    lover2.cosmetics.nameText.text += suffix;

                if (meetingShow)
                    foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
                        if (lover1.PlayerId == player.TargetPlayerId || lover2.PlayerId == player.TargetPlayerId)
                            player.NameText.text += suffix;
            }

            // Akujo
            if (CachedPlayer.LocalPlayer.PlayerControl.isAlive() && CachedPlayer.LocalPlayer.PlayerControl.isAkujoLover())
            {
                foreach (var akujo in Akujo.players)
                {
                    string suffix = Helpers.cs(akujo.iconColor, " ♥");
                    if (CachedPlayer.LocalPlayer.PlayerControl == akujo.player)
                    {
                        CachedPlayer.LocalPlayer.PlayerControl.cosmetics.nameText.text += suffix;
                        if (akujo.honmei != null && !Helpers.hidePlayerName(akujo.honmei.player))
                            akujo.honmei.player.cosmetics.nameText.text += suffix;

                        foreach (var keep in akujo.keeps)
                        {
                            if (!Helpers.hidePlayerName(keep.player))
                                keep.player.cosmetics.nameText.text += suffix;
                        }

                        if (Helpers.ShowMeetingText)
                        {
                            foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
                            {
                                if (CachedPlayer.LocalPlayer.PlayerControl.PlayerId == player.TargetPlayerId ||
                                    akujo.honmei?.player?.PlayerId == player.TargetPlayerId ||
                                    akujo.keeps.Any(x => x.player.PlayerId == player.TargetPlayerId))
                                    player.NameText.text += suffix;
                            }
                        }
                    }

                    else if (CachedPlayer.LocalPlayer.PlayerControl == akujo.honmei?.player || akujo.keeps.Any(x => CachedPlayer.LocalPlayer.PlayerControl == x?.player))
                    {
                        CachedPlayer.LocalPlayer.PlayerControl.cosmetics.nameText.text += suffix;
                        if (!Helpers.hidePlayerName(akujo.player))
                            akujo.player.cosmetics.nameText.text += suffix;

                        if (Helpers.ShowMeetingText)
                        {
                            foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
                            {
                                if (CachedPlayer.LocalPlayer.PlayerControl.PlayerId == player.TargetPlayerId || akujo.player.PlayerId == player.TargetPlayerId)
                                    player.NameText.text += suffix;
                            }
                        }
                    }
                }
            }

            if (MapOptions.ghostsSeeRoles && CachedPlayer.LocalPlayer.PlayerControl.isDead())
            {
                foreach (var couple in Lovers.couples)
                {
                    string suffix = Lovers.getIcon(couple.lover1);
                    if (Cupid.isCupidLovers(couple.lover1))
                    {
                        suffix = Helpers.cs(Cupid.color, " ♥");
                    }
                    couple.lover1.cosmetics.nameText.text += suffix;
                    couple.lover2.cosmetics.nameText.text += suffix;

                    if (meetingShow)
                        foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
                            if (couple.lover1.PlayerId == player.TargetPlayerId || couple.lover2.PlayerId == player.TargetPlayerId)
                                player.NameText.text += suffix;
                }
                foreach (var akujo in Akujo.players)
                {
                    string suffix = Helpers.cs(akujo.iconColor, " ♥");
                    akujo.player.cosmetics.nameText.text += suffix;
                    if (akujo.honmei != null)
                        akujo.honmei.player.cosmetics.nameText.text += suffix;
                    foreach (var keep in akujo.keeps)
                        keep.player.cosmetics.nameText.text += suffix;

                    if (Helpers.ShowMeetingText)
                    {
                        foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
                        {
                            if (akujo.player.PlayerId == player.TargetPlayerId ||
                                akujo.honmei?.player?.PlayerId == player.TargetPlayerId ||
                                akujo.keeps.Any(x => x.player.PlayerId == player.TargetPlayerId))
                                player.NameText.text += suffix;
                        }
                    }
                }
            }

            // Lawyer
            bool localIsLawyer = Lawyer.lawyer != null && Lawyer.target != null && Lawyer.lawyer == CachedPlayer.LocalPlayer.PlayerControl;
            bool localIsKnowingTarget = Lawyer.lawyer != null && Lawyer.target != null && Lawyer.targetKnows && Lawyer.target == CachedPlayer.LocalPlayer.PlayerControl;
            if (localIsLawyer || (localIsKnowingTarget && !Lawyer.lawyer.Data.IsDead))
            {
                string suffix = Helpers.cs(Lawyer.color, " §");
                if (!Helpers.hidePlayerName(Lawyer.target))
                    Lawyer.target.cosmetics.nameText.text += suffix;

                if (meetingShow)
                    foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
                        if (player.TargetPlayerId == Lawyer.target.PlayerId)
                            player.NameText.text += suffix;
            }

            // Hacker and Detective
            if (CachedPlayer.LocalPlayer.PlayerControl != null && MapOptions.showLighterDarker)
            {
                if (meetingShow)
                {
                    foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
                    {
                        var target = Helpers.playerById(player.TargetPlayerId);
                        if (target != null) player.NameText.text += $" ({(Helpers.isLighterColor(target.Data.DefaultOutfit.ColorId) ? ModTranslation.getString("detectiveLightLabel") : ModTranslation.getString("detectiveDarkLabel"))})";
                    }
                }
            }


        }

        static void updateShielded()
        {
            if (Medic.shielded == null) return;

            if (Medic.shielded.Data.IsDead || Medic.medic == null || Medic.medic.Data.IsDead)
            {
                Medic.shielded = null;
            }
        }

        static void timerUpdate()
        {
            Hacker.hackerTimer -= Time.deltaTime;
            Trickster.lightsOutTimer -= Time.deltaTime;
            Tracker.corpsesTrackingTimer -= Time.deltaTime;
        }

        public static void miniUpdate()
        {
            foreach (var mini in Mini.players)
            {
                _miniUpdate(mini);
            }

        }
        public static void _miniUpdate(Mini mini)
        {
            if (Camouflager.camouflageTimer > 0f) return;

            float growingProgress = mini.growingProgress();
            float scale = growingProgress * 0.35f + 0.35f;
            string suffix = "";
            if (growingProgress != 1f)
                suffix = " <color=#FAD934FF>(" + Mathf.FloorToInt(growingProgress * 18) + ")</color>";

            if (!Helpers.hidePlayerName(mini.player))
                mini.player.cosmetics.nameText.text += suffix;

            if (MeetingHud.Instance != null)
            {
                foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
                    if (player.NameText != null && mini.player.PlayerId == player.TargetPlayerId)
                        player.NameText.text += suffix;
            }

            if (Morphling.morphling != null && Morphling.morphTarget == mini.player && Morphling.morphTimer > 0f && !Helpers.hidePlayerName(Morphling.morphling))
                Morphling.morphling.cosmetics.nameText.text += suffix;
        }

        static void updateImpostorKillButton(HudManager __instance)
        {
            if (!CachedPlayer.LocalPlayer.PlayerControl.Data.Role.IsImpostor) return;
            if (MeetingHud.Instance)
            {
                __instance.KillButton.Hide();
                return;
            }
            bool enabled = Helpers.ShowButtons;
            if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Vampire))
                enabled &= false;
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Mafioso) && !Mafioso.canKill)
                enabled &= false;
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.Janitor))
                enabled &= false;
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.BomberA) && BomberB.isAlive())
                enabled &= false;
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.BomberB) && BomberA.isAlive())
                enabled &= false;
            else if (CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.MimicA) && MimicK.isAlive())
                enabled &= false;

            if (enabled) __instance.KillButton.Show();
            else __instance.KillButton.Hide();
        }

        static void updateUseButton(HudManager __instance)
        {
            if (MeetingHud.Instance) __instance.UseButton.Hide();
        }

        static void updateSabotageButton(HudManager __instance)
        {
            if (MeetingHud.Instance) __instance.SabotageButton.Hide();
        }
        static void updateVentButton(HudManager __instance)
        {
            if (MeetingHud.Instance) __instance.ImpostorVentButton.Hide();
        }
        static void updateReportButton(HudManager __instance)
        {
            if (MeetingHud.Instance) __instance.ReportButton.Hide();
        }

        static void camouflageAndMorphActions()
        {
            float oldCamouflageTimer = Camouflager.camouflageTimer;
            float oldMorphTimer = Morphling.morphTimer;

            Camouflager.camouflageTimer -= Time.deltaTime;
            Morphling.morphTimer -= Time.deltaTime;

            // Everyone but morphling reset
            if (oldCamouflageTimer > 0f && Camouflager.camouflageTimer <= 0f)
            {
                Camouflager.resetCamouflage();
            }

            // Morphling reset
            if (oldMorphTimer > 0f && Morphling.morphTimer <= 0f)
            {
                Morphling.resetMorph();
            }
        }

        static void Postfix(HudManager __instance)
        {
            if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started) return;

            CustomButton.HudUpdate();
            resetNameTagsAndColors();
            setNameColors();
            updateShielded();
            setNameTags();

            // Camouflager and Morphling
            camouflageAndMorphActions();

            // Impostors
            updateImpostorKillButton(__instance);
            // Timer updates
            timerUpdate();
            // Mini
            miniUpdate();

            updateSabotageButton(__instance);
            updateUseButton(__instance);
            updateReportButton(__instance);
            updateVentButton(__instance);
        }
    }
}
