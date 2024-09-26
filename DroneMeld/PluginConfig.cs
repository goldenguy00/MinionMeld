using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using MinionMeld.Modules;
using RoR2;

namespace MinionMeld
{
    public static class PluginConfig
    {
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
        public static ConfigEntry<string> blacklistOption;
        public static ConfigEntry<string> blacklistOption2;
        public static ConfigEntry<bool> printMasterNames;
        public static readonly HashSet<MasterCatalog.MasterIndex> masterBlacklist = [];
        public static readonly HashSet<MasterCatalog.MasterIndex> turretBlacklist = [];
        public const string permaBlackList = "DevotedLemurianMaster,DevotedLemurianBruiserMaster,NemMercCloneMaster,";

        private static void RebuildBlacklist(HashSet<MasterCatalog.MasterIndex> list, string option)
        {
            list.Clear();
            if (!string.IsNullOrWhiteSpace(option))
            {
                var split = option.Split(',');
                for (var i = 0; i < split.Length; i++)
                    if (!string.IsNullOrWhiteSpace(split[i]))
                    {
                        var name = split[i].Replace(" ", string.Empty).Replace("(Clone)", string.Empty);
                        var idx = MasterCatalog.FindMasterIndex(name);
                        if (idx == MasterCatalog.MasterIndex.none)
                            idx = MasterCatalog.FindMasterIndex(name + "Master");

                        if (idx != MasterCatalog.MasterIndex.none)
                            list.Add(idx);
                    }
            }
        }

        public static void Init(ConfigFile cfg)
        {
            string GENERAL = "General", STATS = "Stats", LIST = "BlackList";
            myConfig = cfg;

            BindOption(GENERAL,
                "!!INFO!!",
                true,
                "The MinionMeld v1.1.0 patch fixed some issues with the plugin metadata using the wrong name." +
                "\r\n\t- --**This causes the config file to get reset**-- but it won't delete the old one." +
                "\r\n\t- The old cfg file is called DroneMeld, and new file is called MinionMeld if you have settings you'd like to transfer.");

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

            blacklistOption = BindOption(LIST,
                "Blacklist",
                "EngiTurretMaster,EngiWalkerTurretMaster,GhoulMaster,TombstoneMaster",
                "Put the broken shit in here, or just things you want duplicates of. For Devotion Artifact, download LemurFusion.\r\n" +
                "To find these, download the DebugToolKit mod, open the console (Ctrl Alt ~), then type list_ai or enable the print option below.");

            blacklistOption2 = BindOption(LIST,
                "Blacklist Teleporting Turret", "",
                "Makes teleporting turret component unable to be applied to these guys. Typically applied to characters without the ability to move on their own.");

            printMasterNames = BindOption(LIST,
                "Print Master Names To Console",
                true,
                "Prints the name to the console (Ctrl Alt ~) when preforming a successful meld. Helpful for setting up the blacklist.");

            On.RoR2.MasterCatalog.Init += (orig) =>
            {
                orig();

                RebuildBlacklist(masterBlacklist, permaBlackList + blacklistOption.Value);
                blacklistOption.SettingChanged += (_, _) => RebuildBlacklist(masterBlacklist, permaBlackList + blacklistOption.Value);
                RebuildBlacklist(turretBlacklist, blacklistOption2.Value);
                blacklistOption2.SettingChanged += (_, _) => RebuildBlacklist(turretBlacklist, blacklistOption2.Value);
            };
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
