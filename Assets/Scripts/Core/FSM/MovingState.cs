// MovingState: The unit has a target and is closing the distance.
// Transitions:
//   → IdleState    : target is null or dead
//   → AttackingState : unit enters attack range
public class MovingState : IUnitState
{
    public void Enter(BaseUnit unit) { }

    public void Execute(BaseUnit unit)
    {
        // Visuals are driven centrally by BaseUnit.UpdateVisuals() every frame.
        // Only FSM logic belongs here.
        unit.MoveTowardsTarget();
    }

    public void Exit(BaseUnit unit) { }
}
