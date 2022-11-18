using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using TheOtherRoles.Objects;
using TheOtherRoles.Patches;
using UnityEngine;
using static TheOtherRoles.GameHistory;
using static TheOtherRoles.Patches.PlayerControlFixedUpdatePatch;

namespace TheOtherRoles
{
    [HarmonyPatch]
    public class BomberB : RoleBase<BomberB>
    {
        public static Color color = Palette.ImpostorRed;

        public static CustomButton bomberButton;
        public static CustomButton releaseButton;

        public static PlayerControl bombTarget;
        public static PlayerControl currentTarget;
        public static PlayerControl tmpTarget;
        public static TMPro.TextMeshPro targetText;
        public static TMPro.TextMeshPro partnerTargetText;
        public static Dictionary<byte, PoolablePlayer> playerIcons = new();
        public static float duration { get { return CustomOptionHolder.bomberDuration.getFloat(); } }
        public static float cooldown { get { return CustomOptionHolder.bomberCooldown.getFloat(); } }
        public static bool ifOneDiesBothDie { get { return CustomOptionHolder.bomberIfOneDiesBothDie.getBool(); } }
        public static Sprite bomberButtonSprite;
        public static Sprite releaseButtonSprite;
        public static float updateTimer = 0f;
        public static List<Arrow> arrows = new();
        public static float arrowUpdateInterval = 0.5f;

        public BomberB()
        {
            RoleType = roleId = RoleType.BomberB;
        }

        public override void OnMeetingStart() { }
        public override void OnMeetingEnd()
        {
            bombTarget = null;
        }
        public override void FixedUpdate()
        {
            if (player == CachedPlayer.LocalPlayer.PlayerControl)
            {
                currentTarget = setTarget();
                setPlayerOutline(currentTarget, BomberA.color);
                arrowUpdate();

                foreach (PoolablePlayer pp in MapOptions.playerIcons.Values) pp.gameObject.SetActive(false);
                foreach (PoolablePlayer pp in playerIcons.Values) pp.gameObject.SetActive(false);
                if (player.isAlive() && BomberA.isAlive())
                {
                    if (bombTarget != null && MapOptions.playerIcons.ContainsKey(bombTarget.PlayerId) && MapOptions.playerIcons[bombTarget.PlayerId].gameObject != null)
                    {
                        var icon = MapOptions.playerIcons[bombTarget.PlayerId];
                        Vector3 bottomLeft = new(-FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.x, FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.y, FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.z);
                        icon.gameObject.SetActive(true);
                        icon.transform.localPosition = bottomLeft + new Vector3(-0.25f, 0f, 0);
                        icon.transform.localScale = Vector3.one * 0.4f;
                        if (targetText == null)
                        {
                            targetText = GameObject.Instantiate(icon.cosmetics.nameText, icon.cosmetics.nameText.transform.parent);
                            targetText.enableWordWrapping = false;
                            targetText.transform.localScale = Vector3.one * 1.5f;
                            targetText.transform.localPosition += new Vector3(0f, 1.7f, 0);
                        }
                        targetText.text = ModTranslation.getString("bomberTarget");
                        targetText.gameObject.SetActive(true);
                        targetText.transform.parent = icon.gameObject.transform;
                    }
                    // 相方の設置したターゲットを表示する
                    if (BomberA.bombTarget != null && playerIcons.ContainsKey(BomberA.bombTarget.PlayerId) && playerIcons[BomberA.bombTarget.PlayerId].gameObject != null)
                    {
                        var icon = playerIcons[BomberA.bombTarget.PlayerId];
                        Vector3 bottomLeft = new(-FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.x, FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.y, FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.z);
                        icon.gameObject.SetActive(true);
                        icon.transform.localPosition = bottomLeft + new Vector3(1.0f, 0f, 0);
                        icon.transform.localScale = Vector3.one * 0.4f;
                        if (partnerTargetText == null)
                        {
                            partnerTargetText = GameObject.Instantiate(icon.cosmetics.nameText, icon.cosmetics.nameText.transform.parent);
                            partnerTargetText.enableWordWrapping = false;
                            partnerTargetText.transform.localScale = Vector3.one * 1.5f;
                            partnerTargetText.transform.localPosition += new Vector3(0f, 1.7f, 0);
                        }
                        partnerTargetText.text = ModTranslation.getString("bomberPartnerTarget");
                        partnerTargetText.gameObject.SetActive(true);
                        partnerTargetText.transform.parent = icon.gameObject.transform;
                    }
                }
            }
        }
        public override void OnKill(PlayerControl target) { }
        public override void OnDeath(PlayerControl killer = null)
        {
            if (ifOneDiesBothDie)
            {
                var partner = BomberA.players.FirstOrDefault().player;
                if (!partner.Data.IsDead)
                {
                    if (killer != null)
                    {
                        partner.MurderPlayer(partner);
                    }
                    else
                    {
                        partner.Exiled();
                    }

                    finalStatuses[partner.PlayerId] = FinalStatus.Suicide;
                }
            }

        }
        public override void OnFinishShipStatusBegin() { }
        public override void HandleDisconnect(PlayerControl player, DisconnectReasons reason) { }

