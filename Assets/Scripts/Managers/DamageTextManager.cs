using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Singleton manager that spawns floating damage numbers using an ObjectPool.
/// Replaces all Instantiate/Destroy calls with pool Get/Release calls.
/// </summary>
public class DamageTextManager : MonoBehaviour
{
    public static DamageTextManager Instance { get; private set; }

    [SerializeField] private DamageTextDataSO _damageTextData;

    // -------------------------------------------------------------------------
    // Pool
    // -------------------------------------------------------------------------

    // Single pool for the one damage-text prefab defined in _damageTextData.
    private ObjectPool<DamageText> _pool;

    // Parent transform to keep pooled (inactive) objects out of the root hierarchy.
    private Transform _poolRoot;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Create a tidy hierarchy root for inactive pooled objects.
        _poolRoot = new GameObject("[DamageTextPool]").transform;
        _poolRoot.SetParent(transform);

        InitialisePool();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Retrieves a DamageText from the pool, positions it, and starts its animation.
    /// Replaces the old Instantiate call.
    /// </summary>
    public void SpawnDamageText(Vector3 position, float damageAmount)
    {
        if (_damageTextData == null || _damageTextData.textPrefab == null) return;

        // Calculate a randomised spawn position above the unit's head.
        float offsetX = Random.Range(-_damageTextData.randomOffsetRange.x, _damageTextData.randomOffsetRange.x);
        float offsetY = Random.Range(0f, _damageTextData.randomOffsetRange.y);
        Vector3 spawnPos = position + new Vector3(offsetX, offsetY + _damageTextData.spawnHeightOffset, 0f);

        // Get an instance from the pool (ActionOnGet activates it).
        DamageText textInstance = _pool.Get();

        // Position it at the calculated world-space location.
        textInstance.transform.position = spawnPos;

        // Start the float-and-fade animation with the current data settings.
        textInstance.Initialize(damageAmount, _damageTextData);
    }

    // -------------------------------------------------------------------------
    // Pool setup
    // -------------------------------------------------------------------------

    private void InitialisePool()
    {
        if (_damageTextData == null || _damageTextData.textPrefab == null)
        {
            Debug.LogWarning("DamageTextManager: DamageTextDataSO or its textPrefab is not assigned.");
            return;
        }

        GameObject prefab = _damageTextData.textPrefab;

        _pool = new ObjectPool<DamageText>(
            createFunc: () =>
            {
                // Instantiate a new DamageText and bind it to this pool.
                GameObject go = Instantiate(prefab, _poolRoot);
                DamageText dt = go.GetComponent<DamageText>();
                dt.SetPool(_pool);
                go.SetActive(false);
                return dt;
            },
            actionOnGet: dt =>
            {
                // Activate the GameObject so it is visible and its coroutine can run.
                dt.gameObject.SetActive(true);
            },
            actionOnRelease: dt =>
            {
                // Reset visual state and deactivate — ready for the next Get().
                dt.ResetState();
                dt.gameObject.SetActive(false);
                dt.transform.SetParent(_poolRoot);
            },
            actionOnDestroy: dt =>
            {
                // Pool is full or being disposed — physically destroy the GameObject.
                Destroy(dt.gameObject);
            },
            collectionCheck: true,  // Catches double-release bugs in the Editor.
            defaultCapacity: 10,
            maxSize: 30
        );
    }
}
