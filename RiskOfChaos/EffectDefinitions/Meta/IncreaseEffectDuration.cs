﻿using RiskOfChaos.ConfigHandling;
using RiskOfChaos.EffectHandling;
using RiskOfChaos.EffectHandling.Controllers;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Data;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Methods;
using RiskOfChaos.EffectHandling.Formatting;
using RiskOfChaos.ModifierController.Effect;
using RiskOfOptions.OptionConfigs;
using System;
using UnityEngine.Networking;

namespace RiskOfChaos.EffectDefinitions.Meta
{
    [ChaosTimedEffect("increase_effect_duration", TimedEffectType.UntilStageEnd, ConfigName = "Increase Effect Duration", DefaultStageCountDuration = 2, IgnoreDurationModifiers = true)]
    public sealed class IncreaseEffectDuration : TimedEffect, IEffectModificationProvider
    {
        [EffectConfig]
        static readonly ConfigHolder<float> _durationMultiplier =
            ConfigFactory<float>.CreateConfig("Effect Duration Multiplier", 2f)
                                .OptionConfig(new StepSliderConfig
                                {
                                    formatString = "{0}x",
                                    min = 1f,
                                    max = 5f,
                                    increment = 0.1f
                                })
                                .ValueConstrictor(CommonValueConstrictors.GreaterThanOrEqualTo(1f))
                                .OnValueChanged(() =>
                                {
                                    if (!NetworkServer.active || !TimedChaosEffectHandler.Instance)
                                        return;

                                    TimedChaosEffectHandler.Instance.InvokeEventOnAllInstancesOfEffect<IncreaseEffectDuration>(e => e.OnValueDirty);
                                })
                                .Build();

        [EffectCanActivate]
        static bool CanActivate()
        {
            return EffectModificationManager.Instance;
        }

        [GetEffectNameFormatter]
        static EffectNameFormatter GetNameFormatter()
        {
            return new EffectNameFormatter_GenericFloat(_durationMultiplier.Value);
        }

        public event Action OnValueDirty;

        public override void OnStart()
        {
            EffectModificationManager.Instance.RegisterModificationProvider(this);

            if (TimedChaosEffectHandler.Instance)
            {
                foreach (TimedEffect effect in TimedChaosEffectHandler.Instance.GetAllActiveEffects())
                {
                    if (effect.EffectInfo.IgnoreDurationModifiers)
                        continue;

                    effect.MaxStocks *= _durationMultiplier.Value;
                }
            }
        }

        public void ModifyValue(ref EffectModificationInfo value)
        {
            value.DurationMultiplier *= _durationMultiplier.Value;
        }

        public override void OnEnd()
        {
            if (EffectModificationManager.Instance)
            {
                EffectModificationManager.Instance.UnregisterModificationProvider(this);
            }
        }
    }
}
