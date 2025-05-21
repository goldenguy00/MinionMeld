using System.Linq;
using MinionMeld.Components;
using R2API;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Networking;
using static RoR2.FriendlyFireManager;

namespace MinionMeld.Modules
{
    public class Hooks
    {
        private const string SPAWN_STRING = "{0} | Spawning Meldable Minion: {1}";
        private const string MELD_STRING = "{0} | Performing Minion Meld: {1}";

        public static Hooks Instance { get; private set; }

        public static void Init() => Instance ??= new Hooks();
        private Hooks()
        {
            // hooks
            On.RoR2.CharacterBody.GetDisplayName += CharacterBody_GetDisplayName;

            //events
            CharacterBody.onBodyStartGlobal += CharacterBody_ResizeBody;
            CharacterBody.onBodyInventoryChangedGlobal += CharacterBody_ResizeBody;
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;

            On.RoR2.MasterSummon.Perform += MasterSummon_Perform;
            On.RoR2.DirectorCore.TrySpawnObject += DirectorCore_TrySpawnObject;

            if (PluginConfig.disableProjectileCollision.Value)
            {
                On.RoR2.BulletAttack.DefaultFilterCallbackImplementation += BulletAttack_DefaultFilterCallbackImplementation;
                On.RoR2.Projectile.ProjectileController.IgnoreCollisionsWithOwner += ProjectileController_IgnoreCollisionsWithOwner;
            }
        }

        private static bool BulletAttack_DefaultFilterCallbackImplementation(On.RoR2.BulletAttack.orig_DefaultFilterCallbackImplementation orig, BulletAttack bulletAttack, ref BulletAttack.BulletHit hitInfo)
        {
            return orig(bulletAttack, ref hitInfo)
                && !(hitInfo.hitHurtBox
                && hitInfo.hitHurtBox.healthComponent
                && bulletAttack.owner
                && bulletAttack.owner.TryGetComponent<TeamComponent>(out var attackerTeamComponent)
                && attackerTeamComponent.teamIndex == TeamIndex.Player
                && !FriendlyFireManager.ShouldDirectHitProceed(hitInfo.hitHurtBox.healthComponent, attackerTeamComponent.teamIndex));
        }

        private static void ProjectileController_IgnoreCollisionsWithOwner(On.RoR2.Projectile.ProjectileController.orig_IgnoreCollisionsWithOwner orig, ProjectileController self, bool shouldIgnore)
        {
            orig(self, shouldIgnore);

            if (!shouldIgnore || !PluginConfig.disableProjectileCollision.Value || FriendlyFireManager.friendlyFireMode != FriendlyFireMode.Off ||
                self.teamFilter.teamIndex != TeamIndex.Player || self.myColliders.Length == 0 || !self.owner)
            {
                return;
            }

            foreach (var tc in TeamComponent.GetTeamMembers(TeamIndex.Player))
            {
                if (tc.gameObject != self.owner && tc.body && tc.body.hurtBoxGroup)
                {
                    var hurtBoxes = tc.body.hurtBoxGroup.hurtBoxes;
                    for (int i = 0; i < hurtBoxes.Length; i++)
                    {
                        for (int j = 0; j < self.myColliders.Length; j++)
                        {
                            Physics.IgnoreCollision(hurtBoxes[i].collider, self.myColliders[j], true);
                        } // end for
                    } // end for
                }
            } // endforeach
        }

        private static CharacterMaster MasterSummon_Perform(On.RoR2.MasterSummon.orig_Perform orig, MasterSummon self)
        {
            if (!(self.masterPrefab && self.summonerBodyObject && self.summonerBodyObject.TryGetComponent<CharacterBody>(out var summonerBody) && summonerBody.master))
                return orig(self);

            var masterIdx = MasterCatalog.FindMasterIndex(self.masterPrefab);
            if (!MeldingTime.CanApply(masterIdx, summonerBody.teamComponent.teamIndex, self.teamIndexOverride))
                return orig(self);

            if (MeldingTime.PerformMeld(masterIdx, summonerBody.master, out var newSummon))
            {
                // turret time
                if (PluginConfig.teleturret.Value && newSummon.hasBody && newSummon.bodyInstanceObject.TryGetComponent<TeleportingTurret>(out var teleTurret))
                    teleTurret.RegisterLocation(self.position);
                else if (PluginConfig.respawnSummon.Value)
                    TeleportHelper.TeleportBody(newSummon.GetBody(), self.position, false);

                MeldingTime.HandleInventory(self, newSummon, newSummon.inventory);

                if (PluginConfig.printMasterNames.Value)
                    Log.Info(string.Format(MELD_STRING, nameof(MasterSummon), newSummon.name));
                
                return newSummon;
            }

            // first guy/dont meld yet but is valid target
            newSummon = orig(self);

            if (newSummon)
            {
                MeldingTime.InitMinion(newSummon);

                if (PluginConfig.printMasterNames.Value)
                    Log.Info(string.Format(SPAWN_STRING, nameof(MasterSummon), newSummon.name));
            }

            return newSummon;
        }

