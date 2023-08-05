﻿using HG;
using RiskOfChaos.ConfigHandling;
using RiskOfChaos.EffectHandling;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Data;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Methods;
using RiskOfChaos.Utilities;
using RiskOfChaos.Utilities.Extensions;
using RiskOfOptions.OptionConfigs;
using RoR2;
using System.Collections.Generic;
using System.Linq;

namespace RiskOfChaos.EffectDefinitions.Character.Player.Items
{
    [ChaosEffect("scrap_random_item", DefaultSelectionWeight = 0.8f, EffectWeightReductionPercentagePerActivation = 15f)]
    public sealed class ScrapRandomItem : BaseEffect
    {
        static PickupIndex[] _scrapPickupByItemTier;

        [SystemInitializer(typeof(PickupCatalog), typeof(ItemTierCatalog))]
        static void InitItemScrapDict()
        {
            int itemTierCount = ItemTierCatalog.allItemTierDefs.Max(itd => (int)itd.tier) + 1;

            _scrapPickupByItemTier = new PickupIndex[itemTierCount];
            for (ItemTier i = 0; i < (ItemTier)itemTierCount; i++)
            {
                _scrapPickupByItemTier[(int)i] = i switch
                {
                    ItemTier.Tier1 => PickupCatalog.FindPickupIndex(RoR2Content.Items.ScrapWhite.itemIndex),
                    ItemTier.Tier2 => PickupCatalog.FindPickupIndex(RoR2Content.Items.ScrapGreen.itemIndex),
                    ItemTier.Tier3 => PickupCatalog.FindPickupIndex(RoR2Content.Items.ScrapRed.itemIndex),
                    ItemTier.Boss => PickupCatalog.FindPickupIndex(RoR2Content.Items.ScrapYellow.itemIndex),
                    _ => PickupIndex.none,
                };
            }
        }

        [EffectConfig]
        static readonly ConfigHolder<bool> _scrapWholeStack =
            ConfigFactory<bool>.CreateConfig("Scrap Whole Stack", true)
                               .Description("If the effect should scrap all items of the selected stack. If this option is disabled, only one item will be turned into scrap, and if it's enabled, it's as if you used a scrapper on that item.")
                               .OptionConfig(new CheckBoxConfig())
                               .Build();

        static IEnumerable<ItemIndex> getAllScrappableItems(Inventory inventory)
        {
            if (!inventory)
                yield break;

            foreach (ItemIndex itemIndex in ItemCatalog.allItems)
            {
                int itemCount = inventory.GetItemCount(itemIndex);
                if (itemCount > 0)
                {
                    ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                    if (itemDef)
                    {
                        ItemTierDef itemTierDef = ItemTierCatalog.GetItemTierDef(itemDef.tier);
                        if (!itemDef.hidden &&
                            itemDef.canRemove &&
                            itemTierDef &&
                            itemTierDef.canScrap &&
                            itemDef.DoesNotContainTag(ItemTag.Scrap))
                        {
                            for (int i = 0; i < itemCount; i++)
                            {
                                yield return itemIndex;
                            }
                        }
                    }
                }
            }
        }

        [EffectCanActivate]
        static bool CanActivate(EffectCanActivateContext context)
        {
            return _scrapPickupByItemTier != null && (!context.IsNow || PlayerUtils.GetAllPlayerMasters(false).Any(cm => getAllScrappableItems(cm.inventory).Any()));
        }

        public override void OnStart()
        {
            PlayerUtils.GetAllPlayerMasters(false).TryDo(tryScrapRandomItem, Util.GetBestMasterName);
        }

        void tryScrapRandomItem(CharacterMaster characterMaster)
        {
            Inventory inventory = characterMaster.inventory;
            if (!inventory)
                return;

            IEnumerable<ItemIndex> scrappableItems = getAllScrappableItems(inventory);
            if (!scrappableItems.Any())
                return;

            ItemDef itemToScrap = ItemCatalog.GetItemDef(RNG.NextElementUniform(scrappableItems.ToArray()));
            scrapItem(characterMaster, inventory, itemToScrap);
        }

        static void scrapItem(CharacterMaster characterMaster, Inventory inventory, ItemDef itemToScrap)
        {
            PickupDef scrapPickup = getScrapPickupForItemTier(itemToScrap.tier);
            if (scrapPickup == null)
                return;

            int itemCount;
            if (_scrapWholeStack.Value)
            {
                itemCount = inventory.GetItemCount(itemToScrap);
            }
            else
            {
                itemCount = 1;
            }

            inventory.RemoveItem(itemToScrap, itemCount);
            inventory.GiveItem(scrapPickup.itemIndex, itemCount);

            CharacterMasterNotificationQueue.PushItemTransformNotification(characterMaster, itemToScrap.itemIndex, scrapPickup.itemIndex, CharacterMasterNotificationQueue.TransformationType.Default);

            Chat.AddPickupMessage(characterMaster.GetBody(), scrapPickup.nameToken, scrapPickup.baseColor, (uint)inventory.GetItemCount(scrapPickup.itemIndex));
        }

        static PickupDef getScrapPickupForItemTier(ItemTier tier)
        {
            return PickupCatalog.GetPickupDef(ArrayUtils.GetSafe(_scrapPickupByItemTier, (int)tier, PickupIndex.none));
        }
    }
}
