using System.Collections.Generic;
using System.Linq;
using KinematicCharacterController;
using MinionMeld.Components;
using RoR2;
using RoR2.Navigation;
using UnityEngine;

namespace MinionMeld.Modules
{
    public static class MeldingTime
    {
        public enum DronemeldPriorityOrder
        {
            RoundRobin,
            Random,
            FirstOnly
        }

        #region Apply
        public static void HandleInventory(DirectorSpawnRequest spawnReq, Inventory inventory, SpawnCard.SpawnResult result)
        {
            var stacks = inventory.GetItemCount(MinionMeldPlugin.meldStackIndex);

            // sneaky
            inventory.itemAcquisitionOrder.Clear();
            inventory.itemStacks = ItemCatalog.RequestItemStackArray();
            inventory.itemAcquisitionOrder.Add(MinionMeldPlugin.meldStackIndex);
            inventory.itemStacks[(int)MinionMeldPlugin.meldStackIndex] = stacks;

            spawnReq.onSpawnedServer?.Invoke(result);

            var newStacks = inventory.GetItemCount(MinionMeldPlugin.meldStackIndex);
            inventory.GiveItem(MinionMeldPlugin.meldStackIndex, stacks - newStacks);
        }
        public static void HandleInventory(MasterSummon self, CharacterMaster newSummon, Inventory inventory)
        {
            var stacks = inventory.GetItemCount(MinionMeldPlugin.meldStackIndex);

            if (self.inventoryToCopy)
            {
                inventory.CopyItemsFrom(self.inventoryToCopy, self.inventoryItemCopyFilter ?? Inventory.defaultItemCopyFilterDelegate);
            }
            else
            {
                inventory.itemAcquisitionOrder.Clear();
                inventory.itemStacks = ItemCatalog.RequestItemStackArray();
                inventory.itemAcquisitionOrder.Add(MinionMeldPlugin.meldStackIndex);
                inventory.itemStacks[(int)MinionMeldPlugin.meldStackIndex] = stacks;
            }

            self.inventorySetupCallback?.SetupSummonedInventory(self, inventory);
            self.preSpawnSetupCallback?.Invoke(newSummon);

            var newStacks = inventory.GetItemCount(MinionMeldPlugin.meldStackIndex);
            inventory.GiveItem(MinionMeldPlugin.meldStackIndex, stacks - newStacks);
        }
        public static CharacterMaster ApplyPerPlayer(MasterCatalog.MasterIndex masterIdx, CharacterMaster summonerMaster)
        {
            List<CharacterMaster> validTargets = [];
            var priority = PluginConfig.priorityOrder.Value;

            var summonerId = summonerMaster.netId;
            if (summonerMaster.minionOwnership.ownerMaster)
                summonerId = summonerMaster.minionOwnership.ownerMaster.netId;

            var minionGroup = MinionOwnership.MinionGroup.FindGroup(summonerId);
            if (minionGroup != null)
            {
                foreach (var member in minionGroup.members)
                {
                    if (!member)
                        continue;

                    var master = member.GetComponent<CharacterMaster>();
                    if (master && master.inventory && master.masterIndex == masterIdx && !master.IsDeadAndOutOfLivesServer())
                    {
                        if (priority == DronemeldPriorityOrder.FirstOnly)
                            return master;

                        validTargets.Add(master);
                    }
                }
            }

            // success
            if (validTargets.Any() && validTargets.Count >= PluginConfig.maxDronesPerType.Value)
            {
                return priority switch
                {
                    DronemeldPriorityOrder.RoundRobin => validTargets.OrderBy(m => m.inventory.GetItemCount(MinionMeldPlugin.meldStackIndex)).FirstOrDefault(),
                    DronemeldPriorityOrder.Random => validTargets.ElementAtOrDefault(Random.Range(0, validTargets.Count)),
                    _ => null
                };
            }

            return null;
        }

        public static CharacterMaster ApplyGlobal(MasterCatalog.MasterIndex masterIdx)
        {
            List<CharacterMaster> validTargets = [];
            var priority = PluginConfig.priorityOrder.Value;

            foreach (var member in TeamComponent.GetTeamMembers(TeamIndex.Player))
            {
                if (!member || !member.body)
                    continue;

                var master = member.body.master;
                if (master && master.inventory && master.masterIndex == masterIdx && !master.IsDeadAndOutOfLivesServer())
                {
                    if (priority == DronemeldPriorityOrder.FirstOnly)
                        return master;

                    validTargets.Add(master);
                }
            }

            // success
            if (validTargets.Any() && validTargets.Count >= PluginConfig.maxDronesPerType.Value)
            {
                return priority switch
                {
                    DronemeldPriorityOrder.RoundRobin => validTargets.OrderBy(m => m.inventory.GetItemCount(MinionMeldPlugin.meldStackIndex)).FirstOrDefault(),
                    DronemeldPriorityOrder.Random => validTargets.ElementAtOrDefault(Random.Range(0, validTargets.Count)),
                    _ => null
                };
            }

            return null;
        }

