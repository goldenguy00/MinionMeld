using System.Security.Permissions;
using System.Security;
using BepInEx;
using BepInEx.Bootstrap;
using MinionMeld.Modules;
using R2API;
using RoR2;
using UnityEngine;
using MinionMeld.Components;
using HarmonyLib;
using System.Collections.Generic;
using System.Text.RegularExpressions;

[module: UnverifiableCode]
[assembly: HG.Reflection.SearchableAttribute.OptIn]
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
        public const string PluginName = "MinionMeld";
        public const string PluginVersion = "1.1.6";

        public static bool RooInstalled => Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions");

        public static MinionMeldPlugin Instance { get; private set; }

        public static ItemDef meldStackItem;
        public static ItemIndex meldStackIndex => meldStackItem.itemIndex;

        internal Harmony harmonyPatcher;

        public void Awake()
        {
            Instance = this;
            harmonyPatcher = new Harmony(PluginGUID);

            Log.Init(Logger);
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
            ContentAddition.AddItemDef(meldStackItem);

            Hooks.Init();
            TurretHooks.Init();
            MultiEquipDrone.Init();
        }

    }
}
