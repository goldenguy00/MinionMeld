using MinionMeld.Components;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace MinionMeld.Modules
{
    public class TurretHooks
    {
        public static TurretHooks Instance { get; private set; }

        public static void Init() => Instance ??= new TurretHooks();
        private TurretHooks()
        {
            // spawn interactable on death
            On.EntityStates.Drone.DeathState.OnImpactServer += DeathState_OnImpactServer;

            // give teleporting turret component
            On.RoR2.CharacterMaster.Respawn += CharacterMaster_Respawn;

            // events
            On.RoR2.HalcyoniteShrineInteractable.TrackInteractions += HalcyoniteShrineInteractable_TrackInteractions;
            On.RoR2.HoldoutZoneController.OnEnable += HoldoutZoneController_OnEnable;
            On.RoR2.ScriptedCombatEncounter.BeginEncounter += ScriptedCombatEncounter_BeginEncounter;
        }

        private static CharacterBody CharacterMaster_Respawn(On.RoR2.CharacterMaster.orig_Respawn orig, CharacterMaster self, Vector3 footPosition, Quaternion rotation, bool wasRevivedMidStage)
        {
            var body = orig(self, footPosition, rotation, wasRevivedMidStage);
            if (MeldingTime.CanApplyTurret(self))
                MeldingTime.TryAddTeleTurret(body);

            return body;
        }

        private static void HalcyoniteShrineInteractable_TrackInteractions(On.RoR2.HalcyoniteShrineInteractable.orig_TrackInteractions orig, HalcyoniteShrineInteractable self)
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
        private void DeathState_OnImpactServer(On.EntityStates.Drone.DeathState.orig_OnImpactServer orig, EntityStates.Drone.DeathState self, Vector3 contactPoint)
        {
            var num = 0;
            if (self.characterBody && self.characterBody.master && self.characterBody.master.inventory)
                num = self.characterBody.master.inventory.GetItemCount(MinionMeldPlugin.meldStackItem);

            orig(self, contactPoint);
            for (var i = 0; i < num; i++)
                orig(self, contactPoint);
        }

    }
}
