using System.Collections.Generic;
using System.Linq;
using KinematicCharacterController;
using MinionMeld.Components;
using RoR2;
using RoR2.Navigation;
using UnityEngine;
using UnityEngine.Networking;

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
        public static CharacterMaster ApplyPerPlayer(MasterCatalog.MasterIndex masterIdx, NetworkInstanceId summonerId)
        {
            List<CharacterMaster> validTargets = [];
            var priority = PluginConfig.priorityOrder.Value;

            var minionGroup = MinionOwnership.MinionGroup.FindGroup(summonerId);
            if (minionGroup != null)
            {
                foreach (var member in minionGroup.members)
                {
                    if (!member)
                        continue;

                    var master = member.GetComponent<CharacterMaster>();
                    if (master && master.inventory && master.masterIndex == masterIdx)
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
                    DronemeldPriorityOrder.RoundRobin => validTargets.OrderBy(m => m.inventory.GetItemCount(MinionMeldPlugin.meldStackItem)).FirstOrDefault(),
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
                if (master && master.inventory && master.masterIndex == masterIdx)
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
                    DronemeldPriorityOrder.RoundRobin => validTargets.OrderBy(m => m.inventory.GetItemCount(MinionMeldPlugin.meldStackItem)).FirstOrDefault(),
                    DronemeldPriorityOrder.Random => validTargets.ElementAtOrDefault(Random.Range(0, validTargets.Count)),
                    _ => null
                };
            }

            return null;
        }

        public static bool CanApply(MasterCatalog.MasterIndex masterIdx, TeamIndex summonerTeam, TeamIndex? teamIndexOverride)
        {
            if (masterIdx == MasterCatalog.MasterIndex.none ||
                PluginConfig.masterBlacklist.Contains(masterIdx))
                return false;

            return (teamIndexOverride.HasValue ? teamIndexOverride.Value : summonerTeam) == TeamIndex.Player;
        }

        public static bool Apply(MasterCatalog.MasterIndex masterIdx, NetworkInstanceId summonerId, out CharacterMaster newSummon)
        {
            newSummon = PluginConfig.perPlayer.Value ? ApplyPerPlayer(masterIdx, summonerId) : ApplyGlobal(masterIdx);

            if (newSummon)
            {
                newSummon.inventory.GiveItem(MinionMeldPlugin.meldStackItem);

                if (newSummon.TryGetComponent<MasterSuicideOnTimer>(out var component))
                {
                    newSummon.gameObject.AddComponent<TimedMeldStack>().Activate(component.lifeTimer - component.timer);
                    component.timer = 0f;
                }

                var itemCount = newSummon.inventory.GetItemCount(RoR2Content.Items.HealthDecay);
                if (itemCount > 0)
                {
                    var body = newSummon.GetBody();
                    if (body && body.healthComponent)
                    {
                        var stacks = 1 + newSummon.inventory.GetItemCount(MinionMeldPlugin.meldStackItem);
                        body.healthComponent.HealFraction(1f / stacks, default);
                        newSummon.gameObject.AddComponent<TimedMeldStack>().Activate(itemCount * body.healthComponent.combinedHealthFraction);
                    }
                }
            }

            return newSummon != null;
        }
        #endregion

        #region TeleTurret
        public static void TryAddTeleTurret(CharacterMaster newSummon)
        {
            var body = newSummon ? newSummon.GetBody() : null;
            if (PluginConfig.teleturret.Value && body && !body.GetComponent<TeleportingTurret>() && !PluginConfig.turretBlacklist.Contains(newSummon.masterIndex))
            {
                if ((!body.characterMotor && !body.GetComponent<KinematicCharacterMotor>() && !body.GetComponent<RigidbodyMotor>()) || body.baseMoveSpeed <= 0f)
                {
                    body.gameObject.AddComponent<TeleportingTurret>();
                }
            }
        }
        #endregion

        public static Vector3? FindSpawnDestination(CharacterBody characterBodyOrPrefabComponent, DirectorPlacementRule rule, Xoroshiro128Plus rng)
        {
            if (rule == null || rule == default)
                return null;

            Vector3? result = null;
            SpawnCard spawnCard = ScriptableObject.CreateInstance<SpawnCard>();
            spawnCard.hullSize = characterBodyOrPrefabComponent.hullClassification;
            spawnCard.nodeGraphType = MapNodeGroup.GraphType.Ground;
            spawnCard.prefab = LegacyResourcesAPI.Load<GameObject>("SpawnCards/HelperPrefab");

            var gameObject = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, rule, rng));

            if (!gameObject)
            {
                rule.placementMode = DirectorPlacementRule.PlacementMode.NearestNode;
                gameObject = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, rule, rng));

                if (!gameObject)
                {
                    rule.placementMode = DirectorPlacementRule.PlacementMode.RandomNormalized;
                    gameObject = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, rule, rng));
                }
            }

            if (gameObject)
            {
                result = gameObject.transform.position;
                Object.Destroy(gameObject);
            }

            Object.Destroy(spawnCard);
            return result;
        }
    }
}
