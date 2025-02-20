﻿using BepInEx.Configuration;
using HG;
using RiskOfChaos.EffectDefinitions;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Data;
using RiskOfChaos.Utilities.Extensions;
using RiskOfOptions;
using RiskOfOptions.Options;
using RoR2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RiskOfChaos.EffectHandling
{
    public static class ChaosEffectCatalog
    {
        const string CONFIG_SECTION_NAME = "Effects";

        public const string CONFIG_MOD_GUID = $"RoC_Config_{CONFIG_SECTION_NAME}";
        public const string CONFIG_MOD_NAME = $"Risk of Chaos: {CONFIG_SECTION_NAME}";

        static ConfigFile _effectConfigFile;
        static readonly Sprite _effectsConfigIcon = Configs.GenericIcon;

        public static ResourceAvailability Availability = new ResourceAvailability();

        static ChaosEffectInfo[] _effects;
        public static ReadOnlyArray<ChaosEffectInfo> AllEffects { get; private set; }

        public static ReadOnlyArray<TimedEffectInfo> AllTimedEffects { get; private set; }

        static readonly Dictionary<string, ChaosEffectIndex> _effectIndexByNameToken = new Dictionary<string, ChaosEffectIndex>();
        static readonly Dictionary<Type, ChaosEffectIndex> _effectIndexByType = new Dictionary<Type, ChaosEffectIndex>();

        static int _effectCount;
        public static int EffectCount => _effectCount;

        static readonly WeightedSelection<ChaosEffectInfo> _pickNextEffectSelection = new WeightedSelection<ChaosEffectInfo>();

        internal static void InitConfig(ConfigFile config)
        {
            _effectConfigFile = config;

            if (_effectsConfigIcon)
            {
                ModSettingsManager.SetModIcon(_effectsConfigIcon, CONFIG_MOD_GUID, CONFIG_MOD_NAME);
            }

            ModSettingsManager.SetModDescription("Effect config options for Risk of Chaos", CONFIG_MOD_GUID, CONFIG_MOD_NAME);
        }

        [SystemInitializer]
        static void Init()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            _effects = HG.Reflection.SearchableAttribute.GetInstances<ChaosEffectAttribute>()
                                                        .Cast<ChaosEffectAttribute>()
                                                        .Where(attr => attr.Validate())
                                                        .OrderBy(e => e.Identifier, StringComparer.OrdinalIgnoreCase)
                                                        .Select((e, i) => e.BuildEffectInfo((ChaosEffectIndex)i, _effectConfigFile))
                                                        .ToArray();

            AllEffects = new ReadOnlyArray<ChaosEffectInfo>(_effects);

            AllTimedEffects = new ReadOnlyArray<TimedEffectInfo>(_effects.OfType<TimedEffectInfo>().ToArray());

            foreach (ChaosEffectInfo effect in _effects)
            {
                if (_effectIndexByNameToken.ContainsKey(effect.NameToken))
                {
                    Log.Error($"Duplicate effect name token: {effect.NameToken}");
                }
                else
                {
                    _effectIndexByNameToken.Add(effect.NameToken, effect.EffectIndex);
                }

                if (_effectIndexByType.ContainsKey(effect.EffectType))
                {
                    Log.Error($"Duplicate effect type: {effect.EffectType}");
                }
                else
                {
                    _effectIndexByType.Add(effect.EffectType, effect.EffectIndex);
                }
            }

            _effectCount = _effects.Length;

            _pickNextEffectSelection.Capacity = _effectCount;

            checkFindEffectIndex();

            ChaosEffectInfo[] effectsByConfigName = new ChaosEffectInfo[_effectCount];
            _effects.CopyTo(effectsByConfigName, 0);
            Array.Sort(effectsByConfigName, (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.ConfigSectionName, b.ConfigSectionName));

            foreach (ChaosEffectInfo effectInfo in effectsByConfigName)
            {
                effectInfo.Validate();
                effectInfo.BindConfigs();
            }

            Log.Info($"Registered {_effectCount} effects");

            foreach (ChaosEffectInfo effectInfo in effectsByConfigName)
            {
                foreach (MemberInfo member in effectInfo.EffectType.GetMembers(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly).WithAttribute<MemberInfo, InitEffectMemberAttribute>())
                {
                    foreach (InitEffectMemberAttribute initEffectMember in member.GetCustomAttributes<InitEffectMemberAttribute>())
                    {
                        if (initEffectMember.Priority == InitEffectMemberAttribute.InitializationPriority.EffectCatalogInitialized)
                        {
                            initEffectMember.ApplyTo(member, effectInfo);
                        }
                    }
                }
            }

            Availability.MakeAvailable();

            stopwatch.Stop();
            Log.Info($"Effect catalog initialized in {stopwatch.Elapsed.TotalSeconds:F1} seconds");
        }

        static void checkFindEffectIndex()
        {
            for (ChaosEffectIndex effectIndex = 0; effectIndex < (ChaosEffectIndex)_effectCount; effectIndex++)
            {
                ChaosEffectInfo effectInfo = GetEffectInfo(effectIndex);

                if (FindEffectIndex(effectInfo.Identifier) != effectIndex)
                {
                    Log.Error($"Effect Find Test: {effectInfo.Identifier} failed case-sensitive check");
                }

                if (FindEffectIndex(effectInfo.Identifier.ToUpper()) != effectIndex)
                {
                    Log.Error($"Effect Find Test: {effectInfo.Identifier} failed case-insensitive check");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] PerEffectArray<T>()
        {
            return new T[_effectCount];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChaosEffectInfo GetEffectInfo(ChaosEffectIndex effectIndex)
        {
            return ArrayUtils.GetSafe(_effects, (int)effectIndex);
        }

        public static ChaosEffectIndex FindEffectIndex(string identifier)
        {
            int index = Array.BinarySearch(_effects, identifier, ChaosEffectInfoIdentifierComparer.Instance);

            if (index < 0)
            {
                Log.Warning($"unable to find effect index for identifier '{identifier}'");
                return ChaosEffectIndex.Invalid;
            }

            return (ChaosEffectIndex)index;
        }

        public static ChaosEffectIndex FindEffectIndexByNameToken(string token)
        {
            if (_effectIndexByNameToken.TryGetValue(token, out ChaosEffectIndex effectIndex))
                return effectIndex;

            return ChaosEffectIndex.Invalid;
        }

        public static ChaosEffectIndex FindEffectIndexByType(Type type)
        {
            if (_effectIndexByType.TryGetValue(type, out ChaosEffectIndex effectIndex))
            {
                return effectIndex;
            }
            else
            {
                Log.Error($"{type} is not an effect type");
                return ChaosEffectIndex.Invalid;
            }
        }

        public static ChaosEffectInfo FindEffectInfoByType(Type type)
        {
            ChaosEffectIndex effectIndex = FindEffectIndexByType(type);
            if (effectIndex != ChaosEffectIndex.Invalid)
            {
                return GetEffectInfo(effectIndex);
            }
            else
            {
                return null;
            }
        }

        public static bool IsEffectRelatedToken(string token)
        {
            switch (token)
            {
                case "CHAOS_ACTIVE_EFFECTS_BAR_TITLE":
                case "CHAOS_ACTIVE_EFFECT_UNTIL_STAGE_END_SINGLE_FORMAT":
                case "CHAOS_ACTIVE_EFFECT_UNTIL_STAGE_END_MULTI_FORMAT":
                case "CHAOS_ACTIVE_EFFECT_FIXED_DURATION_FORMAT":
                case "CHAOS_ACTIVE_EFFECT_FIXED_DURATION_LONG_FORMAT":
                case "CHAOS_NEXT_EFFECT_DISPLAY_FORMAT":
                case "CHAOS_NEXT_EFFECT_TIME_REMAINING_DISPLAY_FORMAT":
                case "CHAOS_EFFECT_ACTIVATE":
                case "CHAOS_EFFECT_VOTING_RANDOM_OPTION_NAME":
                case "TIMED_TYPE_UNTIL_STAGE_END_SINGLE_FORMAT":
                case "TIMED_TYPE_UNTIL_STAGE_END_MULTI_FORMAT":
                case "TIMED_TYPE_FIXED_DURATION_FORMAT":
                case "TIMED_TYPE_PERMANENT_FORMAT":
                    return true;
            }

            if (FindEffectIndexByNameToken(token) != ChaosEffectIndex.Invalid)
                return true;

            return false;

        }

        public static WeightedSelection<ChaosEffectInfo> GetAllEnabledEffects(HashSet<ChaosEffectInfo> excludeEffects = null)
        {
            _pickNextEffectSelection.Clear();

            foreach (ChaosEffectInfo effect in _effects)
            {
                if (effect.IsEnabled() && (excludeEffects == null || !excludeEffects.Contains(effect)))
                {
                    _pickNextEffectSelection.AddChoice(effect, effect.TotalSelectionWeight);
                }
            }

            return _pickNextEffectSelection;
        }

        public static ChaosEffectInfo PickEnabledEffect(Xoroshiro128Plus rng, HashSet<ChaosEffectInfo> excludeEffects = null)
        {
            return pickEffectFromSelection(rng, GetAllEnabledEffects(excludeEffects));
        }

        public static WeightedSelection<ChaosEffectInfo> GetAllActivatableEffects(in EffectCanActivateContext context, HashSet<ChaosEffectInfo> excludeEffects = null)
        {
            _pickNextEffectSelection.Clear();

            foreach (ChaosEffectInfo effect in _effects)
            {
                if (effect.CanActivate(context) && (excludeEffects == null || !excludeEffects.Contains(effect)))
                {
                    _pickNextEffectSelection.AddChoice(effect, effect.TotalSelectionWeight);
                }
            }

            return _pickNextEffectSelection;
        }

        public static ChaosEffectInfo PickActivatableEffect(Xoroshiro128Plus rng, in EffectCanActivateContext context, HashSet<ChaosEffectInfo> excludeEffects = null)
        {
            return pickEffectFromSelection(rng, GetAllActivatableEffects(context, excludeEffects));
        }

        static ChaosEffectInfo pickEffectFromSelection(Xoroshiro128Plus rng, WeightedSelection<ChaosEffectInfo> weightedSelection)
        {
            ChaosEffectInfo effect;
            if (weightedSelection.Count > 0)
            {
                float nextNormalizedFloat = rng.nextNormalizedFloat;
                effect = weightedSelection.Evaluate(nextNormalizedFloat);

#if DEBUG
                float effectWeight = weightedSelection.GetChoice(weightedSelection.EvaluateToChoiceIndex(nextNormalizedFloat)).weight;
                Log.Debug($"effect {effect.Identifier} selected, weight={effectWeight} ({weightedSelection.GetSelectionChance(effectWeight):P} chance)");
#endif
            }
            else
            {
                Log.Warning("No activatable effects, defaulting to Nothing");

                effect = Nothing.EffectInfo;
            }

            return effect;
        }
    }
}
