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
            On.RoR2.Inventory.SetEquipment += Inventory_SetEquipment;
        }

        private static void Inventory_SetEquipment(On.RoR2.Inventory.orig_SetEquipment orig, Inventory self, EquipmentState equipmentState, uint slot)
        {
            // ignore if we arent overwriting a non-minion equip or if the minion equip is empty or if the new state is nothing
            if (!NetworkServer.active || self.GetItemCount(MinionMeldPlugin.meldStackIndex) <= 0 || equipmentState.equipmentIndex == EquipmentIndex.None || self.GetEquipment(slot).equipmentIndex == EquipmentIndex.None)
            {
                orig(self, equipmentState, slot);
                return;
            }

            // ignore if we already have it. nobody will know.
            for (uint i = 0; i < self.equipmentStateSlots.Length; i++)
            {
                if (self.equipmentStateSlots[i].equipmentIndex == equipmentState.equipmentIndex)
                {
                    orig(self, equipmentState, i);
                    return;
                }
            }

            // move that gear up
            for (uint i = (uint)self.equipmentStateSlots.Length; i > slot; i--)
            {
                var state = self.GetEquipment(i - 1);
                if (self.SetEquipmentInternal(state, i))
                {
                    self.SetDirtyBit(16u);

                    self.HandleInventoryChanged();
                    if (self.spawnedOverNetwork)
                    {
                        self.CallRpcClientEquipmentChanged(state.equipmentIndex, i);
                    }
                }
            }

            // let orig set the new state like normal
            orig(self, equipmentState, slot);

            // set the active equipment to the lowest cooldown cuz why not its for equipment drones only essentially idk
            if (self.activeEquipmentSlot != slot)
            {
                var newDef = self.GetEquipment(slot).equipmentDef;
                if (newDef && newDef.cooldown > 0)
                {
                    var currentDef = self.currentEquipmentState.equipmentDef;
                    if (!currentDef || currentDef.cooldown <= 0 || newDef.cooldown < currentDef.cooldown)
                        self.SetActiveEquipmentSlot((byte)slot);
                }
            }
        }

        private static void ActivateAllEquipment(EquipmentSlot self, EquipmentIndex equipmentIndex)
		{
            if (!NetworkServer.active)
                return;

            var inventory = self.characterBody ? self.characterBody.inventory : null;	
			if (!inventory || inventory.GetItemCount(MinionMeldPlugin.meldStackIndex) <= 0) 
                return;

            var slots = inventory.GetEquipmentSlotCount();
            if (slots <= 1)
                return;

			for (uint i = 0; i < slots; i++)
			{
                if (i != inventory.activeEquipmentSlot)
                {
                    var equipmentDef = EquipmentCatalog.GetEquipmentDef(inventory.GetEquipment(i).equipmentIndex);
                    if (equipmentDef && equipmentDef.cooldown > 0)
                    {
                        self.PerformEquipmentAction(equipmentDef);
                    }
                }
			}
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
                    if (body.inventory.GetItemCount(MinionMeldPlugin.meldStackIndex) > 0)
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
				Log.Error("MinionMeld.CharacterBody_OnInventoryChanged: ILHook failed.");
			}
		}
    }
}