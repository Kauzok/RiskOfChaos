﻿using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.Utilities;
using RiskOfChaos.Utilities.Extensions;
using RoR2;
using System;
using UnityEngine.Networking;

namespace RiskOfChaos.EffectDefinitions.Character
{
    [ChaosEffect("max_cooldowns", IsNetworked = true)]
    public sealed class MaxCooldowns : BaseEffect
    {
        static BodySkillPair[] _ignoreSkillSlots = Array.Empty<BodySkillPair>();

        [SystemInitializer(typeof(BodyCatalog))]
        static void Init()
        {
            _ignoreSkillSlots = new BodySkillPair[]
            {
                // Railgunner can only get secondary stocks back after shooting while scoped, which you can't do if stocks are 0
                new BodySkillPair("RailgunnerBody", SkillSlot.Secondary)
            };
        }

        static bool canDrainSkill(BodyIndex bodyIndex, SkillSlot slot)
        {
            foreach (BodySkillPair ignoreSkillPair in _ignoreSkillSlots)
            {
                if (ignoreSkillPair.BodyIndex == bodyIndex && ignoreSkillPair.SkillSlot == slot)
                {
                    return false;
                }
            }

            return true;
        }

        public override void OnStart()
        {
            CharacterBody.readOnlyInstancesList.TryDo(body =>
            {
                if (body.hasAuthority)
                {
                    SkillLocator skillLocator = body.skillLocator;

                    int skillSlotCount = skillLocator.skillSlotCount;
                    for (int i = 0; i < skillSlotCount; i++)
                    {
                        GenericSkill skill = skillLocator.GetSkillAtIndex(i);
                        if (skill && canDrainSkill(body.bodyIndex, skillLocator.FindSkillSlot(skill)))
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
