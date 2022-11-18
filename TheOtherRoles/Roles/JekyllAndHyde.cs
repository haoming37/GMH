using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using TheOtherRoles.Objects;
using UnityEngine;
using static TheOtherRoles.Patches.PlayerControlFixedUpdatePatch;


namespace TheOtherRoles
{
    [HarmonyPatch]
    public class JekyllAndHyde : RoleBase<JekyllAndHyde>
    {

        public enum Status
        {
            None,
            Jekyll,
            Hyde,
        }

        public static Status status;
        public static Color color = Color.grey;
        public static int counter = 0;
        public static int numberToWin { get { return (int)CustomOptionHolder.jekyllAndHydeNumberToWin.getFloat(); } }
        public static float suicideTimer { get { return CustomOptionHolder.jekyllAndHydeSuicideTimer.getFloat(); } }
        public static bool reset { get { return CustomOptionHolder.jekyyllAndHydeResetAfterMeeting.getBool(); } }
        public static float cooldown { get { return CustomOptionHolder.jekyllAndHydeCooldown.getFloat(); } }
        public static int numCommonTasks { get { return CustomOptionHolder.jekyllAndHydeTasks.commonTasks; } }
        public static int numLongTasks { get { return CustomOptionHolder.jekyllAndHydeTasks.longTasks; } }
        public static int numShortTasks { get { return CustomOptionHolder.jekyllAndHydeTasks.shortTasks; } }
        public static int numTasks { get { return (int)CustomOptionHolder.jekyllAndHydeNumTasks.getFloat(); } }
        public static int numUsed;
        public static bool oddIsJekyll;
        public static bool triggerWin = false;
        public static CustomButton killButton;
        public static CustomButton suicideButton;
        public static CustomButton drugButton;
        public static PlayerControl currentTarget;
        public static TMPro.TMP_Text text;
        public static TMPro.TMP_Text drugText;

        public JekyllAndHyde()
        {
            RoleType = roleId = RoleType.JekyllAndHyde;
        }

        public override void OnMeetingStart() { }
        public override void OnMeetingEnd()
        {
            if (reset)
                suicideButton.Timer = suicideButton.MaxTimer;
        }
        public override void FixedUpdate()
        {
            if (player == CachedPlayer.LocalPlayer.PlayerControl && !isJekyll())
            {
                currentTarget = setTarget();
                setPlayerOutline(currentTarget, JekyllAndHyde.color);
            }
        }
        public override void OnKill(PlayerControl target)
        {
            if (!(target.isRole(RoleType.SchrodingersCat) && SchrodingersCat.team == SchrodingersCat.Team.None))
                counter += 1;
            if (counter >= numberToWin) triggerWin = true;
            killButton.Timer = killButton.MaxTimer;
            suicideButton.Timer = suicideButton.MaxTimer;
        }
        public override void OnDeath(PlayerControl killer = null) { }
        public override void OnFinishShipStatusBegin()
        {
            PlayerControl.LocalPlayer.clearAllTasks();
            local.assignTasks();
            oddIsJekyll = TheOtherRoles.rnd.Next(0, 2) == 1;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.SetOddIsJekyll, Hazel.SendOption.Reliable, -1);
            writer.Write(oddIsJekyll);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public override void HandleDisconnect(PlayerControl player, DisconnectReasons reason) { }

        public static void MakeButtons(HudManager hm)
        {
            // Bomber button
            killButton = new CustomButton(
                // OnClick
                () =>
                {
                    if (Helpers.checkMuderAttemptAndKill(CachedPlayer.LocalPlayer.PlayerControl, currentTarget) == MurderAttemptResult.SuppressKill) return;

                    killButton.Timer = killButton.MaxTimer;
                    suicideButton.Timer = suicideButton.MaxTimer;
                    currentTarget = null;
                },
                // HasButton
                () => { return CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.JekyllAndHyde) && !JekyllAndHyde.isJekyll() && CachedPlayer.LocalPlayer.PlayerControl.isAlive(); },
                // CouldUse
                () =>
                {
                    if (text != null)
                    {
                        text.text = $"{counter}/{numberToWin}";
                    }
                    return currentTarget != null && CachedPlayer.LocalPlayer.PlayerControl.CanMove;
                },
                // OnMeetingEnds
                () =>
                {
                    killButton.Timer = killButton.MaxTimer = cooldown;
                },
                hm.KillButton.graphic.sprite,
                new Vector3(0f, 1f, 0),
                hm,
                hm.KillButton,
                KeyCode.Q,
                false
            )
            {
                MaxTimer = cooldown
            };
            killButton.Timer = killButton.MaxTimer;
            text = GameObject.Instantiate(killButton.actionButton.cooldownTimerText, killButton.actionButton.cooldownTimerText.transform.parent);
            text.text = "";
            text.enableWordWrapping = false;
            text.transform.localScale = Vector3.one * 0.5f;
            text.transform.localPosition += new Vector3(-0.05f, 0.7f, 0);

