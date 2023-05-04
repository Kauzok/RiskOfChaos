﻿using BepInEx.Configuration;
using RiskOfChaos.EffectHandling;
using RiskOfChaos.EffectHandling.Controllers;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Data;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Methods;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace RiskOfChaos.EffectDefinitions.Time
{
    [ChaosEffect("increase_time_scale", ConfigName = "Increase World Speed", EffectWeightReductionPercentagePerActivation = 20f)]
    [ChaosTimedEffect(TimedEffectType.UntilStageEnd)]
    public sealed class IncreaseTimeScale : GenericMultiplyTimeScaleEffect
    {
        [InitEffectInfo]
        static readonly ChaosEffectInfo _effectInfo;

        static ConfigEntry<float> _timeScaleIncreaseConfig;
        const float TIME_SCALE_INCREASE_DEFAULT_VALUE = 0.25f;

        static float timeScaleIncrease
        {
            get
            {
                if (_timeScaleIncreaseConfig == null)
                {
                    return TIME_SCALE_INCREASE_DEFAULT_VALUE;
                }
                else
                {
                    return Mathf.Max(0f, _timeScaleIncreaseConfig.Value);
                }
            }
        }

        [SystemInitializer(typeof(ChaosEffectCatalog))]
        static void Init()
        {
            _timeScaleIncreaseConfig = _effectInfo.BindConfig("World Speed Increase", TIME_SCALE_INCREASE_DEFAULT_VALUE, null);

            _timeScaleIncreaseConfig.SettingChanged += (o, e) =>
            {
                if (!NetworkServer.active || !TimedChaosEffectHandler.Instance)
                    return;

                foreach (IncreaseTimeScale effectInstance in TimedChaosEffectHandler.Instance.GetActiveEffectInstancesOfType<IncreaseTimeScale>())
                {
                    effectInstance.OnValueDirty?.Invoke();
                }
            };

            addConfigOption(new StepSliderOption(_timeScaleIncreaseConfig, new StepSliderConfig
            {
                formatString = "+{0:P0}",
                min = 0f,
                max = 1f,
                increment = 0.01f
            }));
        }

        public override event Action OnValueDirty;

        protected override float multiplier => 1f + timeScaleIncrease;

        [EffectNameFormatArgs]
        static object[] GetDisplayNameFormatArgs()
        {
            return new object[] { timeScaleIncrease };
        }
    }
}
