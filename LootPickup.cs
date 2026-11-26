using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(Collider))]
    /// <summary>
    /// Provides the loot pickup component.
    /// </summary>
    public class LootPickup : MonoBehaviour
    {
        [Header("Item Config")]
        [SerializeField] private LootItem lootItemData;

        [Header("Pickup Settings")]
        [SerializeField] private bool destroyOnPickup = true;
        [SerializeField] private LayerMask playerLayer;

        private void OnTriggerEnter(Collider other)
        {
            // checks whether who encostou pertence to the layer of the player
            if ((playerLayer.value & (1 << other.gameObject.layer)) != 0 || other.CompareTag("Player"))
            {
                Pickup(other.gameObject);
            }
        }

        private void Pickup(GameObject player)
        {
            if (lootItemData == null)
            {
                Debug.LogWarning($"{name}: LootPickup not has the LootItem assigned.");
                return;
            }

            Debug.Log($"Item coletado: {lootItemData.itemName}");

            // tries add when the inventory of the player (if the component Inventory existir in the Player)
            var playerInventory = player.GetComponent<PlayerInventory>();
            if (playerInventory != null)
            {
                playerInventory.AddItem(lootItemData);
            }

            if (destroyOnPickup)
            {
                Destroy(gameObject);
            }
        }
    }
}
