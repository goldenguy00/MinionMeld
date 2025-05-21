using MinionMeld.Components;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace MinionMeld.Modules
{
    public static class TurretHooks
    {
        private static bool _initialized;

        public static void Init()
        {
            if (_initialized)
                return;
            _initialized = true;

            // spawn interactable on death
            On.EntityStates.Drone.DeathState.OnImpactServer += DeathState_OnImpactServer;

            // give teleporting turret component
            On.RoR2.CharacterMaster.Respawn += CharacterMaster_Respawn;

            // events
            On.RoR2.HalcyoniteShrineInteractable.TrackInteractions += HalcyoniteShrineInteractable_TrackInteractions;
            On.RoR2.HoldoutZoneController.OnEnable += HoldoutZoneController_OnEnable;
            On.RoR2.ScriptedCombatEncounter.BeginEncounter += ScriptedCombatEncounter_BeginEncounter;
        }

        private static void HalcyoniteShrineInteractable_TrackInteractions(On.RoR2.HalcyoniteShrineInteractable.orig_TrackInteractions orig, HalcyoniteShrineInteractable self)
        {
            orig(self);

            UpdateTurretPositions(self.transform.position);
        }

        private static void HoldoutZoneController_OnEnable(On.RoR2.HoldoutZoneController.orig_OnEnable orig, HoldoutZoneController self)
        {
            orig(self);

            UpdateTurretPositions(self.transform.position);
        }

        private static void ScriptedCombatEncounter_BeginEncounter(On.RoR2.ScriptedCombatEncounter.orig_BeginEncounter orig, ScriptedCombatEncounter self)
        {
            orig(self);

            UpdateTurretPositions(self.transform.position);
        }

        private static void UpdateTurretPositions(Vector3 newPosition)
        {
            if (NetworkServer.active)
            {
                foreach (var drone in TeleportingTurret.instancesList)
                {
                    drone.CreateTeleportNode(newPosition);
                }
            }
        }

        private static CharacterBody CharacterMaster_Respawn(On.RoR2.CharacterMaster.orig_Respawn orig, CharacterMaster self, Vector3 footPosition, Quaternion rotation, bool wasRevivedMidStage)
        {
            var body = orig(self, footPosition, rotation, wasRevivedMidStage);

            if (MeldingTime.CanApplyTurret(self))
                MeldingTime.TryAddTeleTurret(body);

            return body;
        }

        private static void DeathState_OnImpactServer(On.EntityStates.Drone.DeathState.orig_OnImpactServer orig, EntityStates.Drone.DeathState self, Vector3 contactPoint)
        {
            orig(self, contactPoint);

            var inventory = self.characterBody ? self.characterBody.inventory : null;
            if (inventory)
            {
                var stacks = inventory.GetItemCount(MinionMeldPlugin.meldStackIndex);

                for (var i = 1; i < stacks; i++)
                    orig(self, contactPoint);
            }
        }

    }
}
