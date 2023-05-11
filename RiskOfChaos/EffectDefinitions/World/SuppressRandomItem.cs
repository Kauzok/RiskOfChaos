﻿using RiskOfChaos.EffectHandling;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Methods;
using RiskOfChaos.Utilities;
using RoR2;
using RoR2.Items;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RiskOfChaos.EffectDefinitions.World
{
    [ChaosEffect("suppress_random_item", EffectWeightReductionPercentagePerActivation = 10f, EffectRepetitionWeightCalculationMode = EffectActivationCountMode.PerRun)]
    public sealed class SuppressRandomItem : BaseEffect
    {
        [EffectCanActivate]
        static bool CanActivate()
        {
            return ExpansionUtils.DLC1Enabled && getAllSuppressableItems().Any();
        }

        static IEnumerable<ItemIndex> getAllSuppressableItems()
        {
            if (!Run.instance || Run.instance.availableItems == null)
                return Enumerable.Empty<ItemIndex>();

            return ItemCatalog.allItems.Where(i => getTransformedItemIndex(i) != ItemIndex.None && Run.instance.availableItems.Contains(i));
        }

        static ItemIndex getTransformedItemIndex(ItemIndex suppressedItem)
        {
            if (suppressedItem == ItemIndex.None)
                return ItemIndex.None;

            ItemDef itemDef = ItemCatalog.GetItemDef(suppressedItem);
            if (!itemDef)
                return ItemIndex.None;

            switch (itemDef.tier)
            {
                case ItemTier.Tier1:
                    return DLC1Content.Items.ScrapWhiteSuppressed.itemIndex;
                case ItemTier.Tier2:
                    return DLC1Content.Items.ScrapGreenSuppressed.itemIndex;
                case ItemTier.Tier3:
                    return DLC1Content.Items.ScrapRedSuppressed.itemIndex;
                default:
                    return ItemIndex.None;
            }
        }

        public override void OnStart()
        {
            List<ItemIndex> availableItems = getAllSuppressableItems().ToList();

            ItemIndex suppressedItemIndex;
            ItemIndex transformedItemIndex;
            do
            {
                if (availableItems.Count == 0)
                {
                    Log.Error("No suppressable items found");
                    return;
                }

                int index = RNG.RangeInt(0, availableItems.Count);
                suppressedItemIndex = availableItems[index];
                availableItems.RemoveAt(index);

                transformedItemIndex = getTransformedItemIndex(suppressedItemIndex);
            } while (transformedItemIndex == ItemIndex.None || !SuppressedItemManager.SuppressItem(suppressedItemIndex, transformedItemIndex));

            ItemDef suppressedItem = ItemCatalog.GetItemDef(suppressedItemIndex);

            ItemTierDef itemTierDef = ItemTierCatalog.GetItemTierDef(suppressedItem.tier);
            Chat.SendBroadcastChat(new ColoredTokenChatMessage
            {
                subjectAsCharacterBody = PlayerUtils.GetLocalUserBody(),
                baseToken = "VOID_SUPPRESSOR_USE_MESSAGE",
                paramTokens = new string[]
                {
                    suppressedItem.nameToken
                },
                paramColors = new Color32[]
                {
                    ColorCatalog.GetColor(itemTierDef.colorIndex)
                }
            });
        }
    }
}