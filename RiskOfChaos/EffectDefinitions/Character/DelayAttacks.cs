﻿using RiskOfChaos.ConfigHandling;
using RiskOfChaos.EffectHandling.Controllers;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Data;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Methods;
using RiskOfChaos.ModifierController.AttackDelay;
using RiskOfOptions.OptionConfigs;
using System;
using UnityEngine.Networking;

namespace RiskOfChaos.EffectDefinitions.Character
{
    [ChaosTimedEffect("delay_attacks", 90f)]
    public sealed class DelayAttacks : TimedEffect, IAttackDelayModificationProvider
    {
        [EffectConfig]
        static readonly ConfigHolder<float> _attackDelay =
            ConfigFactory<float>.CreateConfig("Attack Delay", 0.5f)
                                .OptionConfig(new StepSliderConfig
                                {
                                    formatString = "{0:F2}s",
                                    increment = 0.1f,
                                    min = 0f,
                                    max = 2f
                                })
                                .ValueConstrictor(CommonValueConstrictors.GreaterThanOrEqualTo(0f))
                                .OnValueChanged(() =>
                                {
                                    if (!NetworkServer.active || !TimedChaosEffectHandler.Instance)
                                        return;

                                    TimedChaosEffectHandler.Instance.InvokeEventOnAllInstancesOfEffect<DelayAttacks>(e => e.OnValueDirty);
                                })
                                .Build();

        [EffectCanActivate]
        static bool CanActivate()
        {
            return AttackDelayModificationManager.Instance;
        }

        public event Action OnValueDirty;

        public override void OnStart()
        {
            AttackDelayModificationManager.Instance.RegisterModificationProvider(this);
        }

        public override void OnEnd()
        {
            if (AttackDelayModificationManager.Instance)
            {
                AttackDelayModificationManager.Instance.UnregisterModificationProvider(this);
            }
        }

        public void ModifyValue(ref AttackDelayModificationInfo value)
        {
            value.TotalDelay += _attackDelay.Value;
        }
    }
}
