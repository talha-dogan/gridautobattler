using UnityEngine;
using System;
using System.Collections.Generic;

public enum Team { Player, Enemy }
public enum UnitState { Idle, Moving, Attacking, Dead, Victory }

[RequireComponent(typeof(Rigidbody2D))]
public abstract class BaseUnit : MonoBehaviour, IDamageable, IAttacker
{
    public event Action<float, float> OnHealthChanged;
    public event Action<BaseUnit> OnDeath;

    [Header("Unit Status")]
    public Team unitTeam;
    public BaseUnitDataSO unitData;

    // ── FSM ───────────────────────────────────────────────────────────────────
    // Shared, allocation-free state instances (one per state type, not per unit)
    private static readonly IdleState _sharedIdleState = new IdleState();
    private static readonly MovingState _sharedMovingState = new MovingState();
    private static readonly AttackingState _sharedAttackingState = new AttackingState();

    private IUnitState _currentStateObj;

    // Public read-only accessor so subclasses (e.g. RangedUnit) can still read the enum
    public UnitState currentState { get; private set; } = UnitState.Idle;

    // ── Target ────────────────────────────────────────────────────────────────
    // Internal field; exposed via property so states can read it safely
    private BaseUnit _currentTarget;
    public BaseUnit currentTarget
    {
        get => _currentTarget;
        protected set => _currentTarget = value;
    }

    [Header("Cached Stats")]
    protected float maxHealth;
    protected float currentHealth;
    protected float moveSpeed;
    protected float attackRange;
    protected float attackDamage;
    protected float attackCooldown;
    protected float lastAttackTime;

    // Accumulated equipment bonuses — stored separately so they can be
    // stripped and re-applied cleanly when a unit is recycled from the pool.
    private float _equipmentBonusHealth;
    private float _equipmentBonusDamage;
    private float _equipmentBonusAttackSpeed;

    // ── Target Search Timer ───────────────────────────────────────────────────
    private float _targetSearchTimer = 0f;
    private const float _targetSearchInterval = 0.2f;
    private bool _targetSearchDue = false;
    private const float _targetStickinessMultiplier = 1.2f;
    private const float _flipDeadzone = 0.05f;

    // ── Focus Timer ───────────────────────────────────────────────────────────
    [Header("Targeting Settings")]
    [Tooltip("Minimum seconds a unit must stay locked on a target before re-scanning.")]
    [SerializeField] private float _minFocusTime = 0.5f;
    private float _focusTimer = 0f;

    // ── Attack Range Hysteresis ───────────────────────────────────────────────
    private const float _attackExitMultiplier = 1.05f;

    // ── Physics Overlap Settings ──────────────────────────────────────────────
    private const float _overlapRadiusMultiplier = 1.5f;
    private LayerMask _enemyLayerMask;
    private LayerMask _playerLayerMask;

    // Modern allocation-free list buffer and filter setup to replace deprecated OverlapCircleNonAlloc
    private static readonly List<Collider2D> _overlapListBuffer = new List<Collider2D>(32);
    private ContactFilter2D _contactFilter;

    // ── Hysteresis Threshold ──────────────────────────────────────────────────
    private const float _hysteresisFactorSq = 0.5625f;

    [Header("Juice Settings")]
    [SerializeField] protected UnitBreathingVisuals breathingVisuals;
    protected float idleBreathingSpeed;
    protected float moveBreathingSpeed;
    protected float breathingAmplitude;

    [Tooltip("The max rotation angle for the walking sway (waddle) effect.")]
    [SerializeField] protected float walkSwayAngle = 8f;

    [SerializeField] protected float victoryBounceSpeed = 15f;
    [SerializeField] protected float victoryBounceHeight = 0.5f;

    private Rigidbody2D _rb;
    private Vector2 _pendingMove = Vector2.zero;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        // Dynamic body type enables physical pushing and sliding between units
        _rb.bodyType = RigidbodyType2D.Dynamic;

        // Disable gravity — units navigate a flat 2D grid, no falling needed
        _rb.gravityScale = 0f;

        // Freeze rotation so units never tip over from collision impulses
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Apply linear damping to act as natural friction when pushed
        _rb.linearDamping = 15f;

        _enemyLayerMask = LayerMask.GetMask("EnemyUnit");
        _playerLayerMask = LayerMask.GetMask("PlayerUnit");

