﻿using RiskOfChaos.EffectHandling;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.Utilities;
using RiskOfChaos.Utilities.Extensions;
using RoR2;
using UnityEngine.Networking;

namespace RiskOfChaos.EffectDefinitions.Character
{
    [ChaosEffect("max_cooldowns", IsNetworked = true)]
    public sealed class MaxCooldowns : BaseEffect
    {
        public override void OnStart()
        {
            CharacterBody.readOnlyInstancesList.TryDo(body =>
            {
                if (body.hasAuthority)
                {
                    int skillSlotCount = body.skillLocator.skillSlotCount;
                    for (int i = 0; i < skillSlotCount; i++)
                    {
                        GenericSkill skill = body.skillLocator.GetSkillAtIndex(i);
                        if (skill)
                        {
                            skill.RemoveAllStocks();
                        }
                    }
                }

                if (NetworkServer.active)
                {
                    Inventory inventory = body.inventory;
                    if (inventory)
                    {
                        int equipmentSlotCount = inventory.GetEquipmentSlotCount();
                        for (uint i = 0; i < equipmentSlotCount; i++)
                        {
                            EquipmentState equipmentState = inventory.GetEquipment(i);
                            if (equipmentState.equipmentIndex != EquipmentIndex.None)
                            {
#pragma warning disable Publicizer001 // Accessing a member that was not originally public
                                float equipmentCooldown = equipmentState.equipmentDef.cooldown * inventory.CalculateEquipmentCooldownScale();
#pragma warning restore Publicizer001 // Accessing a member that was not originally public

                                inventory.SetEquipment(new EquipmentState(equipmentState.equipmentIndex, Run.FixedTimeStamp.now + equipmentCooldown, 0), i);
                            }
                        }
                    }
                }
            }, FormatUtils.GetBestBodyName);
        }
    }
}
