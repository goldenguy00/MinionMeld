using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using MinionMeld.Modules;
using MiscFixes.Modules;
using RoR2;
using UnityEngine;

namespace MinionMeld
{
    public static class PluginConfig
    {
        // general
        public static ConfigEntry<bool> perPlayer;
        public static ConfigEntry<bool> teleturret;
        public static ConfigEntry<bool> respawnSummon;
        public static ConfigEntry<int> maxDronesPerType;
        public static ConfigEntry<bool> enableTurretLeash;
        public static ConfigEntry<int> minionLeashRange;
        public static ConfigEntry<MeldingTime.DronemeldPriorityOrder> priorityOrder;
        public static ConfigEntry<bool> disableTeamCollision;
        public static ConfigEntry<bool> disableProjectileCollision;

        // stats
        public static ConfigEntry<int> statMultHealth;
        public static ConfigEntry<int> statMultDamage;
        public static ConfigEntry<int> statMultAttackSpeed;
        public static ConfigEntry<int> statMultCDR;
        public static ConfigEntry<int> vfxResize;

        //  blacklist
        public static ConfigEntry<string> blacklistMasters;
        public static ConfigEntry<string> blacklistTurrets;
        public static ConfigEntry<bool> printMasterNames;

        // whitelist
        public static ConfigEntry<bool> useWhitelist;
        public static ConfigEntry<string> whitelistMasters;
        public static ConfigEntry<string> whitelistTurrets;

        public static readonly HashSet<MasterCatalog.MasterIndex> MasterBlacklist = [];
        public static readonly HashSet<MasterCatalog.MasterIndex> TurretBlacklist = [];
        public static readonly HashSet<MasterCatalog.MasterIndex> MasterWhitelist = [];
        public static readonly HashSet<MasterCatalog.MasterIndex> TurretWhitelist = [];

        private static readonly Regex StringFilter = new(@"\s", RegexOptions.Compiled);

        [SystemInitializer([typeof(MasterCatalog)])]
        private static void Init()
        {
            const string permaBlackList = "DevotedLemurianMaster,DevotedLemurianBruiserMaster,NemMercCloneMaster,";

            RebuildBlacklist(MasterBlacklist, permaBlackList + blacklistMasters.Value);
            blacklistMasters.SettingChanged += (_, _) => RebuildBlacklist(MasterBlacklist, permaBlackList + blacklistMasters.Value);

            RebuildBlacklist(TurretBlacklist, blacklistTurrets.Value);
            blacklistTurrets.SettingChanged += (_, _) => RebuildBlacklist(TurretBlacklist, blacklistTurrets.Value);

            RebuildBlacklist(MasterWhitelist, whitelistMasters.Value);
            whitelistMasters.SettingChanged += (_, _) => RebuildBlacklist(MasterWhitelist, whitelistMasters.Value);

            RebuildBlacklist(TurretWhitelist, whitelistTurrets.Value);
            whitelistTurrets.SettingChanged += (_, _) => RebuildBlacklist(TurretWhitelist, whitelistTurrets.Value);

            static void RebuildBlacklist(HashSet<MasterCatalog.MasterIndex> list, string option)
            {
                list.Clear();

                option = StringFilter.Replace(option, (match) => string.Empty);

                if (string.IsNullOrEmpty(option))
                    return;

                foreach (var split in option.Split(','))
                {
                    if (!string.IsNullOrEmpty(split))
                    {
                        var name = split.Replace("(Clone)", string.Empty);
                        var idx = MasterCatalog.FindMasterIndex(name);

                        if (idx == MasterCatalog.MasterIndex.none)
                            idx = MasterCatalog.FindMasterIndex(name + "Master");

                        if (idx != MasterCatalog.MasterIndex.none)
                            list.Add(idx);
                    }
                }
            }
        }

