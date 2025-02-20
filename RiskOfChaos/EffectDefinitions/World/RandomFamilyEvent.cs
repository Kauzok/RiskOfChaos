﻿using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Methods;
using RiskOfChaos.Utilities;
using RoR2;
using RoR2.ExpansionManagement;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RiskOfChaos.EffectDefinitions.World
{
    [ChaosEffect("random_family_event", DefaultSelectionWeight = 0.4f)]
    public sealed class RandomFamilyEvent : BaseEffect
    {
        static readonly DccsPool _allFamilyEventsPool = ScriptableObject.CreateInstance<DccsPool>();

        [SystemInitializer(typeof(ExpansionCatalog))]
        static void Init()
        {
            ExpansionDef dlc1 = ExpansionUtils.DLC1;
            ExpansionDef[] dlc1Expansions = new ExpansionDef[] { dlc1 };

            static void loadAndSetPoolEntryDccs(DccsPool.PoolEntry poolEntry, string assetKey)
            {
                AsyncOperationHandle<FamilyDirectorCardCategorySelection> loadAssetHandle = Addressables.LoadAssetAsync<FamilyDirectorCardCategorySelection>(assetKey);
                loadAssetHandle.Completed += handle =>
                {
                    poolEntry.dccs = handle.Result;
                };
            }

            static DccsPool.PoolEntry getPoolEntry(string assetKey, float weight = 1f)
            {
                DccsPool.PoolEntry poolEntry = new DccsPool.PoolEntry
                {
                    weight = weight
                };

                loadAndSetPoolEntryDccs(poolEntry, assetKey);

                return poolEntry;
            }

            static DccsPool.ConditionalPoolEntry getConditionalPoolEntry(string assetKey, ExpansionDef[] requiredExpansions, float weight = 1f)
            {
                DccsPool.ConditionalPoolEntry poolEntry = new DccsPool.ConditionalPoolEntry
                {
                    weight = weight,
                    requiredExpansions = requiredExpansions
                };

                loadAndSetPoolEntryDccs(poolEntry, assetKey);

                return poolEntry;
            }

            DccsPool.Category category = new DccsPool.Category
            {
                name = "AllFamilies",
                categoryWeight = 1f,
                alwaysIncluded = new DccsPool.PoolEntry[]
                {
                    getPoolEntry("RoR2/Base/Common/dccsBeetleFamily.asset"),
                    getPoolEntry("RoR2/Base/Common/dccsGolemFamily.asset"),
                    getPoolEntry("RoR2/Base/Common/dccsImpFamily.asset"),
                    getPoolEntry("RoR2/Base/Common/dccsJellyfishFamily.asset"),
                    getPoolEntry("RoR2/Base/Common/dccsLemurianFamily.asset"),
                    getPoolEntry("RoR2/Base/Common/dccsLunarFamily.asset"),
                    getPoolEntry("RoR2/Base/Common/dccsMushroomFamily.asset"),
                    getPoolEntry("RoR2/Base/Common/dccsParentFamily.asset"),
                    getPoolEntry("RoR2/Base/Common/dccsWispFamily.asset")
                },
                includedIfConditionsMet = new DccsPool.ConditionalPoolEntry[]
                {
                    getConditionalPoolEntry("RoR2/Base/Common/dccsGupFamily.asset", dlc1Expansions),
                    getConditionalPoolEntry("RoR2/DLC1/Common/dccsAcidLarvaFamily.asset", dlc1Expansions),
                    getConditionalPoolEntry("RoR2/DLC1/Common/dccsConstructFamily.asset", dlc1Expansions),
                    getConditionalPoolEntry("RoR2/DLC1/Common/dccsVoidFamily.asset", dlc1Expansions)
                },
                includedIfNoConditionsMet = Array.Empty<DccsPool.PoolEntry>()
            };

#pragma warning disable Publicizer001 // Accessing a member that was not originally public
            _allFamilyEventsPool.poolCategories = new DccsPool.Category[] { category };
#pragma warning restore Publicizer001 // Accessing a member that was not originally public
        }

        static bool _appliedPatches = false;
        static bool _forceAllFamilyEventsAvailable = false;

        static void applyPatchesIfNeeded()
        {
            if (_appliedPatches)
                return;

            On.RoR2.FamilyDirectorCardCategorySelection.IsAvailable += static (orig, self) =>
            {
                return orig(self) || _forceAllFamilyEventsAvailable;
            };

            _appliedPatches = true;
        }

        [EffectCanActivate]
        static bool CanActivate()
        {
            ClassicStageInfo stageInfo = ClassicStageInfo.instance;
#pragma warning disable Publicizer001 // Accessing a member that was not originally public
            return stageInfo && stageInfo.modifiableMonsterCategories is not FamilyDirectorCardCategorySelection;
#pragma warning restore Publicizer001 // Accessing a member that was not originally public
        }

        public override void OnStart()
        {
            applyPatchesIfNeeded();

            ClassicStageInfo stageInfo = ClassicStageInfo.instance;

#pragma warning disable Publicizer001 // Accessing a member that was not originally public
            ref DccsPool monsterDccsPool = ref stageInfo.monsterDccsPool;
#pragma warning restore Publicizer001 // Accessing a member that was not originally public

            DccsPool originalDccsPool = monsterDccsPool;

            monsterDccsPool = _allFamilyEventsPool;

            _forceAllFamilyEventsAvailable = true;
#pragma warning disable Publicizer001 // Accessing a member that was not originally public
            stageInfo.RebuildCards();
#pragma warning restore Publicizer001 // Accessing a member that was not originally public
            _forceAllFamilyEventsAvailable = false;

            monsterDccsPool = originalDccsPool;
        }
    }
}
