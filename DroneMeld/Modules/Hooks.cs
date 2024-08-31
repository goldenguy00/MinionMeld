using MinionMeld.Components;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace MinionMeld.Modules
{
    public class Hooks
    {
        public static Hooks Instance { get; private set; }

        public static void Init() => Instance ??= new Hooks();

        private Hooks()
        {
            // hooks
            On.RoR2.CharacterBody.GetDisplayName += CharacterBody_GetDisplayName;
            On.EntityStates.Drone.DeathState.OnImpactServer += DeathState_OnImpactServer;
            On.RoR2.HoldoutZoneController.OnEnable += HoldoutZoneController_OnEnable;
            On.RoR2.ScriptedCombatEncounter.BeginEncounter += ScriptedCombatEncounter_BeginEncounter;
            On.RoR2.CharacterMaster.Respawn += CharacterMaster_Respawn;

            //events
            //CharacterBody.onBodyStartGlobal += CharacterBody_onBodyStartGlobal;
            //On.RoR2.CharacterMaster.OnItemAddedClient += CharacterMaster_OnItemAddedClient;
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;


            On.RoR2.MasterSummon.Perform += MasterSummon_Perform;
            On.RoR2.DirectorCore.TrySpawnObject += DirectorCore_TrySpawnObject;
        }

        private CharacterBody CharacterMaster_Respawn(On.RoR2.CharacterMaster.orig_Respawn orig, CharacterMaster self, Vector3 footPosition, Quaternion rotation, bool wasRevivedMidStage)
        {
            var body = orig(self, footPosition, rotation, wasRevivedMidStage);
            if (!PluginConfig.masterBlacklist.Contains(self.masterIndex))
                MeldingTime.TryAddTeleTurret(self);

            return body;
        }

        private CharacterMaster MasterSummon_Perform(On.RoR2.MasterSummon.orig_Perform orig, MasterSummon self)
        {
            if (!(self.masterPrefab && self.summonerBodyObject && self.summonerBodyObject.TryGetComponent<CharacterBody>(out var summonerBody) && summonerBody.master))
                return orig(self);

            var masterIdx = MasterCatalog.FindMasterIndex(self.masterPrefab);
            if (!MeldingTime.CanApply(masterIdx, summonerBody.teamComponent.teamIndex, self.teamIndexOverride))
                return orig(self);

            if (MeldingTime.Apply(masterIdx, summonerBody.masterObjectId, out var newSummon))
            {
                // turret time
                var newBody = newSummon ? newSummon.GetBody() : null;

                var stacks = newSummon.inventory.GetItemCount(MinionMeldPlugin.meldStackItem);
                if (self.inventoryToCopy)
                {
                    newSummon.inventory.CopyEquipmentFrom(self.inventoryToCopy);
                    newSummon.inventory.CopyItemsFrom(self.inventoryToCopy, self.inventoryItemCopyFilter ?? Inventory.defaultItemCopyFilterDelegate);
                }
                self.inventorySetupCallback?.SetupSummonedInventory(self, newSummon.inventory);

                var newStacks = newSummon.inventory.GetItemCount(MinionMeldPlugin.meldStackItem);
                if (newStacks != stacks)
                    newSummon.inventory.GiveItem(MinionMeldPlugin.meldStackItem, stacks - newStacks);

                if (PluginConfig.teleturret.Value && newBody && newBody.TryGetComponent<TeleportingTurret>(out var teleTurret))
                {
                    teleTurret.RegisterLocation(self.position);
                }
                return null;
            }

            // first guy/dont meld yet but is valid target
            newSummon = orig(self);
            MeldingTime.TryAddTeleTurret(newSummon);

            return newSummon;
        }

        private GameObject DirectorCore_TrySpawnObject(On.RoR2.DirectorCore.orig_TrySpawnObject orig, DirectorCore self, DirectorSpawnRequest spawnReq)
        {
            if (!(spawnReq != null && spawnReq.spawnCard && spawnReq.spawnCard.prefab && spawnReq.summonerBodyObject &&
                spawnReq.summonerBodyObject.TryGetComponent<CharacterBody>(out var summonerBody) && summonerBody.master))
                return orig(self, spawnReq);

            var masterIdx = MasterCatalog.FindMasterIndex(spawnReq.spawnCard.prefab);
            if (!MeldingTime.CanApply(masterIdx, summonerBody.teamComponent.teamIndex, spawnReq.teamIndexOverride))
                return orig(self, spawnReq);

            if (MeldingTime.Apply(masterIdx, summonerBody.masterObjectId, out var newSummon))
            {
                // turret time
                var newBody = newSummon ? newSummon.GetBody() : null;
                if (PluginConfig.teleturret.Value && newBody && newBody.TryGetComponent<TeleportingTurret>(out var teleTurret))
                {
                    var newPos = MeldingTime.FindSpawnDestination(newBody, spawnReq.placementRule, spawnReq.rng);
                    if (newPos.HasValue)
                        teleTurret.RegisterLocation(newPos.Value);
                }
                return null;
            }

            // first guy/dont meld yet but is valid target
            var newSummonObj = orig(self, spawnReq);
            MeldingTime.TryAddTeleTurret(newSummonObj ? newSummonObj.GetComponent<CharacterMaster>() : null);

            return newSummonObj;
        }

        private void HoldoutZoneController_OnEnable(On.RoR2.HoldoutZoneController.orig_OnEnable orig, HoldoutZoneController self)
        {
            orig(self);

            if (NetworkServer.active)
            {
                foreach (var drone in TeleportingTurret.instancesList)
                {
                    drone.CreateTeleportNode(self.transform.position);
                }
            }
        }

        private void ScriptedCombatEncounter_BeginEncounter(On.RoR2.ScriptedCombatEncounter.orig_BeginEncounter orig, ScriptedCombatEncounter self)
        {
            orig(self);

            if (NetworkServer.active)
            {
                foreach (var drone in TeleportingTurret.instancesList)
                {
                    drone.CreateTeleportNode(self.transform.position);
                }
            }
        }
        private string CharacterBody_GetDisplayName(On.RoR2.CharacterBody.orig_GetDisplayName orig, CharacterBody self)
        {
            var text = orig.Invoke(self);
            if (self && self.master && self.master.inventory)
            {
                var itemCount = self.master.inventory.GetItemCount(MinionMeldPlugin.meldStackItem);
                if (itemCount > 0)
                    return $"{text} <style=cStack>x{itemCount + 1}</style>";
            }
            return text;
        }

        private void DeathState_OnImpactServer(On.EntityStates.Drone.DeathState.orig_OnImpactServer orig, EntityStates.Drone.DeathState self, Vector3 contactPoint)
        {
            var num = 0;
            if (self.characterBody && self.characterBody.master && self.characterBody.master.inventory)
                num = self.characterBody.master.inventory.GetItemCount(MinionMeldPlugin.meldStackItem);

            orig(self, contactPoint);
            for (var i = 0; i < num; i++)
                orig(self, contactPoint);
        }

        private void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender && sender.master && sender.master.inventory)
            {
                var itemCount = sender.master.inventory.GetItemCount(MinionMeldPlugin.meldStackItem);
                if (itemCount > 0)
                {
                    args.baseHealthAdd += (sender.baseMaxHealth + (sender.levelMaxHealth * sender.level)) * itemCount * PluginConfig.statMultHealth.Value * 0.01f;
                    args.baseDamageAdd += (sender.baseDamage + (sender.levelDamage * sender.level)) * itemCount * PluginConfig.statMultDamage.Value * 0.01f;
                    args.baseAttackSpeedAdd += (sender.baseAttackSpeed + (sender.levelAttackSpeed * sender.level)) * itemCount * PluginConfig.statMultAttackSpeed.Value * 0.01f;
                    args.cooldownMultAdd += itemCount * PluginConfig.statMultCDR.Value * 0.01f;
                }
            }
        }
    }
}
