using System;

// Interface segregation: Only entities that can take damage implement this.
public interface IDamageable
{
    // Applies the specified amount of damage to the entity.
    void TakeDamage(float amount);

    // Event triggered when the entity reaches zero health.
    // Passes the dying unit as a parameter so subscribers can use direct
    // method-group references instead of closures, preventing memory leaks.
    event Action<BaseUnit> OnDeath;
}
