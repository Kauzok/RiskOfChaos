﻿using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RiskOfChaos.EffectHandling;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Methods;
using RiskOfChaos.Utilities;
using RoR2;
using System.Collections.Generic;
using System.Linq;

namespace RiskOfChaos.EffectDefinitions.Character.Player.Items
{
    [ChaosEffect("void_clover_upgrade_random_item", DefaultSelectionWeight = 0.5f, EffectRepetitionWeightCalculationMode = EffectActivationCountMode.PerRun)]
    public sealed class VoidCloverUpgradeRandomItem : BaseEffect
    {
        [HarmonyPatch]
        static class TryCloverVoidUpgradesReversePatch
        {
            [HarmonyPatch(typeof(CharacterMaster), nameof(CharacterMaster.TryCloverVoidUpgrades))]
            [HarmonyReversePatch(HarmonyReversePatchType.Original)]
            public static void TryCloverVoidUpgrades(CharacterMaster instance, int numItemTransformations, Xoroshiro128Plus rng)
            {
                const int NUM_TRANSFORMATIONS_ARG_INDEX = 1;
                const int RNG_ARG_INDEX = 2;

                static void Transpiler(ILContext il)
                {
                    ILCursor c = new ILCursor(il);

#pragma warning disable Publicizer001 // Accessing a member that was not originally public
                    const string CLOVER_VOID_RNG_FIELD_NAME = nameof(CharacterMaster.cloverVoidRng);
#pragma warning restore Publicizer001 // Accessing a member that was not originally public

                    while (c.TryGotoNext(MoveType.After,
                                         x => x.MatchLdarg(0),
                                         x => x.MatchLdfld<CharacterMaster>(CLOVER_VOID_RNG_FIELD_NAME)))
                    {
                        // Removing instructions causes issues with labels, so just pop the value instead
                        c.Emit(OpCodes.Pop);
                        c.Emit(OpCodes.Ldarg, RNG_ARG_INDEX);
                    }

                    c.Index = 0;

                    if (c.TryGotoNext(MoveType.After,
                                      x => x.MatchLdcI4(3),
                                      x => x.MatchMul(),
                                      x => x.MatchStloc(out _)))
                    {
                        c.Index--;
                        c.Emit(OpCodes.Pop);
                        c.Emit(OpCodes.Ldarg, NUM_TRANSFORMATIONS_ARG_INDEX);
                    }
                }

                Transpiler(null);
            }
        }

        [EffectCanActivate]
        static bool CanActivate()
        {
            return ExpansionUtils.DLC1Enabled && PlayerUtils.GetAllPlayerMasters(true).Any(m => m.inventory.HasAtLeastXTotalItemsOfTier(ItemTier.Tier1, 1) || m.inventory.HasAtLeastXTotalItemsOfTier(ItemTier.Tier2, 1));
        }

        public override void OnStart()
        {
            foreach (CharacterMaster playerMaster in PlayerUtils.GetAllPlayerMasters(false))
            {
                upgradeRandomItem(playerMaster, new Xoroshiro128Plus(RNG.nextUlong));
            }
        }

        static void upgradeRandomItem(CharacterMaster playerMaster, Xoroshiro128Plus rng)
        {
            TryCloverVoidUpgradesReversePatch.TryCloverVoidUpgrades(playerMaster, 1, rng);
        }
    }
}