// AttackingState: The unit is within attack range and fires/swings on cooldown.
// Transitions:
//   → IdleState    : target is null or dead
//   → MovingState  : target moved outside the attack-exit range (hysteresis)
public class AttackingState : IUnitState
{
    public void Enter(BaseUnit unit)
    {
        // Reset breathing visuals so the attack pose starts clean
        unit.ResetBreathingVisuals();
    }

    public void Execute(BaseUnit unit)
    {
        unit.HandleAttackCooldown();
    }

    public void Exit(BaseUnit unit)
    {
        // Nothing special needed on exit; subclasses can override via BaseUnit hooks
    }
}