        private static GameObject DirectorCore_TrySpawnObject(On.RoR2.DirectorCore.orig_TrySpawnObject orig, DirectorCore self, DirectorSpawnRequest spawnReq)
        {
            if (!(spawnReq?.spawnCard && spawnReq.spawnCard.prefab && spawnReq.summonerBodyObject && spawnReq.placementRule != null &&
                spawnReq.summonerBodyObject.TryGetComponent<CharacterBody>(out var summonerBody) && summonerBody.master))
                return orig(self, spawnReq);

            var masterIdx = MasterCatalog.FindMasterIndex(spawnReq.spawnCard.prefab);
            if (!MeldingTime.CanApply(masterIdx, summonerBody.teamComponent.teamIndex, spawnReq.teamIndexOverride))
                return orig(self, spawnReq);

            if (MeldingTime.PerformMeld(masterIdx, summonerBody.master, out var newSummon))
            {
                // turret time
                var newBody = newSummon.GetBody();
                if (newBody)
                {
                    var newPos = MeldingTime.FindSpawnDestination(newBody, spawnReq.placementRule, spawnReq.rng);
                    if (newPos.HasValue)
                    {
                        if (PluginConfig.teleturret.Value && newBody.TryGetComponent<TeleportingTurret>(out var teleTurret))
                            teleTurret.RegisterLocation(newPos.Value);
                        else if (PluginConfig.respawnSummon.Value)
                            TeleportHelper.TeleportBody(newBody, newPos.Value, false);
                    }

                    MeldingTime.HandleInventory(spawnReq, newSummon.inventory, new SpawnCard.SpawnResult
                    {
                        spawnedInstance = newSummon.gameObject,
                        spawnRequest = spawnReq,
                        position = newBody.footPosition,
                        success = true,
                        rotation = newBody.transform.rotation
                    });
                }

                if (PluginConfig.printMasterNames.Value)
                    Log.Info(string.Format(MELD_STRING, nameof(DirectorCore), newSummon.name));

                return newSummon.gameObject;
            }

            // first guy/dont meld yet but is valid target
            var newSummonObj = orig(self, spawnReq);

            newSummon = newSummonObj ? newSummonObj.GetComponent<CharacterMaster>() : null;
            if (newSummon)
            {
                MeldingTime.InitMinion(newSummon);

                if (PluginConfig.printMasterNames.Value)
                    Log.Info(string.Format(SPAWN_STRING, nameof(DirectorCore), newSummonObj.name));
            }

            return newSummonObj;
        }

        private static void CharacterBody_ResizeBody(CharacterBody body)
        {
            if (PluginConfig.vfxResize.Value > 0 && NetworkClient.active && body && body.inventory)
            {
                var itemCount = body.inventory.GetItemCount(MinionMeldPlugin.meldStackIndex);
                if (itemCount > 0 && body.modelLocator && body.modelLocator.modelTransform)
                {
                    var prefabBody = BodyCatalog.GetBodyPrefab(body.bodyIndex);
                    var prefabModelLoc = prefabBody ? prefabBody.GetComponent<ModelLocator>() : null;
                    if (prefabModelLoc && prefabModelLoc.modelTransform)
                    {
                        var initialSize = prefabModelLoc.modelTransform.localScale;
                        var sizePerStack = itemCount * (PluginConfig.vfxResize.Value * 0.01f);
                        body.modelLocator.modelTransform.localScale = initialSize + (initialSize * sizePerStack);

                    }
                }
            }
        }

        private string CharacterBody_GetDisplayName(On.RoR2.CharacterBody.orig_GetDisplayName orig, CharacterBody self)
        {
            var text = orig.Invoke(self);
            if (self.inventory)
            {
                var itemCount = self.inventory.GetItemCount(MinionMeldPlugin.meldStackIndex);
                if (itemCount > 0)
                    return $"{text} <style=cStack>x{itemCount + 1}</style>";
            }
            return text;
        }

        private void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender && sender.master && sender.master.inventory)
            {
                var itemCount = sender.master.inventory.GetItemCount(MinionMeldPlugin.meldStackIndex);
                if (itemCount > 0)
                {
                    args.baseHealthAdd += (sender.baseMaxHealth + (sender.levelMaxHealth * sender.level)) * itemCount * PluginConfig.statMultHealth.Value * 0.01f;
                    args.baseDamageAdd += (sender.baseDamage + (sender.levelDamage * sender.level)) * itemCount * PluginConfig.statMultDamage.Value * 0.01f;
                    args.baseAttackSpeedAdd += (sender.baseAttackSpeed + (sender.levelAttackSpeed * sender.level)) * itemCount * PluginConfig.statMultAttackSpeed.Value * 0.01f;
                    args.cooldownMultAdd -= Util.ConvertAmplificationPercentageIntoReductionNormalized(itemCount * PluginConfig.statMultCDR.Value * 0.01f);
                }
            }
        }
    }
}
