using RoR2;
using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine.Networking;

namespace MinionMeld.Components
{
	// welcome to hell
    public class MultiEquipDrone
    {
        public static MultiEquipDrone Instance { get; private set; }

        public static void Init() => Instance ??= new MultiEquipDrone();
		private MultiEquipDrone()
        {
            EquipmentSlot.onServerEquipmentActivated += ActivateAllEquipment;
            IL.RoR2.CharacterBody.OnInventoryChanged += CharacterBody_OnInventoryChanged;
            On.RoR2.EquipmentSlot.Update += EquipmentSlot_Update;
            On.RoR2.Inventory.SetEquipment += Inventory_SetEquipment;
        }

        private static void Inventory_SetEquipment(On.RoR2.Inventory.orig_SetEquipment orig, Inventory self, EquipmentState equipmentState, uint slot)
        {
            if (NetworkServer.active && self.GetItemCount(MinionMeldPlugin.meldStackItem) > 0)
            {
                var state = self.GetEquipment(slot);
                if (state.equipmentIndex != EquipmentIndex.None && equipmentState.equipmentIndex != EquipmentIndex.None && state.equipmentIndex != equipmentState.equipmentIndex)
                {
                    for (uint i = (uint)self.equipmentStateSlots.Length; i > slot; i--)
                    {
                        state = self.GetEquipment(i - 1);
                        if (self.SetEquipmentInternal(state, i))
                        {
                            if (NetworkServer.active)
                            {
                                self.SetDirtyBit(16u);
                            }

                            self.HandleInventoryChanged();
                            if (self.spawnedOverNetwork)
                            {
                                self.CallRpcClientEquipmentChanged(state.equipmentIndex, i);
                            }
                        }
                    }
                }
            }

            orig(self, equipmentState, slot);
        }

        private static void ActivateAllEquipment(EquipmentSlot self, EquipmentIndex equipmentIndex)
		{
            if (self.stock > 0)
                return;

            var inventory = self.characterBody ? self.characterBody.inventory : null;	
			if (!inventory || inventory.GetItemCount(MinionMeldPlugin.meldStackItem) <= 0) 
                return;

            var slots = inventory.GetEquipmentSlotCount();
            if (slots <= 1)
                return;

            var index = inventory.activeEquipmentSlot;
		    var lowestCooldown = float.PositiveInfinity;
			for (uint i = 0; i < slots; i++)
			{
				var state = inventory.GetEquipment(i);
				if (i != inventory.activeEquipmentSlot && state.equipmentIndex != EquipmentIndex.None)
				{
					var equipmentDef = EquipmentCatalog.GetEquipmentDef(state.equipmentIndex);
					if(equipmentDef.cooldown > 0 && self.PerformEquipmentAction(equipmentDef) &&  equipmentDef.cooldown < lowestCooldown)
                    {
						lowestCooldown = equipmentDef.cooldown;
                        index = (byte)i;
                    }
				}
			}
            if (index != inventory.activeEquipmentSlot)
                inventory.SetActiveEquipmentSlot(index);
		}


		//vanilla only adds passivebuffdef from active equipment slot
		//if body has composite injector, we want them from all equipment slots
		// hook runs after OnEquipmentLost and OnEquipmentGained, and before adding itembehaviors from elite buffs
		private void CharacterBody_OnInventoryChanged(ILContext il)
		{
			var c = new ILCursor(il);
			if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(0),
                x => x.MatchLdcI4(1),
                x => x.MatchStfld<CharacterBody>(nameof(CharacterBody.statsDirty))))
			{
				c.Emit(OpCodes.Ldarg_0); //body
				c.EmitDelegate<Action<CharacterBody>>((body) =>
				{
                    if (body.inventory.GetItemCount(MinionMeldPlugin.meldStackItem) > 0)
                    {
                        for (uint i = 0; i < body.inventory.GetEquipmentSlotCount(); i++)
                        {
                            var buffDef = body.inventory.GetEquipment(i).equipmentDef?.passiveBuffDef;
                            if (buffDef && !body.HasBuff(buffDef))
                            {
                                body.AddBuff(buffDef);
                            }
                        }
                    }
				});
			}
			else
			{
				Log.Warning("CompositeInjector.CharacterBody_OnEquipmentLost: ILHook failed.");
			}

		}

		// display indicator of the first equipment that uses one
		// would be cool to get all the indicators but it would be very annoying to do
		private void EquipmentSlot_Update(On.RoR2.EquipmentSlot.orig_Update orig, EquipmentSlot self)
		{
			orig(self);

            if (self.inventory?.GetItemCount(MinionMeldPlugin.meldStackItem) > 0 && self.targetIndicator != null)
            {
				self.targetIndicator.active = false;
                for (uint i = 0; i < self.inventory.GetEquipmentSlotCount(); i++)
                {
					var state = self.inventory.GetEquipment(i);
					if (state.equipmentIndex != self.equipmentIndex)
					{
						self.UpdateTargets(state.equipmentIndex, self.stock > 0);
					}

					if (self.targetIndicator.active)
                        break; // use the indicator of the first equipment that uses one			
                }
            }
        }
    }
}