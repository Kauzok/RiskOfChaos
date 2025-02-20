﻿using RiskOfChaos.Content;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Methods;
using RiskOfChaos.Utilities;
using RoR2;

namespace RiskOfChaos.EffectDefinitions.Character.Buff
{
    [ChaosTimedEffect("everyone_1hp", 30f, AllowDuplicates = false)]
    public sealed class Everyone1Hp : ApplyBuffEffect
    {
        [EffectCanActivate]
        static bool CanActivate()
        {
            return canSelectBuff(Buffs.SetTo1Hp.buffIndex);
        }

        protected override BuffIndex getBuffIndexToApply()
        {
            return Buffs.SetTo1Hp.buffIndex;
        }

        public override void OnStart()
        {
            foreach (CharacterBody playerBody in PlayerUtils.GetAllPlayerBodies(true))
            {
                playerBody.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, 1f);
            }

            base.OnStart();
        }

        protected override void onBuffApplied(CharacterBody body)
        {
            base.onBuffApplied(body);

            HealthComponent healthComponent = body.healthComponent;
            if (healthComponent)
            {
                healthComponent.Networkbarrier = 0f;
            }
        }
    }
}
