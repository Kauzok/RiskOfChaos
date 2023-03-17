﻿using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RoR2.Artifacts;

namespace RiskOfChaos.EffectDefinitions.World.Spawn
{
    [ChaosEffect("spawn_doppelganger", DefaultSelectionWeight = 0.8f, EffectWeightReductionPercentagePerActivation = 30f)]
    public class SpawnDoppelganger : BaseEffect
    {
        public override void OnStart()
        {
            DoppelgangerInvasionManager.PerformInvasion(new Xoroshiro128Plus(RNG.nextUlong));
        }
    }
}
