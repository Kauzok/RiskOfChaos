﻿using RiskOfChaos.ConfigHandling;
using RiskOfChaos.EffectHandling;
using RiskOfChaos.EffectHandling.Controllers;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Data;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Methods;
using RiskOfChaos.EffectHandling.Formatting;
using RiskOfOptions.OptionConfigs;
using System;
using UnityEngine.Networking;

namespace RiskOfChaos.EffectDefinitions.World.ProjectileSpeed
{
    [ChaosTimedEffect("decrease_projectile_speed", TimedEffectType.UntilStageEnd, ConfigName = "Decrease Projectile Speed")]
    public sealed class DecreaseProjectileSpeed : GenericProjectileSpeedEffect
    {
        [EffectConfig]
        static readonly ConfigHolder<float> _projectileSpeedDecrease =
            ConfigFactory<float>.CreateConfig("Projectile Speed Decrease", 0.5f)
                                .OptionConfig(new StepSliderConfig
                                {
                                    formatString = "-{0:P0}",
                                    min = 0f,
                                    max = 1f,
                                    increment = 0.01f
                                })
                                .ValueConstrictor(CommonValueConstrictors.Clamped01Float)
                                .OnValueChanged(() =>
                                {
                                    if (!NetworkServer.active || !TimedChaosEffectHandler.Instance)
                                        return;

                                    TimedChaosEffectHandler.Instance.InvokeEventOnAllInstancesOfEffect<DecreaseProjectileSpeed>(e => e.OnValueDirty);
                                })
                                .Build();

        public override event Action OnValueDirty;

        [GetEffectNameFormatter]
        static EffectNameFormatter GetNameFormatter()
        {
            return new EffectNameFormatter_GenericFloat(_projectileSpeedDecrease.Value) { ValueFormat = "P0" };
        }

        protected override float speedMultiplier => 1f - _projectileSpeedDecrease.Value;
    }
}