        public static void Init(ConfigFile cfg)
        {
            const string GENERAL = "General",
                STATS = "Stats",
                LIST = "BlackList",
                LIST2 = "WhiteList";

            perPlayer = cfg.BindOption(GENERAL,
                "Limit Drones Per Player",
                "If false, then the team's collective drones will be limited",
                true);

            teleturret = cfg.BindOption(GENERAL,
                "Teleporting Turrets",
                "Turrets, Squids, etc (anything immobile) remember their previous spawn locations and follow when you start a scripted combat event (teleporter, mithrix etc)",
                true);

            respawnSummon = cfg.BindOption(GENERAL,
                "Spawn In New Location",
                "Summoned allies will 'respawn' in the location that that they are summoned.",
                true);

            maxDronesPerType = cfg.BindOptionSlider(GENERAL,
                "Max Minions Per Type",
                "Max Number of Minions you (or your team) can control of that type before melding is applied.",
                1,
                1, 20);

            enableTurretLeash = cfg.BindOption(GENERAL,
                "Enable Turret Leash",
                "Allows turrets to teleport to their owner when too far.",
                true);

            minionLeashRange = cfg.BindOptionSlider(GENERAL,
                "Minion Leash Range",
                "Max distance a minion should be from their owner before teleporting. Applies to turrets.",
                200,
                50, 1000);

            priorityOrder = cfg.BindOption(GENERAL,
                "Selection Priority",
                "Used for deciding which drone should be selected for melding.",
                MeldingTime.DronemeldPriorityOrder.RoundRobin);

            disableTeamCollision = cfg.BindOption(GENERAL,
                "Disable Minion Collision",
                "Allows you to walk through any minions.",
                true);

            disableProjectileCollision = cfg.BindOption(GENERAL,
                "Disable Team Attack Collision",
                "Lightweight filter to allow all teammate bullets and projectiles to pass through allies. Should be disabled for certain characters to function correctly.",
                false);

            // STATS
            statMultHealth = cfg.BindOptionSlider(STATS,
                "Health Multiplier",
                "Stacks additively.",
                20,
                0, 200);

            statMultDamage = cfg.BindOptionSlider(STATS,
                "Damage Multiplier",
                "Stacks additively.",
                20,
                0, 200);

            statMultAttackSpeed = cfg.BindOptionSlider(STATS,
                "Attack Speed Multiplier",
                "Stacks additively.",
                20,
                0, 200);

            statMultCDR = cfg.BindOptionSlider(STATS,
                "Cooldown Reduction Multiplier",
                "Stacks additively.",
                20,
                0, 200);

            vfxResize = cfg.BindOptionSlider(STATS,
                "Size Multiplier",
                "Visual size increase per meld, in percent. Stacks additively.",
                20,
                0, 200);

            blacklistMasters = cfg.BindOption(LIST,
                "Blacklist",
                "Put the broken shit in here, or just things you want duplicates of. For Devotion Artifact, download LemurFusion.\r\n" +
                "To find these, download the DebugToolKit mod, open the console (Ctrl Alt ~), then type list_ai or enable the print option below.",

                "EngiTurretMaster,EngiWalkerTurretMaster,GhoulMaster,TombstoneMaster");

            blacklistTurrets = cfg.BindOption(LIST,
                "Blacklist Teleporting Turret",
                "Makes teleporting turret component unable to be applied to these guys. Typically applied to characters without the ability to move on their own.",
                "");

            printMasterNames = cfg.BindOption(LIST,
                "Print Master Names To Console",
                "Prints the name to the console (Ctrl Alt ~) when preforming a successful meld. Helpful for setting up the blacklist.",
                true);


            useWhitelist = cfg.BindOption(LIST2,
                "Use Whitelist",
                "Use a custom whitelist of allowed CharacterMaster names instead of the default blacklist.",
                false);

            whitelistMasters = cfg.BindOption(LIST2,
                "Whitelist",
                "CharacterMaster names that should be allowed to meld. Teleporting turrets will not be affected by this list.",
                "");

            whitelistTurrets = cfg.BindOption(LIST2,
                "Whitelist Teleporting Turret",
                "CharacterMaster names of the immobile turret-like allies that should teleport around with you during combat events.",
                "");
        }


        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void InitRoO()
        {
            try
            {
                RiskOfOptions.ModSettingsManager.SetModDescription("Combines the guys.", MinionMeldPlugin.PluginGUID, MinionMeldPlugin.PluginName);

                var iconStream = System.IO.File.ReadAllBytes(System.IO.Path.Combine(MinionMeldPlugin.Instance.DirectoryName, "icon.png"));
                var tex = new Texture2D(256, 256);
                tex.LoadImage(iconStream);
                var icon = Sprite.Create(tex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));

                RiskOfOptions.ModSettingsManager.SetModIcon(icon);
            }
            catch (Exception e)
            {
                Log.Debug(e.ToString());
            }
        }
    }
}
