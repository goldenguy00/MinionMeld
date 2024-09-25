using System;
using System.Collections.Generic;
using UnityEngine;
using RoR2;
using MinionMeld.Modules;

namespace MinionMeld.Components
{
    [RequireComponent(typeof(CharacterBody))]
    public class TeleportingTurret : MonoBehaviour
    {
        private readonly List<Vector3> storedNodes = [];
        private CharacterBody body, ownerBody;
        private float teleportAttemptTimer;
        private Vector3 currentNode;

        public static List<TeleportingTurret> instancesList = [];

        #region Unity Shit
        private void OnEnable()
        {
            instancesList.Add(this);
            this.body = this.GetComponent<CharacterBody>();
            this.storedNodes.Add(this.body.footPosition);
            this.currentNode = this.body.footPosition;
        }

        private void OnDisable()
        {
            instancesList.Remove(this);
            this.currentNode = Vector3.zero;
            this.storedNodes.Clear();
        }

        public void FixedUpdate()
        {
            if (!this.ownerBody)
            {
                var master = this.body.master;
                var ownerMaster = master ? master.minionOwnership.ownerMaster : null;
                this.ownerBody = ownerMaster ? ownerMaster.GetBody() : null;
            }

            this.teleportAttemptTimer -= Time.fixedDeltaTime;
            if (this.teleportAttemptTimer > 0f)
                return;

            teleportAttemptTimer = 1f;
            CheckNodesForTeleport();
        }
        #endregion

        public void RegisterLocation(Vector3 pos)
        {
            foreach (var loc in storedNodes)
            {
                if ((loc - pos).sqrMagnitude < 400f)
                    return;
            }

            this.storedNodes.Add(pos);
            this.teleportAttemptTimer = 0f;
        }

        //todo: coroutine
        private void CheckNodesForTeleport()
        {
            if (!this.ownerBody || !this.body || this.body.healthComponent?.alive != true)
                return;
            
            var closestNode = this.currentNode;
            var ownerPos = this.ownerBody.transform.position;
            var distance = (closestNode - ownerPos).sqrMagnitude;

            if (distance > PluginConfig.minionLeashRange.Value * PluginConfig.minionLeashRange.Value)
            {
                var newNode = MeldingTime.FindSpawnDestination(body, new DirectorPlacementRule() { position = ownerPos, placementMode = DirectorPlacementRule.PlacementMode.Approximate }, RoR2Application.rng);

                if (newNode.HasValue) RegisterLocation(newNode.Value);
                else Log.Error("Teleporting turret was unable to create a valid node");
            }

            foreach (var loc in storedNodes)
            {
                var other = (loc - ownerPos).sqrMagnitude;
                if (other < distance)
                {
                    distance = other;
                    closestNode = loc;
                }
            }

            if (this.currentNode != closestNode)
            {
                TeleportHelper.TeleportGameObject(this.gameObject, closestNode);
                this.transform.rotation = Quaternion.identity;
                this.currentNode = closestNode;
            }
        }

        public void CreateTeleportNode(Vector3 destination)
        {
            if (!this.body || this.body.healthComponent?.alive != true)
                return;

            // if owner is dead, just use a team member instead
            var closest = this.currentNode;
            var distance = (closest - destination).sqrMagnitude;
            foreach (var pcmc in PlayerCharacterMasterController.instances)
            {
                if (pcmc.body)
                {
                    if (pcmc.body == ownerBody)
                    {
                        closest = ownerBody.footPosition;
                    }

                    var other = (pcmc.body.footPosition - destination).sqrMagnitude;
                    if (other < distance)
                    {
                        distance = other;
                        closest = pcmc.body.footPosition;
                    }
                }
            }

            Vector3? target = null;
            if (closest != this.currentNode)
            {
                target = MeldingTime.FindSpawnDestination(body, new DirectorPlacementRule() { position = closest, placementMode = DirectorPlacementRule.PlacementMode.Approximate }, RoR2Application.rng);
            }
            // fallback to creating a node for the destination
            if (!target.HasValue)
            {
                target = MeldingTime.FindSpawnDestination(body, new DirectorPlacementRule() { position = destination, placementMode = DirectorPlacementRule.PlacementMode.Approximate }, RoR2Application.rng);
            }

            if (target.HasValue)
            {
                RegisterLocation(target.Value);
            }
            else
            {
                Log.Error("Failed to create turret teleport node for " + this.body.name);
            }
        }
    }
}
