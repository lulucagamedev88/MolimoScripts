namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// Defines the contract for idamageable behavior.
    /// </summary>
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }
        void TakeDamage(float amount, float knockbackDistance = -1f);
    }
}
