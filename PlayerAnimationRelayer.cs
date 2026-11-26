using UnityEngine;
using UnityEngine.Events;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]
    /// <summary>
    /// Provides the player animation relayer component.
    /// </summary>
    public class PlayerAnimationRelayer : MonoBehaviour
    {
        #region Inspector — references of combat
        [Header("=== references (combat) ===")]
        [SerializeField] private WeaponHitBox weaponHitBox;
        [SerializeField] private WeaponVFX weaponVFX;
        #endregion

        #region Inspector — Eventos of prologue
        [Header("=== Eventos of prologue (Animation Event) ===")]
        [SerializeField] private UnityEvent onWakeAnimationComplete;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (weaponHitBox == null)
                weaponHitBox = GetComponentInChildren<WeaponHitBox>(true);

            if (weaponVFX == null)
                weaponVFX = GetComponentInChildren<WeaponVFX>(true);

            if (weaponVFX == null)
                weaponVFX = GetComponentInParent<WeaponVFX>(true);
        }
        #endregion

        #region Animation Event — prologue
        /// <summary>
        /// Handles wake animation complete events.
        /// </summary>
        public void OnWakeAnimationComplete()
        {
            onWakeAnimationComplete?.Invoke();
        }
        #endregion

        #region Animation Event — combat & damage
        /// <summary>
        /// Sets custom damage.
        /// </summary>
        public void SetCustomDamage(float damage)
        {
            if (weaponHitBox != null)
                weaponHitBox.SetCustomDamage(damage);
        }

        /// <summary>
        /// Sets knockback distance.
        /// </summary>
        public void SetKnockbackDistance(float distance)
        {
            if (weaponHitBox != null)
                weaponHitBox.SetKnockbackDistance(distance);
        }

        /// <summary>
        /// Performs the enable weapon hit box operation.
        /// </summary>
        public void EnableWeaponHitBox()
        {
            if (weaponHitBox != null)
                weaponHitBox.EnableHit();
        }

        /// <summary>
        /// Performs the enable weapon hit box with knockback operation.
        /// </summary>
        public void EnableWeaponHitBoxWithKnockback(float knockbackDistance)
        {
            if (weaponHitBox != null)
                weaponHitBox.EnableHitWithKnockback(knockbackDistance);
        }

        /// <summary>
        /// Performs the disable weapon hit box operation.
        /// </summary>
        public void DisableWeaponHitBox()
        {
            if (weaponHitBox != null)
                weaponHitBox.DisableHit();
        }
        #endregion

        #region Animation Event — audio of the sword
        /// <summary>
        /// Plays swing sound.
        /// </summary>
        public void PlaySwingSound()
        {
            if (weaponHitBox != null)
                weaponHitBox.PlaySwingSound();
        }

        /// <summary>
        /// Performs the sword impact operation.
        /// </summary>
        public void SwordImpact(string impactName)
        {
            if (weaponHitBox != null)
                weaponHitBox.SwordImpact(impactName);
        }
        #endregion

        #region Animation Event — WeaponHitBox VFX
        /// <summary>
        /// Performs the slash vfx operation.
        /// </summary>
        public void SlashVFX(string vfxName)
        {
            if (weaponHitBox != null)
                weaponHitBox.SlashVFX(vfxName);
        }

        /// <summary>
        /// Performs the stop slash vfx operation.
        /// </summary>
        public void StopSlashVFX(string vfxName)
        {
            if (weaponHitBox != null)
                weaponHitBox.StopVFX(vfxName);
        }
        #endregion

        #region Animation Event — SwordTrail
        /// <summary>
        /// Plays sword trail horizontal.
        /// </summary>
        public void PlaySwordTrailHorizontal()
        {
            if (weaponVFX != null)
                weaponVFX.EnableSwordTrail(WeaponVFX.VFXOrientation.Horizontal, false);
        }

        /// <summary>
        /// Plays sword trail horizontal reverse.
        /// </summary>
        public void PlaySwordTrailHorizontalReverse()
        {
            if (weaponVFX != null)
                weaponVFX.EnableSwordTrail(WeaponVFX.VFXOrientation.Horizontal, true);
        }

        /// <summary>
        /// Plays sword trail vertical.
        /// </summary>
        public void PlaySwordTrailVertical()
        {
            if (weaponVFX != null)
                weaponVFX.EnableSwordTrail(WeaponVFX.VFXOrientation.Vertical, false);
        }

        /// <summary>
        /// Plays sword trail vertical reverse.
        /// </summary>
        public void PlaySwordTrailVerticalReverse()
        {
            if (weaponVFX != null)
                weaponVFX.EnableSwordTrail(WeaponVFX.VFXOrientation.Vertical, true);
        }

        /// <summary>
        /// Performs the stop sword trail operation.
        /// </summary>
        public void StopSwordTrail()
        {
            if (weaponVFX != null)
                weaponVFX.DisableSwordTrail();
        }

        /// <summary>
        /// Plays impact.
        /// </summary>
        public void PlayImpact()
        {
            if (weaponVFX != null)
                weaponVFX.PlayImpact();
        }

        /// <summary>
        /// Performs the stop impact operation.
        /// </summary>
        public void StopImpact()
        {
            if (weaponVFX != null)
                weaponVFX.StopImpact();
        }
        #endregion

        #region Animation Event — Block
        /// <summary>
        /// Plays block sound.
        /// </summary>
        public void PlayBlockSound()
        {
            if (weaponHitBox != null)
                weaponHitBox.PlayClashSound();
        }

        /// <summary>
        /// Performs the block vfx operation.
        /// </summary>
        public void BlockVFX(string vfxName)
        {
            if (weaponHitBox != null)
                weaponHitBox.BlockVFX(vfxName);
        }

        /// <summary>
        /// Performs the stop block vfx operation.
        /// </summary>
        public void StopBlockVFX(string vfxName)
        {
            if (weaponHitBox != null)
                weaponHitBox.StopBlockVFX(vfxName);
        }
        #endregion
    }
}