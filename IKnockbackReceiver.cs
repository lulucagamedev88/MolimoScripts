using UnityEngine;

namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// Qualquer coisa que possa ser empurrada por um golpe.
    /// O WeaponHitBox procura esta interface no alvo (além do IDamageable).
    /// </summary>
    public interface IKnockbackReceiver
    {
        bool IsStunned { get; }
        void ApplyKnockback(Vector3 direction, float distance, float duration);
    }
}
