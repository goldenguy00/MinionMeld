using System.Security.Permissions;
using System.Security;
using BepInEx;
using BepInEx.Bootstrap;
using MinionMeld.Modules;
using R2API;
using RoR2;
using UnityEngine;

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace MinionMeld
{
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class MinionMeldPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = $"com.{PluginAuthor}.{PluginName}";
        public const string PluginAuthor = "score";
        public const string PluginName = "DroneMeld";
        public const string PluginVersion = "1.0.5";

        public static bool rooInstalled => Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions");

        public static MinionMeldPlugin Instance { get; private set; }
        public static ItemDef meldStackItem;

        public void Awake()
        {
            Instance = this;

            Log.Init(Logger);

            meldStackItem = ScriptableObject.CreateInstance<ItemDef>();
            meldStackItem.deprecatedTier = ItemTier.NoTier;
            meldStackItem.canRemove = true;
            meldStackItem.hidden = true;
            meldStackItem.nameToken = "ITEM_MINIONMELD_STACK_NAME";
            meldStackItem.loreToken = "";
            meldStackItem.descriptionToken = "";
            meldStackItem.pickupToken = "";
            meldStackItem.name = "MinionMeldInternalStackItem";
            meldStackItem.tags = [ItemTag.BrotherBlacklist, ItemTag.CannotSteal];
            ContentAddition.AddItemDef(meldStackItem);

            PluginConfig.Init(Config);
            Hooks.Init();
        }
    }
}