using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// A self-contained projectile that moves toward a target and deals damage on hit.
/// Instead of calling Destroy(), it returns itself to the ObjectPool managed by RangedUnit.
/// </summary>
public class Projectile : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Runtime state (reset on every Launch call via ResetState)
    // -------------------------------------------------------------------------
    private float _damage;
    private float _speed;
    private Vector3 _moveDirection;
    private Team _shooterTeam;
    private float _lifetimeTimer;

    // Guard flag: prevents double-release when both OnTriggerEnter2D and the
    // lifetime timer fire in the same frame (or in rapid succession).
    private bool _isReturned;

    // Cached components — resolved once in Awake to avoid repeated GetComponent calls.
    private TrailRenderer _trailRenderer;
    private Rigidbody2D _rb;

    [SerializeField] private float _lifetime = 2.5f;

    // -------------------------------------------------------------------------
    // Pool reference — injected by RangedUnit before the projectile is launched.
    // -------------------------------------------------------------------------
    private IObjectPool<Projectile> _pool;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Cache components. Both are safe to be null — handled defensively below.
        _trailRenderer = GetComponent<TrailRenderer>();
        _rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Called by RangedUnit to bind this projectile to its owning pool.
    /// Must be called once after the pool creates the instance (createFunc).
    /// </summary>
    public void SetPool(IObjectPool<Projectile> pool)
    {
        _pool = pool;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initialises all runtime state. Called by RangedUnit every time the
    /// projectile is retrieved from the pool (ActionOnGet).
    /// </summary>
    public void Launch(BaseUnit target, float damage, float speed, Team shooterTeam)
    {
        _damage = damage;
        _speed = speed;
        _shooterTeam = shooterTeam;
        _lifetimeTimer = 0f;
        _isReturned = false;

        if (target != null)
        {
            _moveDirection = (target.transform.position - transform.position).normalized;

            float angle = Mathf.Atan2(_moveDirection.y, _moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
        else
        {
            // No valid target — return to pool immediately.
            _moveDirection = Vector3.zero;
            ReturnToPool();
        }

        // Enable the TrailRenderer and clear any old geometry. 
        // This ensures no stretching line is drawn from the pool position.
        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = true;
            _trailRenderer.Clear();
        }
    }

    private void Update()
    {
        if (_isReturned) return;

        // Simple linear movement toward the cached direction.
        transform.position += _moveDirection * _speed * Time.deltaTime;

        // Lifetime check — replaces the old Destroy(gameObject, _lifetime) call.
        _lifetimeTimer += Time.deltaTime;
        if (_lifetimeTimer >= _lifetime)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_isReturned) return;

        // TryGetComponent is more performant than GetComponent — avoids an
        // allocation when the collider belongs to a non-unit object.
        if (collision.TryGetComponent<BaseUnit>(out var hitUnit) && hitUnit.unitTeam != _shooterTeam)
        {
            hitUnit.TakeDamage(_damage);
            ReturnToPool();
        }
    }

    // -------------------------------------------------------------------------
    // Pool helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns this projectile to its pool (deactivates it).
    /// The _isReturned guard ensures this is only executed once per activation.
    /// Falls back to Destroy if no pool has been assigned.
    /// </summary>
    private void ReturnToPool()
    {
        if (_isReturned) return;
        _isReturned = true;

        if (_pool != null)
        {
            _pool.Release(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called by RangedUnit's ActionOnRelease to reset all state when the
    /// projectile is returned to the pool.
    /// </summary>
    public void ResetState()
    {
        _damage = 0f;
        _speed = 0f;
        _moveDirection = Vector3.zero;
        _lifetimeTimer = 0f;
        _isReturned = false; // Ready to be used again on the next Get().

        // Flush any residual physics forces so the projectile does not carry
        // velocity from a previous shot into its next activation.
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        // Disable the TrailRenderer entirely while resting in the pool.
        // This stops it from connecting the old impact point to the new spawn point.
        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = false;
        }
    }
}