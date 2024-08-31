using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using RoR2;

namespace MinionMeld.Modules
{
    public static class PluginConfig
    {
        public static ConfigFile myConfig;

        // general
        public static ConfigEntry<bool> perPlayer;
        public static ConfigEntry<bool> teleturret;
        public static ConfigEntry<int> maxDronesPerType;
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
        public static readonly HashSet<MasterCatalog.MasterIndex> masterBlacklist = [];
        public static readonly HashSet<MasterCatalog.MasterIndex> turretBlacklist = [];
        private static void RebuildBlacklist(HashSet<MasterCatalog.MasterIndex> list, ConfigEntry<string> option)
        {
            list.Clear();
            if (!string.IsNullOrWhiteSpace(option.Value))
            {
                var split = option.Value.Split(',');
                for (int i = 0; i < split.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(split[i]))
                    {
                        var name = split[i].Replace(" ", string.Empty).Replace("(Clone)", string.Empty);
                        var idx = MasterCatalog.FindMasterIndex(name);
                        if (idx != MasterCatalog.MasterIndex.none)
                            list.Add(idx);
                    }
                }
            }
        }
        public static void Init(ConfigFile cfg)
        {
            string GENERAL = "General", STATS = "Stats", LIST = "WhiteList";
            myConfig = cfg;

            perPlayer = BindOption(GENERAL,
                "Limit Drones Per Player",
                true,
                "If false, then the team's collective drones will be limited");

            teleturret = BindOption(GENERAL,
                "Teleporting Turrets",
                true,
                "Turrets, Squids, etc (anything immobile) remember their previous spawn locations and follow when you start a scripted combat event (teleporter, mithrix etc)");

            maxDronesPerType = BindOptionSlider(GENERAL,
                "Max Drones Per Type",
                1,
                "Max Number of Drones you (or your team) can control of that type before melding is applied.",
                1, 20);

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
                0,
                "** DOESNT WORK ** UNDER CONSTRUCTION ** I HATE NETWORKING **",
                0, 200);

            blacklistOption = BindOption(LIST,
                "Blacklist",
                "DevotedLemurianMaster,DevotedLemurianBruiserMaster,EquipmentDroneMaster,EngiTurretMaster,EngiBeamTurretMaster",
                "Put the broken shit in here, or just things you want duplicates of. For devotion, download LemurFusion.\r\n" +
                "To find these, open the console (Ctrl Alt ~), then type list_ai");

            blacklistOption2 = BindOption(LIST,
                "Blacklist Teleporting Turret", "",
                "Makes teleporting turret component unable to be applied to these guys. Typically applied to characters without the ability to move on their own.");

            On.RoR2.MasterCatalog.Init += (orig) =>
            {
                orig();

                RebuildBlacklist(masterBlacklist, blacklistOption);
                blacklistOption.SettingChanged += (_, _) => RebuildBlacklist(masterBlacklist, blacklistOption);
                RebuildBlacklist(turretBlacklist, blacklistOption2);
                blacklistOption2.SettingChanged += (_, _) => RebuildBlacklist(turretBlacklist, blacklistOption2);
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

            if (MinionMeldPlugin.rooInstalled)
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

            if (MinionMeldPlugin.rooInstalled)
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
                    formatString = "{0:0.00}",
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
                    formatString = "{0:0.00}",
                    restartRequired = restartRequired
                }));
        }
        #endregion
    }
}