        public static bool CanApply(MasterCatalog.MasterIndex masterIdx, TeamIndex summonerTeam, TeamIndex? teamIndexOverride)
        {
            if (masterIdx != MasterCatalog.MasterIndex.none && (teamIndexOverride.HasValue ? teamIndexOverride.Value : summonerTeam) == TeamIndex.Player)
            {
                if (PluginConfig.useWhitelist.Value)
                    return PluginConfig.MasterWhitelist.Contains(masterIdx);
                return !PluginConfig.MasterBlacklist.Contains(masterIdx);
            }

            return false;
        }

        public static bool CanApplyTurret(CharacterMaster master)
        {
            if (PluginConfig.teleturret.Value && master.teamIndex == TeamIndex.Player && master.masterIndex != MasterCatalog.MasterIndex.none)
            {
                if (PluginConfig.useWhitelist.Value)
                    return PluginConfig.TurretWhitelist.Contains(master.masterIndex);
                return !PluginConfig.MasterBlacklist.Contains(master.masterIndex) && !PluginConfig.TurretBlacklist.Contains(master.masterIndex);
            }

            return false;
        }

        public static bool PerformMeld(MasterCatalog.MasterIndex masterIdx, CharacterMaster summonerMaster, out CharacterMaster newSummon)
        {
            newSummon = PluginConfig.perPlayer.Value ? ApplyPerPlayer(masterIdx, summonerMaster) : ApplyGlobal(masterIdx);

            if (newSummon)
            {
                newSummon.inventory.GiveItem(MinionMeldPlugin.meldStackIndex);

                if (newSummon.TryGetComponent<MasterSuicideOnTimer>(out var component))
                {
                    newSummon.gameObject.AddComponent<TimedMeldStack>().Activate(component.lifeTimer - component.timer);
                    MonoBehaviour.Destroy(component);
                }

                var itemCount = newSummon.inventory.GetItemCount(RoR2Content.Items.HealthDecay);
                if (itemCount > 0)
                {
                    var body = newSummon.GetBody();
                    if (body && body.healthComponent)
                    {
                        newSummon.gameObject.AddComponent<TimedMeldStack>().Activate(itemCount * body.healthComponent.combinedHealthFraction);

                        var stacks = 1 + newSummon.inventory.GetItemCount(MinionMeldPlugin.meldStackIndex);
                        body.healthComponent.HealFraction(1f / stacks, default);
                    }
                }
            }

            return newSummon != null;
        }
        #endregion

        #region TeleTurret
        public static void InitMinion(CharacterMaster newSummon)
        {
            var body = newSummon.GetBody();
            if (body)
            {
                if (PluginConfig.disableTeamCollision.Value)
                {
                    body.gameObject.layer = LayerIndex.fakeActor.intVal;
                    if (body.characterMotor)
                        body.characterMotor.Motor.RebuildCollidableLayers();
                }

                if (MeldingTime.CanApplyTurret(newSummon))
                    MeldingTime.TryAddTeleTurret(body);
            }
        }
        public static void TryAddTeleTurret(CharacterBody body)
        {
            if (PluginConfig.teleturret.Value && body && !body.GetComponent<TeleportingTurret>())
            {
                if ((!body.characterMotor && !body.GetComponent<KinematicCharacterMotor>() && !body.GetComponent<RigidbodyMotor>()) || body.baseMoveSpeed <= 0f)
                {
                    body.gameObject.AddComponent<TeleportingTurret>();
                }
            }
        }

        public static Vector3? FindSpawnDestination(CharacterBody characterBodyOrPrefabComponent, DirectorPlacementRule rule, Xoroshiro128Plus rng)
        {
            Vector3? result = null;
            SpawnCard spawnCard = ScriptableObject.CreateInstance<SpawnCard>();
            spawnCard.hullSize = characterBodyOrPrefabComponent.hullClassification;
            spawnCard.nodeGraphType = MapNodeGroup.GraphType.Ground;
            spawnCard.prefab = LegacyResourcesAPI.Load<GameObject>("SpawnCards/HelperPrefab");

            var request = new DirectorSpawnRequest(spawnCard, rule, rng);
            var gameObject = DirectorCore.instance.TrySpawnObject(request);

            if (!gameObject)
            {
                if (request.placementRule.placementMode < DirectorPlacementRule.PlacementMode.ApproximateSimple)
                {
                    request.placementRule.placementMode = DirectorPlacementRule.PlacementMode.ApproximateSimple;
                    gameObject = DirectorCore.instance.TrySpawnObject(request);
                }

                if (!gameObject)
                {
                    request.placementRule.placementMode = DirectorPlacementRule.PlacementMode.RandomNormalized;
                    gameObject = DirectorCore.instance.TrySpawnObject(request);
                }
            }

            if (gameObject)
            {
                result = gameObject.transform.position;
                GameObject.Destroy(gameObject);
            }

            ScriptableObject.Destroy(spawnCard);
            return result;
        }
        #endregion
    }
}
