using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Bootstrap;
using MinionMeld.Components;
using MinionMeld.Modules;
using RoR2;
using UnityEngine;

[module: UnverifiableCode]
[assembly: HG.Reflection.SearchableAttribute.OptIn]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace MinionMeld
{
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(R2API.ContentManagement.R2APIContentManager.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(R2API.ItemAPI.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(R2API.LanguageAPI.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(R2API.PrefabAPI.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(R2API.RecalculateStatsAPI.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class MinionMeldPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = $"com.{PluginAuthor}.{PluginName}";
        public const string PluginAuthor = "score";
        public const string PluginName = "MinionMeld";
        public const string PluginVersion = "1.2.0";

        public static bool RooInstalled => Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions");

        public static MinionMeldPlugin Instance { get; private set; }
        internal string DirectoryName => System.IO.Path.GetDirectoryName(Info.Location);

        private static ItemDef meldStackItem;
        public static ItemIndex meldStackIndex => meldStackItem.itemIndex;

        public void Awake()
        {
            Instance = this;

            Log.Init(Logger);

            if (RooInstalled)
                PluginConfig.InitRoO();
            
            PluginConfig.Init(Config);

            meldStackItem = ScriptableObject.CreateInstance<ItemDef>();
#pragma warning disable CS0618 // Type or member is obsolete
            meldStackItem.deprecatedTier = ItemTier.NoTier;
#pragma warning restore CS0618 // Type or member is obsolete
            meldStackItem.canRemove = true;
            meldStackItem.hidden = true;
            meldStackItem.nameToken = "ITEM_MINIONMELD_STACK_NAME";
            meldStackItem.loreToken = "";
            meldStackItem.descriptionToken = "";
            meldStackItem.pickupToken = "";
            meldStackItem.name = "MinionMeldInternalStackItem";
            meldStackItem.tags = [ItemTag.BrotherBlacklist, ItemTag.CannotSteal];
            R2API.ContentAddition.AddItemDef(meldStackItem);

            Hooks.Init();
            TurretHooks.Init();
            MultiEquipDrone.Init();
        }
    }
}
