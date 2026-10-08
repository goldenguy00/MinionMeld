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
            On.RoR2.Inventory.SetEquipment_EquipmentState_uint_uint += Inventory_SetEquipment;
        }

        private static void Inventory_SetEquipment(On.RoR2.Inventory.orig_SetEquipment_EquipmentState_uint_uint orig, Inventory self, EquipmentState equipmentState, uint slot, uint set)
        {
            // ignore if we arent overwriting a non-minion equip or if the minion equip is empty or if the new state is nothing
            if (!NetworkServer.active || self.GetItemCountPermanent(MinionMeldPlugin.meldStackIndex) <= 0)
            {
                orig(self, equipmentState, slot, set);
                return;
            }

            if (equipmentState.equipmentIndex == EquipmentIndex.None || self.GetEquipment(slot, set).equipmentIndex == EquipmentIndex.None || self.HasEquipment(equipmentState.equipmentIndex))
            {
                orig(self, equipmentState, slot, set);
                return;
            }

            self.AddEquipmentSet();
            set = self.FindBestEquipmentSetIndex(false);
            // let orig set the new state like normal
            orig(self, equipmentState, slot, set);

        }

        private static void ActivateAllEquipment(EquipmentSlot self, EquipmentIndex equipmentIndex)
		{
            if (!NetworkServer.active)
                return;

            var inventory = self.characterBody ? self.characterBody.inventory : null;	
			if (!inventory || inventory.GetItemCountPermanent(MinionMeldPlugin.meldStackIndex) <= 0) 
                return;

            for (uint i = 0; i < inventory.GetEquipmentSlotCount(); i++)
            {
                for (uint j = 0; j < inventory.GetEquipmentSetCount(i); j++)
                {
                    if (i != inventory.activeEquipmentSlot && j != inventory.activeEquipmentSet[i])
                    {
                        var equipmentDef = EquipmentCatalog.GetEquipmentDef(inventory.GetEquipment(i, j).equipmentIndex);
                        if (equipmentDef && equipmentDef.cooldown > 0)
                        {
                            self.PerformEquipmentAction(equipmentDef);
                        }
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
                    if (body.inventory.GetItemCountPermanent(MinionMeldPlugin.meldStackIndex) > 0)
                    {
                        for (uint i = 0; i < body.inventory.GetEquipmentSlotCount(); i++)
                        {
                            for (uint j = 0; j < body.inventory.GetEquipmentSetCount(i); j++)
                            {
                                var buffDef = body.inventory.GetEquipment(i, j).equipmentDef?.passiveBuffDef;
                                if (buffDef && !body.HasBuff(buffDef))
                                {
                                    body.AddBuff(buffDef);
                                }
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