        public static void MakeButtons(HudManager hm)
        {

            // Bomber button
            bomberButton = new CustomButton(
                // OnClick
                () =>
                {
                    if (currentTarget != null)
                    {
                        tmpTarget = currentTarget;
                        bomberButton.HasEffect = true;
                    }
                },
                // HasButton
                () => { return CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.BomberB) && CachedPlayer.LocalPlayer.PlayerControl.isAlive() && BomberA.isAlive(); },
                // CouldUse
                () =>
                {
                    if (bomberButton.isEffectActive && tmpTarget != currentTarget)
                    {
                        tmpTarget = null;
                        bomberButton.Timer = 0f;
                        bomberButton.isEffectActive = false;
                    }

                    return CachedPlayer.LocalPlayer.PlayerControl.CanMove && currentTarget != null;
                },
                // OnMeetingEnds
                () =>
                {
                    bomberButton.Timer = bomberButton.MaxTimer;
                    bomberButton.isEffectActive = false;
                    tmpTarget = null;
                },
                getBomberButtonSprite(),
                new Vector3(-1.8f, -0.06f, 0),
                hm,
                hm.KillButton,
                KeyCode.F,
                true,
                duration,
                // OnEffectsEnd
                () =>
                {
                    if (tmpTarget != null)
                    {
                        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.PlantBomb, Hazel.SendOption.Reliable, -1);
                        writer.Write(tmpTarget.PlayerId);
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                        BomberB.bombTarget = tmpTarget;
                    }

                    tmpTarget = null;
                    bomberButton.Timer = bomberButton.MaxTimer;

                }
            )
            {
                buttonText = ModTranslation.getString("bomberPlantBomb")
            };
            // Bomber button
            releaseButton = new CustomButton(
                // OnClick
                () =>
                {
                    var bomberA = BomberA.allPlayers.FirstOrDefault();
                    float distance = Vector2.Distance(CachedPlayer.LocalPlayer.PlayerControl.transform.localPosition, bomberA.transform.localPosition);

                    if (CachedPlayer.LocalPlayer.PlayerControl.CanMove && BomberA.bombTarget != null && BomberB.bombTarget != null && bomberA.isAlive() && distance < 1)
                    {
                        var target = BomberB.bombTarget;
                        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.ReleaseBomb, Hazel.SendOption.Reliable, -1);
                        writer.Write(CachedPlayer.LocalPlayer.PlayerControl.PlayerId);
                        writer.Write(target.PlayerId);
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                        RPCProcedure.releaseBomb(CachedPlayer.LocalPlayer.PlayerControl.PlayerId, target.PlayerId);
                    }
                },
                // HasButton
                () => { return CachedPlayer.LocalPlayer.PlayerControl.isRole(RoleType.BomberB) && CachedPlayer.LocalPlayer.PlayerControl.isAlive() && BomberA.isAlive(); },
                // CouldUse
                () =>
                {
                    var bomberA = BomberA.allPlayers.FirstOrDefault();
                    float distance = Vector2.Distance(CachedPlayer.LocalPlayer.PlayerControl.transform.localPosition, bomberA.transform.localPosition);

                    return CachedPlayer.LocalPlayer.PlayerControl.CanMove && BomberA.bombTarget != null && BomberB.bombTarget != null && bomberA.isAlive() && distance < 1;
                },
                // OnMeetingEnds
                () =>
                {
                    releaseButton.Timer = releaseButton.MaxTimer;
                },
                getReleaseButtonSprite(),
                new Vector3(-2.7f, -0.06f, 0),
                hm,
                hm.KillButton,
                KeyCode.F,
                false
            )
            {
                buttonText = ModTranslation.getString("bomberDetonate")
            };

        }
        public static void SetButtonCooldowns()
        {
            bomberButton.MaxTimer = cooldown;
            bomberButton.EffectDuration = duration;
            releaseButton.MaxTimer = 0f;
        }

        public static void Clear()
        {
            bombTarget = null;
            currentTarget = null;
            tmpTarget = null;
            arrows = new List<Arrow>();
            players = new List<BomberB>();
            playerIcons = new Dictionary<byte, PoolablePlayer>();
            targetText = null;
            partnerTargetText = null;
        }
        public static bool isAlive()
        {
            foreach (var bomber in players)
            {
                if (!(bomber.player.Data.IsDead || bomber.player.Data.Disconnected))
                    return true;
            }
            return false;
        }
        public static Sprite getBomberButtonSprite()
        {
            if (bomberButtonSprite) return bomberButtonSprite;
            bomberButtonSprite = ModTranslation.getImage("PlantBombButton.png", 115f);
            return bomberButtonSprite;
        }
        public static Sprite getReleaseButtonSprite()
        {
            if (releaseButtonSprite) return releaseButtonSprite;
            releaseButtonSprite = ModTranslation.getImage("ReleaseButton.png", 115f);
            return releaseButtonSprite;
        }
        static void arrowUpdate()
        {
            if ((BomberA.bombTarget == null || BomberB.bombTarget == null) && !BomberA.alwaysShowArrow) return;

            // 前フレームからの経過時間をマイナスする
            updateTimer -= Time.fixedDeltaTime;

            // 1秒経過したらArrowを更新
            if (updateTimer <= 0.0f)
            {

                // 前回のArrowをすべて破棄する
                foreach (Arrow arrow in arrows)
                {
                    if (arrow != null)
                    {
                        arrow.arrow.SetActive(false);
                        UnityEngine.Object.Destroy(arrow.arrow);
                    }
                }

                // Arrows一覧
                arrows = new List<Arrow>();

                // 相方の位置を示すArrowsを描画
                foreach (PlayerControl p in CachedPlayer.AllPlayers)
                {
                    if (p.Data.IsDead) continue;
                    if (p.isRole(RoleType.BomberA))
                    {
                        Arrow arrow;
                        arrow = new Arrow(Color.red);
                        arrow.arrow.SetActive(true);
                        arrow.Update(p.transform.position);
                        arrows.Add(arrow);
                    }
                }

                // タイマーに時間をセット
                updateTimer = arrowUpdateInterval;
            }
        }

        [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
        class IntroCutsceneOnDestroyPatch
        {
            public static void Prefix(IntroCutscene __instance)
            {
                if (CachedPlayer.LocalPlayer.PlayerControl != null && FastDestroyableSingleton<HudManager>.Instance != null)
                {
                    Vector3 bottomLeft = new(-FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.x, FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.y, FastDestroyableSingleton<HudManager>.Instance.UseButton.transform.localPosition.z);
                    foreach (PlayerControl p in CachedPlayer.AllPlayers)
                    {
                        GameData.PlayerInfo data = p.Data;
                        PoolablePlayer player = UnityEngine.Object.Instantiate<PoolablePlayer>(__instance.PlayerPrefab, FastDestroyableSingleton<HudManager>.Instance.transform);
                        player.UpdateFromPlayerOutfit((GameData.PlayerOutfit)p.Data.DefaultOutfit, PlayerMaterial.MaskType.ComplexUI, p.Data.IsDead, true);
                        player.SetFlipX(true);
                        player.cosmetics.currentPet?.gameObject.SetActive(false);
                        player.cosmetics.nameText.text = p.Data.DefaultOutfit.PlayerName;
                        player.gameObject.SetActive(false);
                        playerIcons[p.PlayerId] = player;
                    }
                }
            }
        }
    }
}
