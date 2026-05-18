// Defines the contract for any entity capable of dealing damage.
public interface IAttacker
{
    // Executes an attack on a target that implements IDamageable.
    void Attack(IDamageable target);
}