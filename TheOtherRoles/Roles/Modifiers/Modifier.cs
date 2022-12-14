using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using HarmonyLib;
using Hazel;
using TheOtherRoles.Objects;
using TheOtherRoles.Patches;
using UnityEngine;
using static TheOtherRoles.GameHistory;
using static TheOtherRoles.TheOtherRoles;
using static TheOtherRoles.TheOtherRolesGM;

namespace TheOtherRoles
{
    public enum ModifierType
    {
        Madmate = 0,
        CreatedMadmate,
        LastImpostor,
        Munou,
        AntiTeleport,
        Mini,
        AkujoHonmei,
        AkujoKeep,

        // don't put anything below this
        NoModifier = int.MaxValue
    }

    [HarmonyPatch]
    public static class ModifierData
    {
        public static Dictionary<ModifierType, Type> allModTypes = new()
        {
            { ModifierType.Madmate, typeof(ModifierBase<Madmate>) },
            { ModifierType.CreatedMadmate, typeof(ModifierBase<CreatedMadmate>) },
            { ModifierType.LastImpostor, typeof(ModifierBase<LastImpostor>) },
            { ModifierType.Munou, typeof(ModifierBase<Munou>) },
            { ModifierType.AntiTeleport, typeof(ModifierBase<AntiTeleport>) },
            { ModifierType.Mini, typeof(ModifierBase<Mini>) },
            { ModifierType.AkujoHonmei, typeof(ModifierBase<AkujoHonmei>)},
            { ModifierType.AkujoKeep, typeof(ModifierBase<AkujoKeep>)},
        };
    }

    public abstract class Modifier
    {
        public static List<Modifier> allModifiers = new();
        public PlayerControl player;
        public ModifierType modId;

        public abstract void OnMeetingStart();
        public abstract void OnMeetingEnd();
        public abstract void FixedUpdate();
        public abstract void OnKill(PlayerControl target);
        public abstract void OnDeath(PlayerControl killer = null);
        public abstract void OnFinishShipStatusBegin();
        public abstract void HandleDisconnect(PlayerControl player, DisconnectReasons reason);
        public virtual void ResetModifier() { }
        public virtual string modifyNameText(string nameText) { return nameText; }
        public virtual string modifyRoleText(string roleText, List<RoleInfo> roleInfo, bool useColors = true, bool includeHidden = false) { return roleText; }
        public virtual string meetingInfoText() { return ""; }

        public static void ClearAll()
        {
            allModifiers = new List<Modifier>();
        }
    }

    [HarmonyPatch]
    public abstract class ModifierBase<T> : Modifier where T : ModifierBase<T>, new()
    {
        public static List<T> players = new();
        public static ModifierType ModType;
        public static List<RoleType> persistRoleChange = new List<RoleType>();

        public void Init(PlayerControl player)
        {
            this.player = player;
            players.Add((T)this);
            allModifiers.Add(this);
        }

        public static T local
        {
            get
            {
                return players.FirstOrDefault(x => x.player == CachedPlayer.LocalPlayer.PlayerControl);
            }
        }

        public static List<PlayerControl> allPlayers
        {
            get
            {
                return players.Select(x => x.player).ToList();
            }
        }

        public static List<PlayerControl> livingPlayers
        {
            get
            {
                return players.Select(x => x.player).Where(x => x.isAlive()).ToList();
            }
        }

        public static List<PlayerControl> deadPlayers
        {
            get
            {
                return players.Select(x => x.player).Where(x => !x.isAlive()).ToList();
            }
        }

        public static bool exists
        {
            get { return Helpers.RolesEnabled && players.Count > 0; }
        }

        public static T getModifier(PlayerControl player = null)
        {
            player ??= CachedPlayer.LocalPlayer.PlayerControl;
            return players.FirstOrDefault(x => x.player == player);
        }

        public static bool hasModifier(PlayerControl player)
        {
            return players.Any(x => x.player == player);
        }

        public static T addModifier(PlayerControl player)
        {
            T mod = new T();
            mod.Init(player);
            return mod;
        }

        public static void eraseModifier(PlayerControl player, RoleType newRole = RoleType.NoRole)
        {
            List<T> toRemove = new List<T>();

            foreach (var p in players)
            {
                if (p.player == player && p.modId == ModType && !persistRoleChange.Contains(newRole))
                    toRemove.Add(p);
            }
            players.RemoveAll(x => toRemove.Contains(x));
            allModifiers.RemoveAll(x => toRemove.Contains(x));
        }

        public static void swapModifier(PlayerControl p1, PlayerControl p2)
        {
            var index = players.FindIndex(x => x.player == p1);
            if (index >= 0)
            {
                players[index].player = p2;
            }
        }
    }


    public static class ModifierHelpers
    {
        public static bool hasModifier(this PlayerControl player, ModifierType mod)
        {
            foreach (var t in ModifierData.allModTypes)
            {
                if (mod == t.Key)
                {
                    return (bool)t.Value.GetMethod("hasModifier", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { player });
                }
            }
            return false;
        }

        public static void addModifier(this PlayerControl player, ModifierType mod)
        {
            foreach (var t in ModifierData.allModTypes)
            {
                if (mod == t.Key)
                {
                    t.Value.GetMethod("addModifier", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { player });
                    return;
                }
            }
        }

        public static void eraseModifier(this PlayerControl player, ModifierType mod)
        {
            if (hasModifier(player, mod))
            {
                foreach (var t in ModifierData.allModTypes)
                {
                    if (mod == t.Key)
                    {
                        t.Value.GetMethod("eraseModifier", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { player });
                        return;
                    }
                }
                Helpers.log($"eraseRole: no method found for role type {mod}");
            }
        }

        public static void eraseAllModifiers(this PlayerControl player, RoleType newRole = RoleType.NoRole)
        {
            foreach (var t in ModifierData.allModTypes)
            {
                t.Value.GetMethod("eraseModifier", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { player, newRole });
            }
        }

        public static void swapModifiers(this PlayerControl player, PlayerControl target)
        {
            foreach (var t in ModifierData.allModTypes)
            {
                if (player.hasModifier(t.Key))
                {
                    t.Value.GetMethod("swapModifier", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { player, target });
                }
            }
        }
    }
}
