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
    // Target query — Lane Priority system
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the best living target for the seeker using a three-tier Lane Priority system.
    ///
    /// Tier 1 — Same Lane:
    ///   Search for living enemies whose grid row (Y) exactly matches the seeker's
    ///   current grid row. If any exist, return the closest one.
    ///
    /// Tier 2 — Adjacent Lanes:
    ///   If the seeker's own lane is completely empty, check the immediately
    ///   neighbouring rows (seekerRow ± 1). Return the closest enemy found there.
    ///
    /// Tier 3 — Global Fallback:
    ///   If both the same lane and adjacent lanes are empty, return the absolute
    ///   closest living enemy anywhere on the board.
    ///
    /// All distance comparisons use sqrMagnitude to avoid the cost of Mathf.Sqrt.
    /// Units queued in _pendingRemovals are still in the list at query time but
    /// are already in UnitState.Dead, so the Dead-state guard filters them out.
    /// </summary>
    public BaseUnit GetClosestTargetFor(BaseUnit seeker)
    {
        if (!isBattleStarted) return null;

        List<BaseUnit> opposingTeam = seeker.unitTeam == Team.Player ? enemyUnits : playerUnits;
        if (opposingTeam.Count == 0) return null;

        // ── Resolve the seeker's current grid row ─────────────────────────────
        // Convert the seeker's world position to a grid row index.
        // If GridManager is unavailable, fall back to the global closest search.
        int seekerRow = -1;
        if (GridManager.Instance != null)
        {
            GridManager.Instance.WorldToGrid(seeker.transform.position, out _, out seekerRow);
        }

        // ── Tier 1: Same-lane search ──────────────────────────────────────────
        if (seekerRow >= 0)
        {
            BaseUnit sameLaneBest = FindClosestInRows(seeker, opposingTeam, seekerRow, seekerRow);
            if (sameLaneBest != null) return sameLaneBest;

            // ── Tier 2: Adjacent-lane search ──────────────────────────────────
            // Only reached when the seeker's own lane has no living enemies.
            int adjacentRowMin = seekerRow - 1;
            int adjacentRowMax = seekerRow + 1;

            BaseUnit adjacentLaneBest = FindClosestInRows(seeker, opposingTeam, adjacentRowMin, adjacentRowMax,
                                                          excludeRow: seekerRow);
            if (adjacentLaneBest != null) return adjacentLaneBest;
        }

        // ── Tier 3: Global fallback ───────────────────────────────────────────
        // Reached when both the same lane and adjacent lanes are empty,
        // or when GridManager is not available.
        return FindClosestGlobal(seeker, opposingTeam);
    }

    /// <summary>
    /// Scans the opposing team list and returns the closest living unit whose
    /// grid row falls within [rowMin, rowMax], optionally skipping one row.
    /// Returns null if no qualifying unit is found.
    /// </summary>
    private BaseUnit FindClosestInRows(BaseUnit seeker, List<BaseUnit> candidates,
                                       int rowMin, int rowMax, int excludeRow = -999)
    {
        float    bestSqr  = Mathf.Infinity;
        BaseUnit bestUnit = null;

        foreach (BaseUnit candidate in candidates)
        {
            // Skip dead or destroyed units.
            if (candidate == null || candidate.currentState == UnitState.Dead) continue;

            // Resolve the candidate's grid row.
            GridManager.Instance.WorldToGrid(candidate.transform.position, out _, out int candidateRow);

            // Check that the row is within the requested band and not excluded.
            if (candidateRow < rowMin || candidateRow > rowMax) continue;
            if (candidateRow == excludeRow) continue;

            float sqr = ((Vector2)seeker.transform.position -
                         (Vector2)candidate.transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr  = sqr;
                bestUnit = candidate;
            }
        }

        return bestUnit;
    }

    /// <summary>
    /// Returns the absolute closest living unit from the candidates list,
    /// regardless of grid position. Used as the final fallback.
    /// </summary>
    private BaseUnit FindClosestGlobal(BaseUnit seeker, List<BaseUnit> candidates)
    {
        float    bestSqr  = Mathf.Infinity;
        BaseUnit bestUnit = null;

        foreach (BaseUnit candidate in candidates)
        {
            // Skip dead or destroyed units.
            if (candidate == null || candidate.currentState == UnitState.Dead) continue;

            float sqr = ((Vector2)seeker.transform.position -
                         (Vector2)candidate.transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr  = sqr;
                bestUnit = candidate;
            }
        }

        return bestUnit;
    }
}
