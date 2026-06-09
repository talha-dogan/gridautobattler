using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;

/// <summary>
/// Singleton factory responsible for creating and recycling units via ObjectPool.
/// One pool is maintained per unique unit prefab, keyed by the prefab GameObject reference.
/// </summary>
public class UnitFactory : MonoBehaviour
{
    // Singleton instance to ensure only one factory exists in the scene.
    public static UnitFactory Instance { get; private set; }

    // One pool per unique prefab. The key is the source prefab asset reference.
    private readonly Dictionary<GameObject, ObjectPool<BaseUnit>> _pools =
        new Dictionary<GameObject, ObjectPool<BaseUnit>>();

    // We keep a reverse map so that when a unit is released we know which pool it belongs to.
    private readonly Dictionary<BaseUnit, ObjectPool<BaseUnit>> _unitToPool =
        new Dictionary<BaseUnit, ObjectPool<BaseUnit>>();

    // Parent transform used to keep the hierarchy tidy (optional but recommended).
    private Transform _poolRoot;

    private void Awake()
    {
        // Enforce the Singleton pattern.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Create a dedicated hierarchy root so pooled (inactive) units don't clutter the scene.
        _poolRoot = new GameObject("[UnitPool]").transform;
        _poolRoot.SetParent(transform);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Retrieves a unit from the appropriate pool (or creates one if the pool is empty),
    /// positions it, and fully initialises it.
    /// </summary>
    public BaseUnit CreateUnit(BaseUnitDataSO unitData, Vector3 spawnPosition, Team team, int level = 1)
    {
        if (unitData.unitPrefab == null)
        {
            Debug.LogError($"UnitFactory: The prefab for '{unitData.unitName}' is missing!");
            return null;
        }

        ObjectPool<BaseUnit> pool = GetOrCreatePool(unitData.unitPrefab);

        // ActionOnGet will activate the object; we position it here before Initialize.
        BaseUnit spawnedUnit = pool.Get();

        if (spawnedUnit == null)
        {
            Debug.LogWarning($"UnitFactory: Pool returned null for prefab '{unitData.unitPrefab.name}'.");
            return null;
        }

        // Position the unit at the requested spawn point.
        spawnedUnit.transform.position = spawnPosition;
        spawnedUnit.transform.rotation = Quaternion.identity;

        // Give it a clear name in the hierarchy for easy debugging.
        spawnedUnit.gameObject.name = unitData.unitName;

        // Inject the full data, team and level into the unit.
        spawnedUnit.Initialize(unitData, team, level);

        // Register the unit to the BattleManager's active lists.
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.RegisterUnit(spawnedUnit);
        }

        return spawnedUnit;
    }

    /// <summary>
    /// Returns a unit back to its pool instead of destroying it.
    /// Called by BaseUnit.Die() (or any other system that removes a unit from play).
    /// </summary>
    public void ReleaseUnit(BaseUnit unit)
    {
        if (unit == null) return;

        if (_unitToPool.TryGetValue(unit, out ObjectPool<BaseUnit> pool))
        {
            pool.Release(unit);
        }
        else
        {
            // Fallback: if somehow the unit isn't tracked, just destroy it.
            Debug.LogWarning($"UnitFactory: No pool found for unit '{unit.name}'. Destroying instead.");
            Destroy(unit.gameObject);
        }
    }

    // -------------------------------------------------------------------------
    // Pool management helpers
    // -------------------------------------------------------------------------

    private ObjectPool<BaseUnit> GetOrCreatePool(GameObject prefab)
    {
        if (_pools.TryGetValue(prefab, out ObjectPool<BaseUnit> existingPool))
            return existingPool;

        ObjectPool<BaseUnit>[] poolHolder = new ObjectPool<BaseUnit>[1];

        GameObject capturedPrefab = prefab;

        ObjectPool<BaseUnit> newPool = new ObjectPool<BaseUnit>(
            createFunc: () =>
            {
                GameObject go = Instantiate(capturedPrefab, _poolRoot);
                go.SetActive(false); // Start inactive; ActionOnGet will activate it.

                BaseUnit unit = go.GetComponent<BaseUnit>();
                if (unit == null)
                {
                    Debug.LogWarning($"UnitFactory: Prefab '{capturedPrefab.name}' has no BaseUnit component.");
                    Destroy(go);
                    return null;
                }

                // Register the reverse lookup so ReleaseUnit can find the correct pool.
                // poolHolder[0] is guaranteed to be assigned before createFunc is ever
                // called because ObjectPool only calls createFunc on the first Get().
                _unitToPool[unit] = poolHolder[0];
                return unit;
            },
            actionOnGet:     OnGetUnit,
            actionOnRelease: OnReleaseUnit,
            actionOnDestroy: OnDestroyUnit,
            collectionCheck: true,   // Throws if the same instance is released twice (safe in Editor).
            defaultCapacity: 10,
            maxSize:         50
        );

        poolHolder[0] = newPool;
        _pools[prefab] = newPool;
        return newPool;
    }

    /// <summary>
    /// actionOnGet: Called every time a unit is retrieved from the pool.
    /// Activate the GameObject so it becomes visible and receives Update calls.
    /// </summary>
    private void OnGetUnit(BaseUnit unit)
    {
        unit.gameObject.SetActive(true);
    }

    /// <summary>
    /// actionOnRelease: Called every time a unit is returned to the pool.
    /// Deactivate the GameObject to hide it and stop its Update loop.
    /// </summary>
    private void OnReleaseUnit(BaseUnit unit)
    {
        unit.gameObject.SetActive(false);
        // Re-parent back to the pool root to keep the hierarchy clean.
        unit.transform.SetParent(_poolRoot);
    }

    /// <summary>
    /// actionOnDestroy: Called when the pool is full and an excess unit must be discarded,
    /// or when the pool itself is disposed. Physically destroy the GameObject.
    /// </summary>
    private void OnDestroyUnit(BaseUnit unit)
    {
        _unitToPool.Remove(unit);
        Destroy(unit.gameObject);
    }
}