using UnityEngine;

namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// Provides the loot dropper component.
    /// </summary>
    public class LootDropper : MonoBehaviour
    {
        [Header("Loot Configuration")]
        [SerializeField] private LootTable lootTable;

        [Header("Drop Point & Spawn Settings")]
        [SerializeField] private Transform dropPoint;
        [SerializeField] private float fallbackSpawnHeight = 0.35f;
        [SerializeField] private float fallbackSpawnForwardOffset = 0.45f;

        [Header("Drop Force")]
        [SerializeField] private bool applyPopUpForce = true;
        [SerializeField] private float dropForce = 2.5f;

        private bool hasDropped = false;

        /// <summary>
        /// Método público para soltar o loot.
        /// Pode ser chamado por uma Caixa de Pandora, interações, triggers ou destruição do objeto.
        /// </summary>
        public void DropLoot()
        {
            if (hasDropped)
                return;

            if (lootTable == null)
            {
                Debug.LogWarning($"{name}: none LootTable was assigned in the Inspector.");
                return;
            }

            LootItem lootItem = lootTable.GetRandomLoot();

            if (lootItem == null)
            {
                Debug.Log($"{name}: none loot was sorteado (or table empty).");
                return;
            }

            if (lootItem.itemPrefab == null)
            {
                Debug.LogWarning($"{name}: the LootItem '{lootItem.itemName}' does not contain the Prefab assigned.");
                return;
            }

            hasDropped = true;

            // calculates the position of spawn
            Vector3 spawnPosition = dropPoint != null
                ? dropPoint.position
                : transform.position + Vector3.up * fallbackSpawnHeight + transform.forward * fallbackSpawnForwardOffset;

            // Instancia the item sorteado in the world
            GameObject droppedLoot = Instantiate(
                lootItem.itemPrefab,
                spawnPosition,
                Quaternion.identity
            );

            PrepareDroppedLoot(droppedLoot);

            Debug.Log($"{name}: Loot created → {lootItem.itemName}");
        }

        private void PrepareDroppedLoot(GameObject droppedLoot)
        {
            if (droppedLoot == null) return;

            // disables causar damage if the object has ThrowableItem
            var throwable = droppedLoot.GetComponentInChildren<ThrowableItem>(true);
            if (throwable != null)
                throwable.DeactivateDamage();

            // Ajusta the Rigidbody is ter physics
            var rb = droppedLoot.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // applies the pequeno pulo when the soltar (ideal is the box of pandora)
                if (applyPopUpForce)
                {
                    Vector3 forceDirection = Vector3.up * dropForce + transform.forward * (dropForce * 0.3f);
                    rb.AddForce(forceDirection, ForceMode.Impulse);
                }
            }
        }

        /// <summary>
        /// Opcional: Permite resetar a caixa se ela puder ser aberta mais de uma vez.
        /// </summary>
        public void ResetDropper()
        {
            hasDropped = false;
        }
    }
}
