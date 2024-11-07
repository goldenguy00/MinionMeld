﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using MinionMeld.Modules;
using RoR2;

namespace MinionMeld
{
    public static class PluginConfig
    {

        public const string permaBlackList = "DevotedLemurianMaster,DevotedLemurianBruiserMaster,NemMercCloneMaster,";

        public static HashSet<MasterCatalog.MasterIndex> MasterBlacklist { get; } = [];
        public static HashSet<MasterCatalog.MasterIndex> TurretBlacklist { get; } = [];
        public static HashSet<MasterCatalog.MasterIndex> MasterWhitelist { get; } = [];
        public static HashSet<MasterCatalog.MasterIndex> TurretWhitelist { get; } = [];

        public static ConfigFile myConfig;

        // general
        public static ConfigEntry<bool> perPlayer;
        public static ConfigEntry<bool> teleturret;
        public static ConfigEntry<bool> respawnSummon;
        public static ConfigEntry<int> maxDronesPerType;
        public static ConfigEntry<bool> enableTurretLeash;
        public static ConfigEntry<int> minionLeashRange;
        public static ConfigEntry<MeldingTime.DronemeldPriorityOrder> priorityOrder;

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

        private static void RebuildBlacklist(HashSet<MasterCatalog.MasterIndex> list, string option)
        {
            list.Clear();
            if (!string.IsNullOrEmpty(option?.Replace(" ", string.Empty)))
            {
                var split = option.Split(',');
                for (var i = 0; i < split.Length; i++)
                {
                    if (!string.IsNullOrEmpty(split[i]))
                    {
                        var name = split[i].Replace("(Clone)", string.Empty);
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
            string GENERAL = "General", STATS = "Stats", LIST = "BlackList", LIST2 = "WhiteList";
            myConfig = cfg;

            perPlayer = BindOption(GENERAL,
                "Limit Drones Per Player",
                true,
                "If false, then the team's collective drones will be limited");

            teleturret = BindOption(GENERAL,
                "Teleporting Turrets",
                true,
                "Turrets, Squids, etc (anything immobile) remember their previous spawn locations and follow when you start a scripted combat event (teleporter, mithrix etc)");

            respawnSummon = BindOption(GENERAL,
                "Spawn In New Location",
                true,
                "Summoned allies will 'respawn' in the location that that they are summoned.");

            maxDronesPerType = BindOptionSlider(GENERAL,
                "Max Minions Per Type",
                1,
                "Max Number of Minions you (or your team) can control of that type before melding is applied.",
                1, 20);

            enableTurretLeash = BindOption(GENERAL,
                "Enable Turret Leash",
                true,
                "Allows turrets to teleport to their owner when too far.");

            minionLeashRange = BindOptionSlider(GENERAL,
                "Minion Leash Range",
                200,
                "Max distance a minion should be from their owner before teleporting. Applies to turrets.",
                50, 1000);

            priorityOrder = BindOption(GENERAL,
                "Selection Priority",
                MeldingTime.DronemeldPriorityOrder.RoundRobin,
                "Used for deciding which drone should be selected for melding.");

            // STATS
            statMultHealth = BindOptionSlider(STATS,
                "Health Multiplier",
                20,
                "Stacks additively.",
                0, 200);

            statMultDamage = BindOptionSlider(STATS,
                "Damage Multiplier",
                20,
                "Stacks additively.",
                0, 200);

            statMultAttackSpeed = BindOptionSlider(STATS,
                "Attack Speed Multiplier",
                20,
                "Stacks additively.",
                0, 200);

            statMultCDR = BindOptionSlider(STATS,
                "Cooldown Reduction Multiplier",
                20,
                "Stacks additively.",
                0, 200);

            vfxResize = BindOptionSlider(STATS,
                "Size Multiplier",
                20,
                "Visual size increase per meld, in percent. Stacks additively.",
                0, 200);

            blacklistMasters = BindOption(LIST,
                "Blacklist",
                "EngiTurretMaster,EngiWalkerTurretMaster,GhoulMaster,TombstoneMaster",
                "Put the broken shit in here, or just things you want duplicates of. For Devotion Artifact, download LemurFusion.\r\n" +
                "To find these, download the DebugToolKit mod, open the console (Ctrl Alt ~), then type list_ai or enable the print option below.");

            blacklistTurrets = BindOption(LIST,
                "Blacklist Teleporting Turret", "",
                "Makes teleporting turret component unable to be applied to these guys. Typically applied to characters without the ability to move on their own.");

            printMasterNames = BindOption(LIST,
                "Print Master Names To Console",
                true,
                "Prints the name to the console (Ctrl Alt ~) when preforming a successful meld. Helpful for setting up the blacklist.");


            useWhitelist = BindOption(LIST2,
                "Use Whitelist",
                false,
                "Use a custom whitelist of allowed CharacterMaster names instead of the default blacklist.");

            whitelistMasters = BindOption(LIST2,
                "Whitelist",
                "",
                "CharacterMaster names that should be allowed to meld. Teleporting turrets will not be affected by this list.");

            whitelistTurrets = BindOption(LIST2,
                "Whitelist Teleporting Turret",
                "",
                "CharacterMaster names of the immobile turret-like allies that should teleport around with you during combat events.");

            On.RoR2.MasterCatalog.Init += MasterCatalog_Init;
        }

        private static void MasterCatalog_Init(On.RoR2.MasterCatalog.orig_Init orig)
        {
            orig();

            RebuildBlacklist(MasterBlacklist, permaBlackList + blacklistMasters.Value);
            blacklistMasters.SettingChanged += (_, _) => RebuildBlacklist(MasterBlacklist, permaBlackList + blacklistMasters.Value);
            RebuildBlacklist(TurretBlacklist, blacklistTurrets.Value);
            blacklistTurrets.SettingChanged += (_, _) => RebuildBlacklist(TurretBlacklist, blacklistTurrets.Value);

            RebuildBlacklist(MasterWhitelist, whitelistMasters.Value);
            whitelistMasters.SettingChanged += (_, _) => RebuildBlacklist(MasterWhitelist, whitelistMasters.Value);
            RebuildBlacklist(TurretWhitelist, whitelistTurrets.Value);
            whitelistTurrets.SettingChanged += (_, _) => RebuildBlacklist(TurretWhitelist, whitelistTurrets.Value);
        }


        #region Config Binding
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static ConfigEntry<T> BindOption<T>(string section, string name, T defaultValue, string description = "", bool restartRequired = false)
        {
            if (string.IsNullOrEmpty(description))
                description = name;

            if (restartRequired)
                description += " (restart required)";

            var configEntry = myConfig.Bind(section, name, defaultValue, description);

            if (MinionMeldPlugin.RooInstalled)
                TryRegisterOption(configEntry, restartRequired);

            return configEntry;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static ConfigEntry<T> BindOptionSlider<T>(string section, string name, T defaultValue, string description = "", float min = 0, float max = 20, bool restartRequired = false)
        {
            if (string.IsNullOrEmpty(description))
                description = name;

            description += " (Default: " + defaultValue + ")";

            if (restartRequired)
                description += " (restart required)";

            var configEntry = myConfig.Bind(section, name, defaultValue, description);

            if (MinionMeldPlugin.RooInstalled)
                TryRegisterOptionSlider(configEntry, min, max, restartRequired);

            return configEntry;
        }
        #endregion

        #region RoO
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void InitRoO()
        {
            RiskOfOptions.ModSettingsManager.SetModDescription("Devotion Artifact but better.");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void TryRegisterOption<T>(ConfigEntry<T> entry, bool restartRequired)
        {
            if (entry is ConfigEntry<string> stringEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.StringInputFieldOption(stringEntry, restartRequired));
                return;
            }
            if (entry is ConfigEntry<float> floatEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.SliderOption(floatEntry, new RiskOfOptions.OptionConfigs.SliderConfig()
                {
                    min = 0,
                    max = 20,
                    FormatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
                return;
            }
            if (entry is ConfigEntry<int> intEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.IntSliderOption(intEntry, restartRequired));
                return;
            }
            if (entry is ConfigEntry<bool> boolEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(boolEntry, restartRequired));
                return;
            }
            if (entry is ConfigEntry<KeyboardShortcut> shortCutEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.KeyBindOption(shortCutEntry, restartRequired));
                return;
            }
            if (typeof(T).IsEnum)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.ChoiceOption(entry, restartRequired));
                return;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void TryRegisterOptionSlider<T>(ConfigEntry<T> entry, float min, float max, bool restartRequired)
        {
            if (entry is ConfigEntry<int> intEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.IntSliderOption(intEntry, new RiskOfOptions.OptionConfigs.IntSliderConfig()
                {
                    min = (int)min,
                    max = (int)max,
                    formatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
                return;
            }

            if (entry is ConfigEntry<float> floatEntry)
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.SliderOption(floatEntry, new RiskOfOptions.OptionConfigs.SliderConfig()
                {
                    min = min,
                    max = max,
                    FormatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
        }
        #endregion
    }
}
