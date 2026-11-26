using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [CreateAssetMenu(
        fileName = "NewLootTable",
        menuName = "Molimo/Loot/Loot Table"
    )]
    /// <summary>
    /// Provides the loot table component.
    /// </summary>
    public class LootTable : ScriptableObject
    {
        [System.Serializable]
        /// <summary>
        /// Provides the loot entry component.
        /// </summary>
        public class LootEntry
        {
            public LootItem item;

            [Min(0f)]
            public float dropChance = 10f;
        }

        [Header("Loot Items")]
        public List<LootEntry> lootEntries = new List<LootEntry>();

        /// <summary>
        /// Returns random loot.
        /// </summary>
        public LootItem GetRandomLoot()
        {
            if (lootEntries == null || lootEntries.Count == 0)
                return null;

            float totalChance = 0f;

            foreach (LootEntry entry in lootEntries)
            {
                if (entry.item != null && entry.dropChance > 0f)
                {
                    totalChance += entry.dropChance;
                }
            }

            if (totalChance <= 0f)
                return null;

            float randomValue = Random.Range(0f, totalChance);

            foreach (LootEntry entry in lootEntries)
            {
                if (entry.item == null || entry.dropChance <= 0f)
                    continue;

                randomValue -= entry.dropChance;

                if (randomValue <= 0f)
                    return entry.item;
            }

            return null;
        }
    }
}
