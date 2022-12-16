﻿using BepInEx.Configuration;
using RiskOfChaos.EffectDefinitions;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RiskOfChaos.EffectHandling
{
    public readonly struct ChaosEffectInfo
    {
        public readonly int EffectIndex;

        public readonly string Identifier;

        public readonly string NameToken;
        // public readonly string DescriptionToken;

        public readonly Type EffectType;

        public readonly string ConfigSectionName;

        public readonly ConfigEntry<bool> IsEnabledConfig;
        public readonly ConfigEntry<float> SelectionWeightConfig;

        readonly MethodInfo[] _canActivateMethods;
        public bool CanActivate => _canActivateMethods != null && (_canActivateMethods.Length == 0 || _canActivateMethods.All(m => (bool)m.Invoke(null, null)));

        public readonly float EffectRepetitionWeightExponentMultiplier;
        public readonly EffectActivationCountMode EffectRepetitionWeightCalculationMode;

        readonly MethodInfo[] _weightMultSelectorMethods;
        public float TotalSelectionWeight
        {
            get
            {
                int activationCount = ChaosEffectDispatcher.GetEffectActivationCount(EffectIndex, EffectRepetitionWeightCalculationMode);
                float weight = Mathf.Exp(-activationCount * EffectRepetitionWeightExponentMultiplier) * SelectionWeightConfig.Value;

                if (_weightMultSelectorMethods != null)
                {
                    foreach (MethodInfo weightSelector in _weightMultSelectorMethods)
                    {
                        weight *= (float)weightSelector.Invoke(null, null);
                    }
                }

                return weight;
            }
        }

        readonly MethodInfo _getEffectNameFormatArgsMethod;
        public string DisplayName
        {
            get
            {
                if (_getEffectNameFormatArgsMethod != null)
                {
                    object[] args = (object[])_getEffectNameFormatArgsMethod.Invoke(null, null);
                    return Language.GetStringFormatted(NameToken, args);
                }
                else
                {
                    return Language.GetString(NameToken);
                }
            }
        }

        public ChaosEffectInfo(int effectIndex, ChaosEffectAttribute attribute)
        {
            const string LOG_PREFIX = $"{nameof(ChaosEffectInfo)}..ctor ";

            EffectIndex = effectIndex;
            Identifier = attribute.Identifier;

            NameToken = $"EFFECT_{Identifier.ToUpper()}_NAME";
            // DescriptionToken = attribute.HasDescription ? $"EFFECT_{Identifier.ToUpper()}_DESC" : null;

            if (attribute.target is Type effectType)
            {
                EffectType = effectType;

                if (!typeof(BaseEffect).IsAssignableFrom(effectType))
                {
                    Log.Error(LOG_PREFIX + $"effect type {effectType.FullName} is not {nameof(BaseEffect)}");
                }
                else
                {
                    const BindingFlags FLAGS = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

                    _canActivateMethods = effectType.GetMethods(FLAGS).Where(m => m.GetParameters().Length == 0 && m.GetCustomAttribute<EffectCanActivateAttribute>() != null).ToArray();

                    _weightMultSelectorMethods = effectType.GetMethods(FLAGS).Where(m => m.GetCustomAttribute<EffectWeightMultiplierSelectorAttribute>() != null).ToArray();

                    _getEffectNameFormatArgsMethod = effectType.GetMethods(FLAGS).Where(m => m.GetCustomAttribute<EffectNameFormatArgsAttribute>() != null).FirstOrDefault();
                }
            }
            else
            {
                Log.Error(LOG_PREFIX + $"attribute target is not a Type ({attribute.target})");
            }

            EffectRepetitionWeightExponentMultiplier = attribute.EffectRepetitionWeightExponent / 100f;
            EffectRepetitionWeightCalculationMode = attribute.EffectRepetitionWeightCalculationMode;

            ConfigSectionName = "Effect: " + (attribute.ConfigName ?? Language.GetString(NameToken, "en"));

            IsEnabledConfig = Main.Instance.Config.Bind(new ConfigDefinition(ConfigSectionName, "Effect Enabled"), true, new ConfigDescription("If the effect should be able to be picked"));
            ChaosEffectCatalog.AddEffectConfigOption(new CheckBoxOption(IsEnabledConfig));

            SelectionWeightConfig = Main.Instance.Config.Bind(new ConfigDefinition(ConfigSectionName, "Effect Weight"), attribute.DefaultSelectionWeight, new ConfigDescription("How likely the effect is to be picked, higher value means more likely, lower value means less likely"));
            ChaosEffectCatalog.AddEffectConfigOption(new StepSliderOption(SelectionWeightConfig, new StepSliderConfig
            {
                formatString = "{0:F1}",
                increment = 0.1f,
                min = 0f,
                max = 2.5f
            }));
        }

        public readonly BaseEffect InstantiateEffect(Xoroshiro128Plus effectRNG)
        {
            BaseEffect effectInstance = (BaseEffect)Activator.CreateInstance(EffectType);
            effectInstance.RNG = effectRNG;
            return effectInstance;
        }

        public readonly string GetActivationMessage()
        {
            return Language.GetStringFormatted("CHAOS_EFFECT_ACTIVATE", DisplayName);
        }
    }
}
