﻿using BepInEx.Configuration;
using HG;
using MonoMod.Utils;
using RiskOfChaos.ConfigHandling;
using RiskOfChaos.EffectDefinitions;
using RiskOfChaos.EffectHandling.Controllers;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Data;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Methods;
using RiskOfChaos.Utilities;
using RiskOfChaos.Utilities.Extensions;
using RiskOfOptions.OptionConfigs;
using RoR2;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace RiskOfChaos.EffectHandling
{
    public class ChaosEffectInfo : IEquatable<ChaosEffectInfo>
    {
        public readonly ChaosEffectIndex EffectIndex;

        public readonly string Identifier;

        public readonly string NameToken;

        public readonly Type EffectType;

        public readonly string ConfigSectionName;

        readonly ChaosEffectCanActivateMethod[] _canActivateMethods = Array.Empty<ChaosEffectCanActivateMethod>();

        readonly ReadOnlyCollection<TimedEffectInfo> _incompatibleEffects = Empty<TimedEffectInfo>.ReadOnlyCollection;

        public readonly ConfigHolder<bool> IsEnabledConfig;
        readonly ConfigHolder<float> _selectionWeightConfig;

        readonly ConfigHolder<float> _weightReductionPerActivation;

        readonly ConfigHolder<EffectActivationCountMode> _effectRepetitionCountMode;
        public EffectActivationCountMode EffectRepetitionCountMode => _effectRepetitionCountMode.Value;

        readonly ConfigHolder<KeyboardShortcut> _activationShortcut;
        public bool IsActivationShortcutPressed => _activationShortcut != null && _activationShortcut.Value.IsDown();

        readonly EffectWeightMultiplierDelegate[] _effectNameWeightMultipliers = Array.Empty<EffectWeightMultiplierDelegate>();
        public float TotalSelectionWeight
        {
            get
            {
                float weight = _selectionWeightConfig.Value;

                float weightMultiplierPerActivation = 1f - _weightReductionPerActivation.Value;
                if (weightMultiplierPerActivation < 1f)
                {
                    ChaosEffectActivationCounterHandler effectActivationCountHandler = ChaosEffectActivationCounterHandler.Instance;
                    if (effectActivationCountHandler)
                    {
                        weight *= Mathf.Pow(weightMultiplierPerActivation, effectActivationCountHandler.GetEffectActivationCount(this, EffectRepetitionCountMode));
                    }
                }

                foreach (EffectWeightMultiplierDelegate getEffectWeightMultiplier in _effectNameWeightMultipliers)
                {
                    weight *= getEffectWeightMultiplier();
                }

                return weight;
            }
        }

        readonly EffectNameFormatArgsDelegate _getEffectNameFormatArgs;
        public bool HasCustomDisplayNameFormatter => _getEffectNameFormatArgs != null;

        public readonly bool IsNetworked;

        public readonly string[] PreviousConfigSectionNames = Array.Empty<string>();

        public readonly ConfigFile ConfigFile;

        public ChaosEffectInfo(ChaosEffectIndex effectIndex, ChaosEffectAttribute attribute, ConfigFile configFile)
        {
            EffectIndex = effectIndex;
            Identifier = attribute.Identifier;

            NameToken = $"EFFECT_{Identifier.ToUpper()}_NAME";

            if (attribute.target is Type effectType)
            {
                EffectType = effectType;

                EffectConfigBackwardsCompatibilityAttribute configBackwardsCompatibilityAttribute = EffectType.GetCustomAttribute<EffectConfigBackwardsCompatibilityAttribute>();
                if (configBackwardsCompatibilityAttribute != null)
                {
                    PreviousConfigSectionNames = configBackwardsCompatibilityAttribute.ConfigSectionNames;
                }

                if (!typeof(BaseEffect).IsAssignableFrom(effectType))
                {
                    Log.Error($"effect type {effectType.FullName} is not {nameof(BaseEffect)}");
                }
                else
                {
                    MethodInfo[] allMethods = effectType.GetAllMethodsRecursive(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).ToArray();

                    _canActivateMethods = allMethods.WithAttribute<MethodInfo, EffectCanActivateAttribute>()
                                                    .Select(m => new ChaosEffectCanActivateMethod(m))
                                                    .ToArray();

                    _effectNameWeightMultipliers = allMethods.WithAttribute<MethodInfo, EffectWeightMultiplierSelectorAttribute>()
                                                             .Select(m => m.CreateDelegate<EffectWeightMultiplierDelegate>())
                                                             .ToArray();

                    MethodInfo getEffectNameFormatArgsMethod = allMethods.WithAttribute<MethodInfo, EffectNameFormatArgsAttribute>().FirstOrDefault();
                    _getEffectNameFormatArgs = getEffectNameFormatArgsMethod?.CreateDelegate<EffectNameFormatArgsDelegate>();

                    Type[] incompatibleEffectTypes = effectType.GetCustomAttributes<IncompatibleEffectsAttribute>(true)
                                                               .SelectMany(a => a.IncompatibleEffectTypes)
                                                               .ToArray();

                    if (incompatibleEffectTypes.Length > 0)
                    {
                        List<TimedEffectInfo> incompatibleEffects = new List<TimedEffectInfo>(incompatibleEffectTypes.Length);
                        _incompatibleEffects = new ReadOnlyCollection<TimedEffectInfo>(incompatibleEffects);

                        ChaosEffectCatalog.Availability.CallWhenAvailable(() =>
                        {
                            incompatibleEffects.AddRange(ChaosEffectCatalog.AllTimedEffects.Where(e => e != this && incompatibleEffectTypes.Any(t => t.IsAssignableFrom(e.EffectType))));

#if DEBUG
                            Log.Debug($"Initialized incompatibility list for {ChaosEffectCatalog.GetEffectInfo(effectIndex)}: [{string.Join(", ", incompatibleEffects)}]");
#endif
                        });
                    }
                }
            }
            else
            {
                Log.Error($"attribute target is not a Type ({attribute.target})");
            }

            IsNetworked = attribute.IsNetworked;

            ConfigSectionName = "Effect: " + (attribute.ConfigName ?? Language.GetString(NameToken, "en")).FilterConfigKey();

            if (PreviousConfigSectionNames != null && PreviousConfigSectionNames.Length > 0)
            {
                int index = Array.IndexOf(PreviousConfigSectionNames, ConfigSectionName);
                if (index >= 0)
                {
                    ArrayUtils.ArrayRemoveAtAndResize(ref PreviousConfigSectionNames, index);
                }
            }

            ConfigFile = configFile;

            IsEnabledConfig = ConfigFactory<bool>.CreateConfig("Effect Enabled", true)
                                                 .Description("If the effect should be able to be picked")
                                                 .OptionConfig(new CheckBoxConfig())
                                                 .Build();

            _selectionWeightConfig = ConfigFactory<float>.CreateConfig("Effect Weight", attribute.DefaultSelectionWeight)
                                                         .Description("How likely the effect is to be picked, higher value means more likely, lower value means less likely")
                                                         .OptionConfig(new StepSliderConfig
                                                         {
                                                             formatString = "{0:F1}",
                                                             increment = 0.1f,
                                                             min = 0f,
                                                             max = 2.5f
                                                         })
                                                         .ValueConstrictor(CommonValueConstrictors.GreaterThanOrEqualTo(0f))
                                                         .Build();

            _weightReductionPerActivation =
                ConfigFactory<float>.CreateConfig("Effect Repetition Reduction Percentage", attribute.EffectWeightReductionPercentagePerActivation / 100f)
                                    .Description("The percentage reduction to apply to the weight value per activation, setting this to any value above 0 will make the effect less likely to happen several times")
                                    .OptionConfig(new StepSliderConfig
                                    {
                                        formatString = "-{0:P0}",
                                        increment = 0.01f,
                                        min = 0f,
                                        max = 1f
                                    })
                                    .ValueConstrictor(CommonValueConstrictors.Clamped01Float)
                                    .Build();

            _effectRepetitionCountMode =
                ConfigFactory<EffectActivationCountMode>.CreateConfig("Effect Repetition Count Mode", attribute.EffectRepetitionWeightCalculationMode)
                                                        .Description($"Controls how the Reduction Percentage will be applied.\n\n{nameof(EffectActivationCountMode.PerStage)}: Only the activations on the current stage are considered, and the weight reduction is reset on stage start.\n\n{nameof(EffectActivationCountMode.PerRun)}: All activations during the current run are considered.")
                                                        .OptionConfig(new ChoiceConfig())
                                                        .ValueValidator(CommonValueValidators.DefinedEnumValue<EffectActivationCountMode>())
                                                        .Build();

            _activationShortcut =
                ConfigFactory<KeyboardShortcut>.CreateConfig("Activation Shortcut", KeyboardShortcut.Empty)
                                               .Description("A keyboard shortcut that, if pressed, will activate this effect at any time during a run")
                                               .OptionConfig(new KeyBindConfig())
                                               .Build();

            foreach (MemberInfo member in EffectType.GetMembers(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                                                    .WithAttribute<MemberInfo, InitEffectMemberAttribute>())
            {
                foreach (InitEffectMemberAttribute initEffectMember in member.GetCustomAttributes<InitEffectMemberAttribute>())
                {
                    if (initEffectMember.Priority == InitEffectMemberAttribute.InitializationPriority.EffectInfoCreation)
                    {
                        initEffectMember.ApplyTo(member, this);
                    }
                }
            }
        }

        internal virtual void Validate()
        {
            string displayName = GetDisplayName();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                Log.Error($"{this}: Null or empty display name");
            }

            if (displayName == NameToken)
            {
                Log.Error($"{this}: Invalid name token");
            }

            if (Identifier.Any(char.IsUpper))
            {
                Log.Warning($"{this}: Effect identifier has uppercase characters");
            }
        }

        public virtual void BindConfigs()
        {
            IsEnabledConfig?.Bind(this);

            _selectionWeightConfig?.Bind(this);

            _weightReductionPerActivation?.Bind(this);

            _effectRepetitionCountMode?.Bind(this);

            _activationShortcut?.Bind(this);
        }

        public virtual bool CanActivate(in EffectCanActivateContext context)
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return false;
            }

            if (IsEnabledConfig != null && !IsEnabledConfig.Value)
            {
#if DEBUG
                Log.Debug($"effect {Identifier} cannot activate due to: Disabled in config");
#endif
                return false;
            }

            if (_canActivateMethods.Length > 0)
            {
                foreach (ChaosEffectCanActivateMethod canActivateMethod in _canActivateMethods)
                {
                    if (!canActivateMethod.Invoke(context))
                        return false;
                }
            }

            if (TimedChaosEffectHandler.Instance)
            {
                foreach (TimedEffectInfo incompatibleEffect in _incompatibleEffects)
                {
                    if (TimedChaosEffectHandler.Instance.AnyInstanceOfEffectActive(incompatibleEffect, context))
                    {
#if DEBUG
                        Log.Debug($"Effect {this} cannot activate: incompatible effect {incompatibleEffect} is active");
#endif

                        return false;
                    }
                }
            }

            return true;
        }

        public virtual string GetDisplayName(EffectNameFormatFlags formatFlags = EffectNameFormatFlags.All)
        {
            if ((formatFlags & EffectNameFormatFlags.RuntimeFormatArgs) != 0 && HasCustomDisplayNameFormatter)
            {
                return Language.GetStringFormatted(NameToken, _getEffectNameFormatArgs());
            }
            else
            {
                return Language.GetString(NameToken);
            }
        }

        public virtual void OnEffectInstantiatedServer(in CreateEffectInstanceArgs args, BaseEffect effectInstance)
        {
        }

        public override string ToString()
        {
            return Identifier;
        }

        public override bool Equals(object obj)
        {
            return obj is ChaosEffectInfo effectInfo && Equals(effectInfo);
        }

        public bool Equals(ChaosEffectInfo other)
        {
            return other is not null && EffectIndex == other.EffectIndex;
        }

        public override int GetHashCode()
        {
            return -865576688 + EffectIndex.GetHashCode();
        }

        public static bool operator ==(ChaosEffectInfo left, ChaosEffectInfo right)
        {
            if (left is null || right is null)
                return left is null && right is null;

            return left.Equals(right);
        }

        public static bool operator !=(ChaosEffectInfo left, ChaosEffectInfo right)
        {
            return !(left == right);
        }
    }
}
