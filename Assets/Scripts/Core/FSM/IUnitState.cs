// Defines the contract that every concrete unit state must fulfill.
// Enter  → called once when the FSM transitions INTO this state.
// Execute → called every frame while this state is active (replaces the old switch-case body).
// Exit   → called once when the FSM transitions OUT OF this state.
public interface IUnitState
{
    void Enter(BaseUnit unit);
    void Execute(BaseUnit unit);
    void Exit(BaseUnit unit);
}
