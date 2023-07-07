﻿using RiskOfChaos.Components;
using RiskOfChaos.EffectHandling;
using RiskOfChaos.EffectHandling.Controllers;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Data;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Methods;
using RiskOfChaos.Utilities;
using RoR2;

namespace RiskOfChaos.EffectDefinitions.World.Pickups
{
    [ChaosEffect("attract_pickups")]
    [ChaosTimedEffect(90f, AllowDuplicates = false)]
    [IncompatibleEffects(typeof(RepulsePickups))]
    public sealed class AttractPickups : GenericAttractPickupsEffect
    {
        [InitEffectInfo]
        public static readonly ChaosEffectInfo EffectInfo;

        public override void OnStart()
        {
            base.OnStart();

            ItemTierPickupRulesOverride.OverrideRules = ItemTierDef.PickupRules.Default;
        }

        public override void OnEnd()
        {
            base.OnEnd();

            ItemTierPickupRulesOverride.OverrideRules = null;
        }

        protected override void onAttractorComponentAdded(AttractToPlayers attractToPlayers)
        {
            attractToPlayers.MaxSpeed = 0.5f;
            attractToPlayers.Acceleration = 1f;
            attractToPlayers.MaxDistance = 20f;
            attractToPlayers.MinVelocityTreshold = 0f;

            attractToPlayers.DynamicFrictionOverride = 0.1f;
        }
    }
}