        // Initialize the contact filter for non-allocating modern physics queries
        _contactFilter = new ContactFilter2D();
        _contactFilter.useTriggers = true;
    }

    public virtual void Initialize(BaseUnitDataSO data, Team team)
    {
        unitTeam = team;
        unitData = data;

        maxHealth = data.maxHealth;
        currentHealth = maxHealth;
        moveSpeed = data.moveSpeed;
        attackRange = data.attackRange;
        attackDamage = data.attackDamage;
        attackCooldown = data.attackCooldown;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        idleBreathingSpeed = data.idleBreathingSpeed;
        moveBreathingSpeed = data.moveBreathingSpeed;
        breathingAmplitude = data.breathingAmplitude;

        _pendingMove = Vector2.zero;
        _focusTimer = 0f;

        _targetSearchTimer = _targetSearchInterval;
        _targetSearchDue = true;

        // Boot the FSM into Idle on every (re)initialization
        ChangeState(UnitState.Idle);
    }

    protected virtual void Update()
    {
        if (currentState == UnitState.Dead) return;

        // ── Visuals run every frame regardless of battle state ────────────────
        // Breathing / sway / victory animations must play even before battle
        // starts (placement phase). Only the FSM logic (targeting, movement,
        // attacking) is gated behind isBattleStarted.
        UpdateVisuals();
        UpdateSpriteFlip();

        // ── Battle logic gate ─────────────────────────────────────────────────
        // Everything below this point must NOT run until the battle has begun.
        if (BattleManager.Instance != null && !BattleManager.Instance.isBattleStarted) return;

        // Reset pending move BEFORE Execute so the state can write a fresh direction.
        // FixedUpdate reads this value in the same or next physics step.
        _pendingMove = Vector2.zero;

        if (_focusTimer < _minFocusTime)
            _focusTimer += Time.deltaTime;

        if (!_targetSearchDue)
        {
            _targetSearchTimer += Time.deltaTime;
            if (_targetSearchTimer >= _targetSearchInterval)
            {
                _targetSearchTimer = 0f;
                _targetSearchDue = true;
            }
        }

        // ── FSM tick ──────────────────────────────────────────────────────────
        // Victory state has no IUnitState object; handle it inline.
        // All other states delegate to their Execute() implementations.
        // Note: UpdateVisuals() is already called above, so state Execute()
        // methods must NOT call visual helpers again to avoid double-updating.
        if (currentState != UnitState.Victory)
        {
            _currentStateObj?.Execute(this);
        }

        _targetSearchDue = false;
    }

    protected virtual void FixedUpdate()
    {
        if (_pendingMove != Vector2.zero)
        {
            // Apply force in the full 2D direction so units can move diagonally
            // between grid positions without being restricted to a single axis.
            _rb.AddForce(_pendingMove * (moveSpeed * 20f));
        }

        // Clamp the full velocity magnitude so units never exceed their move speed
        // in any direction (horizontal or vertical).
        if (_rb.linearVelocity.magnitude > moveSpeed)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * moveSpeed;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FSM – State Transition
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Safely transitions the unit to a new state.
    /// Calls Exit() on the old state and Enter() on the new one.
    /// Dead and Victory states bypass the IUnitState objects intentionally.
    /// </summary>
    public void ChangeState(UnitState newState)
    {
        // Guard: skip redundant transitions ONLY when the state object is already
        // correctly assigned. On first boot (Initialize), currentState starts as
        // Idle but _currentStateObj is null, so we must NOT skip that first call.
        if (currentState == newState && _currentStateObj != null) return;

        // Exit current state
        _currentStateObj?.Exit(this);

        currentState = newState;

        // Resolve the shared state object
        _currentStateObj = newState switch
        {
            UnitState.Idle => _sharedIdleState,
            UnitState.Moving => _sharedMovingState,
            UnitState.Attacking => _sharedAttackingState,
            _ => null   // Dead / Victory have no Execute logic
        };

        // Enter new state
        _currentStateObj?.Enter(this);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public helpers called by State classes
    // (internal visibility keeps the API clean while states live in the same assembly)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Called by IdleState.Execute every frame.</summary>
    public void FindClosestTarget()
    {
        if (_focusTimer < _minFocusTime && currentTarget != null) return;

        if (currentTarget != null &&
            currentTarget.currentState != UnitState.Dead &&
            currentTarget.gameObject.activeInHierarchy)
        {
            float stickyRangeSq = (attackRange * _targetStickinessMultiplier) *
                                  (attackRange * _targetStickinessMultiplier);
            float distSq = ((Vector2)currentTarget.transform.position -
                            (Vector2)transform.position).sqrMagnitude;

            if (distSq <= stickyRangeSq)
            {
                ChangeState(UnitState.Moving);
                return;
            }
        }

        if (!_targetSearchDue) return;

        RunTargetSearch();
    }

    /// <summary>Called by MovingState.Execute every frame.</summary>
    public void MoveTowardsTarget()
    {
        if (currentTarget == null || currentTarget.currentState == UnitState.Dead)
        {
            ChangeState(UnitState.Idle);
            return;
        }

        float distanceSq = ((Vector2)currentTarget.transform.position - (Vector2)transform.position).sqrMagnitude;
        float attackRangeSq = attackRange * attackRange;

        if (distanceSq <= attackRangeSq)
        {
            ChangeState(UnitState.Attacking);
            return;
        }

        bool focusLocked = _focusTimer < _minFocusTime;
        if (!focusLocked && _targetSearchDue)
        {
            RunTargetSearch();
        }

        // Compute the full 2D direction vector toward the target and normalise it.
        // This allows units to navigate diagonally between grid positions smoothly.
        Vector2 direction = ((Vector2)currentTarget.transform.position - (Vector2)transform.position).normalized;
        _pendingMove = direction;
    }

    /// <summary>Called by AttackingState.Execute every frame.</summary>
    public void HandleAttackCooldown()
    {
        // ── Stuck-state guard: target gone or dead → back to Idle ─────────────
        if (currentTarget == null || currentTarget.currentState == UnitState.Dead)
        {
            ChangeState(UnitState.Idle);
            return;
        }

        float distanceSq = ((Vector2)currentTarget.transform.position - (Vector2)transform.position).sqrMagnitude;

        float attackExitRange = attackRange * _attackExitMultiplier;
        float attackExitRangeSq = attackExitRange * attackExitRange;

        // ── Stuck-state guard: target walked out of range → chase again ───────
        if (distanceSq > attackExitRangeSq)
        {
            ChangeState(UnitState.Moving);
            return;
        }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            Attack(currentTarget);
            lastAttackTime = Time.time;
        }
    }

    // ── Centralised visual update — called once per frame before the logic gate ──
    // Driven entirely by currentState so visuals are always in sync, even during
    // the pre-battle placement phase when FSM Execute() is not running.
    private void UpdateVisuals()
    {
        if (breathingVisuals == null) return;

        switch (currentState)
        {
            case UnitState.Idle:
                breathingVisuals.UpdateBreathing(idleBreathingSpeed, breathingAmplitude);
                break;
            case UnitState.Moving:
                breathingVisuals.UpdateWalkingSway(moveBreathingSpeed, walkSwayAngle);
                break;
            case UnitState.Victory:
                breathingVisuals.UpdateVictoryBounce(victoryBounceSpeed, victoryBounceHeight);
                break;
            default:
                // Attacking / Dead: hold the last pose; no active animation needed.
                break;
        }
    }

    // ── Visual helpers kept public for any external callers (e.g. Enter/Exit hooks) ──

    public void UpdateIdleVisuals()
    {
        if (breathingVisuals != null)
            breathingVisuals.UpdateBreathing(idleBreathingSpeed, breathingAmplitude);
    }

    public void UpdateMovingVisuals()
    {
        if (breathingVisuals != null)
            breathingVisuals.UpdateWalkingSway(moveBreathingSpeed, walkSwayAngle);
    }

    public void ResetBreathingVisuals()
    {
        if (breathingVisuals != null)
            breathingVisuals.ResetVisuals();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void RunTargetSearch()
    {
        LayerMask opponentMask = unitTeam == Team.Player ? _enemyLayerMask : _playerLayerMask;
        float attackRangeSq = attackRange * attackRange;
        float overlapRadius = attackRange * _overlapRadiusMultiplier;

        // Dynamic layer mask injection into the modern contact filter
        _contactFilter.SetLayerMask(opponentMask);

        // Modern allocation-free API query
        int hitCount = Physics2D.OverlapCircle(
            (Vector2)transform.position, overlapRadius, _contactFilter, _overlapListBuffer);

        if (hitCount > 0)
        {
            BaseUnit bestCandidate = null;
            float bestCandidateSq = Mathf.Infinity;

            for (int i = 0; i < hitCount; i++)
            {
                if (_overlapListBuffer[i] == null) continue;
                if (!_overlapListBuffer[i].TryGetComponent<BaseUnit>(out var candidate)) continue;
                if (candidate.currentState == UnitState.Dead) continue;

                float dSq = ((Vector2)candidate.transform.position -
                             (Vector2)transform.position).sqrMagnitude;
                if (dSq < bestCandidateSq)
                {
                    bestCandidateSq = dSq;
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate != null)
            {
                if (bestCandidateSq <= attackRangeSq)
                {
                    AcquireTarget(bestCandidate, UnitState.Attacking);
                    return;
                }

                if (currentTarget == null || currentTarget.currentState == UnitState.Dead)
                {
                    AcquireTarget(bestCandidate, UnitState.Moving);
                }
                else if (bestCandidate != currentTarget)
                {
                    float currentDistSq = ((Vector2)currentTarget.transform.position -
                                           (Vector2)transform.position).sqrMagnitude;
                    if (bestCandidateSq < currentDistSq * _hysteresisFactorSq)
                        AcquireTarget(bestCandidate, UnitState.Moving);
                    else
                        ChangeState(UnitState.Moving);
                }
                else
                {
                    ChangeState(UnitState.Moving);
                }

                return;
            }
        }

        if (BattleManager.Instance != null)
        {
            BaseUnit globalBest = BattleManager.Instance.GetClosestTargetFor(this);
            if (globalBest != null)
                AcquireTarget(globalBest, UnitState.Moving);
        }
    }

    private void AcquireTarget(BaseUnit newTarget, UnitState newState)
    {
        currentTarget = newTarget;
        _focusTimer = 0f;
        ChangeState(newState);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void TriggerVictory()
    {
        if (currentState != UnitState.Dead)
        {
            ChangeState(UnitState.Victory);
            currentTarget = null;
        }
    }

    // -------------------------------------------------------------------------
    // Equipment Bonus API — called by UnitEquipmentManager
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds equipment stat bonuses on top of the base stats set by Initialize().
    /// Accumulates into private tracking fields so bonuses can be fully reversed
    /// by ResetEquipmentBonuses() without touching the original base values.
    /// </summary>
    public void ApplyEquipmentBonus(float bonusHealth, float bonusDamage, float bonusAttackSpeed)
    {
        // Track the cumulative bonus so it can be reversed cleanly on reset.
        _equipmentBonusHealth      += bonusHealth;
        _equipmentBonusDamage      += bonusDamage;
        _equipmentBonusAttackSpeed += bonusAttackSpeed;

        // Apply to live stats.
        maxHealth     += bonusHealth;
        currentHealth += bonusHealth;
        attackDamage  += bonusDamage;

        // bonusAttackSpeed reduces cooldown (lower = faster); clamp to a minimum
        // of 0.05 s so units can never reach an infinite attack rate.
        attackCooldown = Mathf.Max(0.05f, attackCooldown - bonusAttackSpeed);

        // Notify listeners that health pool has changed (e.g. health bar UI).
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Reverses all previously applied equipment bonuses, restoring the unit's
    /// stats to the values set by the last Initialize() call.
    /// Must be called before re-applying a new loadout (e.g. pool reuse).
    /// </summary>
    public void ResetEquipmentBonuses()
    {
        // Reverse every accumulated bonus.
        maxHealth      -= _equipmentBonusHealth;
        currentHealth  -= _equipmentBonusHealth;
        attackDamage   -= _equipmentBonusDamage;
        attackCooldown += _equipmentBonusAttackSpeed;

        // Clamp health so a large bonus removal never drives it below zero.
        currentHealth = Mathf.Max(0f, currentHealth);

        // Clear the accumulators.
        _equipmentBonusHealth      = 0f;
        _equipmentBonusDamage      = 0f;
        _equipmentBonusAttackSpeed = 0f;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public virtual void Attack(IDamageable target) { }

    public virtual void TakeDamage(float amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (DamageTextManager.Instance != null)
            DamageTextManager.Instance.SpawnDamageText(transform.position, amount);

        // Hit sesi
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySoundAtPosition(SoundType.HitFlesh, transform.position);

        if (currentHealth <= 0 && currentState != UnitState.Dead) Die();
    }

    protected virtual void Die()
    {
        ChangeState(UnitState.Dead);
        _pendingMove = Vector2.zero;

        OnDeath?.Invoke(this);

        if (UnitFactory.Instance != null)
        {
            UnitFactory.Instance.ReleaseUnit(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void UpdateSpriteFlip()
    {
        float directionX = 0f;
        bool shouldFlip = false;

        if (currentTarget != null && currentTarget.currentState != UnitState.Dead)
        {
            directionX = currentTarget.transform.position.x - transform.position.x;
            shouldFlip = true;
        }
        else if (_pendingMove.x != 0f)
        {
            directionX = _pendingMove.x;
            shouldFlip = true;
        }

        if (!shouldFlip) return;
        if (Mathf.Abs(directionX) <= _flipDeadzone) return;

        if (directionX > 0f)
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }
}