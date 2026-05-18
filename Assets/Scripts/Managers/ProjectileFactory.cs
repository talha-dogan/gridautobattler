using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;

/// <summary>
/// Centralised projectile pool manager — mirrors the UnitFactory pattern.
///
/// PROBLEM SOLVED: Previously every RangedUnit instance maintained its own
/// ObjectPool<Projectile>. With 20 ranged units on the field that meant 20
/// separate pools, each pre-allocating their own capacity, with no sharing
/// between units that fire the same prefab.
///
/// SOLUTION: One pool per unique projectile prefab, shared across ALL units
/// that fire that prefab. RangedUnit calls ProjectileFactory.Get() and
/// ProjectileFactory.Release() instead of managing a pool itself.
///
/// LIFECYCLE:
///   Get(prefab)          → activates a Projectile from the correct pool
///   Release(projectile)  → returns it to the correct pool (deactivates it)
/// </summary>
public class ProjectileFactory : MonoBehaviour
{
    public static ProjectileFactory Instance { get; private set; }

    // One pool per unique prefab, keyed by the source prefab asset reference.
    // Mirrors the Dictionary<GameObject, ObjectPool<BaseUnit>> pattern in UnitFactory.
    private readonly Dictionary<GameObject, ObjectPool<Projectile>> _pools =
        new Dictionary<GameObject, ObjectPool<Projectile>>();

    // Reverse lookup: given a live Projectile instance, find its owning pool so
    // Release() can return it without the caller needing to pass the prefab key.
    private readonly Dictionary<Projectile, ObjectPool<Projectile>> _projectileToPool =
        new Dictionary<Projectile, ObjectPool<Projectile>>();

    // Parent transform to keep inactive pooled projectiles out of the root hierarchy.
    private Transform _poolRoot;

    // Pool configuration constants — adjust to match the heaviest wave scenario.
    private const int DefaultCapacity = 10;
    private const int MaxPoolSize     = 60;

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

        // Dedicated hierarchy root keeps inactive projectiles tidy in the Editor.
        _poolRoot = new GameObject("[ProjectilePool]").transform;
        _poolRoot.SetParent(transform);
    }

    private void OnDestroy()
    {
        // Dispose all pools when the factory is destroyed (scene unload / quit).
        // This releases the native UnityEngine.Pool resources cleanly.
        foreach (var pool in _pools.Values)
            pool.Dispose();

        _pools.Clear();
        _projectileToPool.Clear();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Retrieves an active Projectile from the pool that corresponds to <paramref name="prefab"/>.
    /// Creates a new pool automatically if this prefab has not been seen before.
    /// The returned Projectile is already active; call Launch() on it immediately.
    /// </summary>
    public Projectile Get(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("ProjectileFactory.Get: prefab is null.");
            return null;
        }

        return GetOrCreatePool(prefab).Get();
    }

    /// <summary>
    /// Returns a Projectile to its pool (deactivates it).
    /// Called by Projectile.ReturnToPool() — the projectile itself holds a
    /// reference to this factory via IObjectPool<Projectile> injected at creation.
    /// </summary>
    public void Release(Projectile projectile)
    {
        if (projectile == null) return;

        if (_projectileToPool.TryGetValue(projectile, out var pool))
        {
            pool.Release(projectile);
        }
        else
        {
            // Fallback: projectile was not created by this factory — destroy it.
            Debug.LogWarning(
                $"ProjectileFactory.Release: No pool found for '{projectile.name}'. Destroying instead.");
            Destroy(projectile.gameObject);
        }
    }

    // -------------------------------------------------------------------------
    // Pool management
    // -------------------------------------------------------------------------

    private ObjectPool<Projectile> GetOrCreatePool(GameObject prefab)
    {
        if (_pools.TryGetValue(prefab, out var existingPool))
            return existingPool;

        // Forward-reference trick (same pattern as UnitFactory):
        // createFunc needs to register the pool in _projectileToPool, but the
        // pool object doesn't exist yet. A one-element array acts as a mutable
        // capture so the lambda can reference the pool after it is assigned.
        ObjectPool<Projectile>[] poolHolder = new ObjectPool<Projectile>[1];
        GameObject capturedPrefab = prefab;

        var newPool = new ObjectPool<Projectile>(
            createFunc: () =>
            {
                GameObject go = Instantiate(capturedPrefab, _poolRoot);
                go.SetActive(false);

                Projectile p = go.GetComponent<Projectile>();
                if (p == null)
                {
                    Debug.LogWarning(
                        $"ProjectileFactory: Prefab '{capturedPrefab.name}' has no Projectile component.");
                    Destroy(go);
                    return null;
                }

                // Inject the IObjectPool reference so the projectile can return
                // itself without knowing about ProjectileFactory directly.
                p.SetPool(poolHolder[0]);

                // Register reverse lookup for Release().
                _projectileToPool[p] = poolHolder[0];
                return p;
            },
            actionOnGet: p =>
            {
                p.gameObject.SetActive(true);
            },
            actionOnRelease: p =>
            {
                p.ResetState();
                p.gameObject.SetActive(false);
                p.transform.SetParent(_poolRoot);
            },
            actionOnDestroy: p =>
            {
                _projectileToPool.Remove(p);
                if (p != null) Destroy(p.gameObject);
            },
            collectionCheck: true,      // Catches double-release bugs in the Editor.
            defaultCapacity: DefaultCapacity,
            maxSize: MaxPoolSize
        );

        poolHolder[0]    = newPool;
        _pools[prefab]   = newPool;
        return newPool;
    }
}
