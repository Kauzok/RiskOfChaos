﻿using RiskOfChaos.EffectHandling;
using System.Collections.Generic;
using System.Linq;

namespace RiskOfChaos.EffectDefinitions.World.Spawn
{
    public abstract class GenericSpawnEffect<TSpawnType> : BaseEffect
    {
        protected class SpawnEntry
        {
            public readonly float Weight;
            protected readonly TSpawnType[] _items;

            public SpawnEntry(TSpawnType[] items, float weight)
            {
                Weight = weight;
                _items = items;
            }

            public SpawnEntry(TSpawnType item, float weight) : this(new TSpawnType[] { item }, weight)
            {
            }

            public virtual bool IsAvailable()
            {
                return Weight > 0f && _items != null && _items.Length > 0 && _items.Any(isItemAvailable);
            }

            public TSpawnType GetItem(Xoroshiro128Plus rng)
            {
                return rng.NextElementUniform(_items.Where(isItemAvailable).ToArray());
            }

            protected virtual bool isItemAvailable(TSpawnType item)
            {
                return true;
            }

            public override string ToString()
            {
                return $"[ {string.Join(", ", _items)} ]";
            }
        }

        protected static bool areAnyAvailable<TSpawnEntry>(TSpawnEntry[] entries) where TSpawnEntry : SpawnEntry
        {
            return entries != null && entries.Length > 0 && entries.Any(e => e.IsAvailable());
        }

        protected static WeightedSelection<TSpawnEntry> getWeightedEntrySelection<TSpawnEntry>(TSpawnEntry[] entries) where TSpawnEntry : SpawnEntry
        {
            WeightedSelection<TSpawnEntry> interactableSelection = new WeightedSelection<TSpawnEntry>(entries.Length);

            foreach (TSpawnEntry entry in entries)
            {
                if (entry.IsAvailable())
                {
                    interactableSelection.AddChoice(entry, entry.Weight);
                }
            }

            return interactableSelection;
        }

        protected static TSpawnType getItemToSpawn<TSpawnEntry>(TSpawnEntry[] spawnEntries, Xoroshiro128Plus rng) where TSpawnEntry : SpawnEntry
        {
            WeightedSelection<TSpawnEntry> weightedSelection = getWeightedEntrySelection(spawnEntries);

            TSpawnEntry entry = weightedSelection.Evaluate(rng.nextNormalizedFloat);

#if DEBUG
            Log.Debug($"Selected entry {entry}");
#endif

            TSpawnType item = entry.GetItem(rng);

#if DEBUG
            Log.Debug($"Selected item {item}");
#endif

            return item;
        }
    }
}
