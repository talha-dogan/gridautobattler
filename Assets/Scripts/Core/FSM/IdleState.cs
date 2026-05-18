// IdleState: The unit has no target yet.
// Every frame it asks BaseUnit to scan for the nearest enemy.
// As soon as a target is found, BaseUnit.ChangeState() will be called
// from within FindClosestTarget(), transitioning to Moving or Attacking.
public class IdleState : IUnitState
{
    public void Enter(BaseUnit unit)
    {
        // Reset visuals to neutral pose when entering idle
        unit.ResetBreathingVisuals();
    }

    public void Execute(BaseUnit unit)
    {
        // Visuals are driven centrally by BaseUnit.UpdateVisuals() every frame.
        // Only FSM logic belongs here.
        unit.FindClosestTarget();
    }

    public void Exit(BaseUnit unit) { }
}