            drugButton = new CustomButton(
                // OnClick
                () =>
                {
                    oddIsJekyll = !oddIsJekyll;
                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.SetOddIsJekyll, Hazel.SendOption.Reliable, -1);
                    writer.Write(oddIsJekyll);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    suicideButton.Timer = suicideButton.MaxTimer;
                    killButton.Timer = killButton.MaxTimer;
                    numUsed += 1;
                },
                // HasButton
                () => { return CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.JekyllAndHyde) && CachedPlayer.LocalPlayer.PlayerControl.isAlive() && numUsed < getNumDrugs(); },
                // CouldUse
                () =>
                {
                    if (drugText != null)
                    {
                        drugText.text = $"{numUsed}/{getNumDrugs()}";
                    }
                    return CachedPlayer.LocalPlayer.PlayerControl.CanMove;
                },
                // OnMeetingEnds
                () =>
                {
                    drugButton.Timer = drugButton.MaxTimer;
                },
                PlagueDoctor.getSyringeIcon(),
                new Vector3(-0.9f, 1f, 0),
                hm,
                hm.AbilityButton,
                KeyCode.F,
                false
            )
            {
                buttonText = ModTranslation.getString("jekyllAndHydeDrugButton"),
                MaxTimer = 0,
                Timer = 0f
            };
            drugText = GameObject.Instantiate(drugButton.actionButton.cooldownTimerText, drugButton.actionButton.cooldownTimerText.transform.parent); text.text = "";
            drugText.enableWordWrapping = false;
            drugText.transform.localScale = Vector3.one * 0.5f;
            drugText.transform.localPosition += new Vector3(-0.05f, 0.7f, 0);

            // Suicide Countdown
            suicideButton = new CustomButton(
                () => { },
                () => { return CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.JekyllAndHyde) && !JekyllAndHyde.isJekyll() && CachedPlayer.LocalPlayer.PlayerControl.isAlive(); },
                () => { return true; },
                () => { },
                SerialKiller.getButtonSprite(),
                new Vector3(-1.8f, -0.06f, 0),
                hm,
                hm.AbilityButton,
                KeyCode.None,
                true,
                suicideTimer,
                () => { local.suicide(); }
            )
            {
                MaxTimer = suicideTimer
            };
            suicideButton.Timer = suicideButton.MaxTimer;
            suicideButton.buttonText = ModTranslation.getString("SerialKillerText");
            suicideButton.isEffectActive = true;
        }
        public void suicide()
        {
            byte targetId = CachedPlayer.LocalPlayer.PlayerControl.PlayerId;
            MessageWriter killWriter = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.SerialKillerSuicide, Hazel.SendOption.Reliable, -1); killWriter.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(killWriter);
            RPCProcedure.serialKillerSuicide(targetId);
        }

        public static void SetButtonCooldowns()
        {
            killButton.Timer = killButton.MaxTimer = cooldown;
            suicideButton.Timer = suicideButton.MaxTimer = suicideTimer;
            drugButton.Timer = drugButton.MaxTimer = 0;
        }
        public static void Clear()
        {
            players = new List<JekyllAndHyde>();
            status = Status.None;
            counter = 0;
            triggerWin = false;
            numUsed = 0;
        }

        public static bool isOdd(int n)
        {
            return n % 2 == 1;
        }

        public static bool isJekyll()
        {
            if (status == Status.None)
            {
                var alive = PlayerControl.AllPlayerControls.GetFastEnumerator().ToArray().Where(x =>
                {
                    return x.isAlive() && x != Puppeteer.dummy;
                });
                bool ret = oddIsJekyll ? isOdd(alive.Count()) : !isOdd(alive.Count());
                return ret;
            }
            return status == Status.Jekyll;
        }

        public static int countLovers()
        {
            int counter = 0;
            foreach (var player in allPlayers)
            {
                if (player.isLovers()) counter += 1;
            }
            return counter;
        }


        public static int getNumDrugs()
        {
            var p = players.Where(p => p.player == CachedPlayer.LocalPlayer.PlayerControl).FirstOrDefault();
            int counter = p.player.Data.Tasks.ToArray().Where(t => t.Complete).Count();
            return (int)Math.Floor((float)counter / numTasks);
        }

        public void assignTasks()
        {
            player.generateAndAssignTasks(numCommonTasks, numShortTasks, numLongTasks);
        }
    }
}
