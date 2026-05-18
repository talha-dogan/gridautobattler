using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the flow of battle, keeps track of all active units, and assigns targets.
/// Win/loss outcomes are broadcast through GameEvents — no direct LevelManager reference needed.
///
/// CRASH PROTECTION: Units that die during a foreach traversal are NOT removed immediately.
/// They are queued in _pendingRemovals and flushed at the end of the same frame via LateUpdate.
/// This prevents InvalidOperationException ("Collection was modified") on playerUnits/enemyUnits.
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("Battle State")]
    public bool isBattleStarted { get; private set; } = false;

    [Header("Active Units")]
    public List<BaseUnit> playerUnits = new List<BaseUnit>();
    public List<BaseUnit> enemyUnits  = new List<BaseUnit>();

    // -------------------------------------------------------------------------
    // Pending removal queue — units that died this frame but have not yet been
    // removed from playerUnits / enemyUnits. Flushed in LateUpdate so that any
    // foreach loop currently iterating the lists completes safely first.
    // -------------------------------------------------------------------------
    private readonly List<BaseUnit> _pendingRemovals = new List<BaseUnit>();

    // Dirty flag: set to true whenever a unit is queued for removal.
    // LateUpdate only runs the flush logic when this is true, avoiding a
    // pointless iteration over an empty list every frame.
    private bool _hasPendingRemovals = false;

    private bool _isGameOver = false;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Flushes the pending-removal queue at the end of every frame.
    /// Running in LateUpdate guarantees that all Update() / FixedUpdate() loops
    /// (including foreach traversals in GetClosestTargetFor) have already finished
    /// before we mutate the lists.
    /// </summary>
    private void LateUpdate()
    {
        if (!_hasPendingRemovals) return;

        for (int i = 0; i < _pendingRemovals.Count; i++)
        {
            BaseUnit unit = _pendingRemovals[i];

            if (unit.unitTeam == Team.Player)
                playerUnits.Remove(unit);
            else
                enemyUnits.Remove(unit);
        }

        _pendingRemovals.Clear();
        _hasPendingRemovals = false;

        // Evaluate win/loss AFTER the lists are clean so the counts are accurate.
        EvaluateBattleOutcome();
    }

    // -------------------------------------------------------------------------
    // Battle start — called by the UI WAR! button (via UnitSpawner)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Begins the battle. Uses a coroutine instead of Invoke so the delay is
    /// cancellable and does not fire on a destroyed object.
    /// </summary>
    public void StartBattle()
    {
        StartCoroutine(StartBattleRoutine());
    }

    private System.Collections.IEnumerator StartBattleRoutine()
    {
        // Small delay to allow any last-moment unit registrations to complete
        // before the battle loop begins.
        yield return new WaitForSeconds(0.1f);

        if (this == null) yield break; // Guard: object may have been destroyed

        SetBattleStarted();
    }

    private void SetBattleStarted()
    {
        isBattleStarted = true;
        GameEvents.BattleStarted();
        Debug.Log("Battle started! Units are moving.");
    }

    // -------------------------------------------------------------------------
    // State reset
    // -------------------------------------------------------------------------

    public void ResetBattleState()
    {
        isBattleStarted = false;
        _isGameOver     = false;
        playerUnits.Clear();
        enemyUnits.Clear();

        // Also clear any leftover pending removals from the previous round
        // so stale entries do not bleed into the next battle.
        _pendingRemovals.Clear();
        _hasPendingRemovals = false;
    }

    // -------------------------------------------------------------------------
    // Unit registration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by UnitFactory when a new unit enters the battlefield.
    /// Assigns the correct physics layer and subscribes to the unit's OnDeath event.
    /// </summary>
    public void RegisterUnit(BaseUnit unit)
    {
        if (unit.unitTeam == Team.Player)
        {
            playerUnits.Add(unit);
            int playerLayer = LayerMask.NameToLayer("PlayerUnit");
            if (playerLayer != -1) unit.gameObject.layer = playerLayer;
        }
        else
        {
            enemyUnits.Add(unit);
            int enemyLayer = LayerMask.NameToLayer("EnemyUnit");
            if (enemyLayer != -1) unit.gameObject.layer = enemyLayer;
        }

        // Direct method-group subscription — no closure allocation, clean unsubscribe.
        unit.OnDeath += QueueUnitForRemoval;

        // Notify any interested systems (analytics, tutorial, etc.) via the event bus.
        GameEvents.UnitSpawned(unit);
    }

    // -------------------------------------------------------------------------
    // Unit unregistration — two-phase (queue → flush)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Phase 1 — called immediately when a unit's OnDeath event fires.
    /// Does NOT touch playerUnits / enemyUnits directly; instead queues the unit
    /// for safe removal in LateUpdate to avoid mutating a list mid-traversal.
    /// </summary>
    private void QueueUnitForRemoval(BaseUnit unit)
    {
        // Unsubscribe immediately to sever the delegate reference and prevent
        // the event from keeping the unit alive in the GC graph after death.
        unit.OnDeath -= QueueUnitForRemoval;

        // Notify kill-tracking / achievement systems right away — the unit is
        // logically dead even though it hasn't been removed from the list yet.
        GameEvents.UnitDied(unit);

        // Queue for deferred list removal.
        _pendingRemovals.Add(unit);
        _hasPendingRemovals = true;
    }

    /// <summary>
    /// Phase 2 — called from LateUpdate after the lists have been mutated.
    /// Evaluates win/loss only when the battle is active and not already over.
    /// </summary>
    private void EvaluateBattleOutcome()
    {
        if (!isBattleStarted || _isGameOver) return;

        // Victory: the last enemy unit just died.
        if (enemyUnits.Count == 0)
        {
            _isGameOver = true;

            // Iterate a snapshot copy so TriggerVictory cannot cause re-entrant
            // modifications if it ever fires an event that touches playerUnits.
            BaseUnit[] survivors = playerUnits.ToArray();
            foreach (BaseUnit survivor in survivors)
                survivor.TriggerVictory();

            // Broadcast win — LevelManager listens and handles rewards/progression.
            GameEvents.LevelWin(string.Empty);
        }
        // Defeat: the last player unit just died.
        else if (playerUnits.Count == 0)
        {
            _isGameOver = true;

            BaseUnit[] survivors = enemyUnits.ToArray();
            foreach (BaseUnit survivor in survivors)
                survivor.TriggerVictory();

            // Broadcast loss — LevelManager listens and handles retry logic.
            GameEvents.LevelLose(string.Empty);
        }
    }

    // -------------------------------------------------------------------------
    // Target query
    // -------------------------------------------------------------------------

    /// <summary>
    /// Provides the closest living enemy target for a given unit.
    /// Uses sqrMagnitude instead of Vector2.Distance to avoid the expensive
    /// Mathf.Sqrt call — valid because we only need relative comparisons.
    /// Units queued in _pendingRemovals are still in the list at query time but
    /// are already in UnitState.Dead, so the Dead-state guard below filters them.
    /// </summary>
    public BaseUnit GetClosestTargetFor(BaseUnit seeker)
    {
        if (!isBattleStarted) return null;

        List<BaseUnit> opposingTeam = seeker.unitTeam == Team.Player ? enemyUnits : playerUnits;
        if (opposingTeam.Count == 0) return null;

        float    closestSqrDistance = Mathf.Infinity;
        BaseUnit closestUnit        = null;

        foreach (BaseUnit potentialTarget in opposingTeam)
        {
            // Skip units that are already dead but not yet flushed from the list.
            if (potentialTarget == null || potentialTarget.currentState == UnitState.Dead) continue;

            float sqrDistance = ((Vector2)seeker.transform.position -
                                 (Vector2)potentialTarget.transform.position).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestUnit        = potentialTarget;
            }
        }

        return closestUnit;
    }
}
