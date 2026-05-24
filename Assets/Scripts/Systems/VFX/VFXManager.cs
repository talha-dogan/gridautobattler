using UnityEngine;
using UnityEngine.Pool;
using System.Collections;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// VFXEntry — Inspector'da her VFXType için bir ParticleSystem prefab atanır.
// ─────────────────────────────────────────────────────────────────────────────
[System.Serializable]
public class VFXEntry
{
    [Tooltip("Bu VFX tipine karşılık gelen enum değeri.")]
    public VFXType vfxType;

    [Tooltip("Oynatılacak ParticleSystem prefab'ı.")]
    public ParticleSystem prefab;

    [Tooltip("Başlangıçta pool'a eklenecek nesne sayısı.")]
    public int initialPoolSize = 5;

    [Tooltip("Pool'un ulaşabileceği maksimum boyut.")]
    public int maxPoolSize = 20;
}

// ─────────────────────────────────────────────────────────────────────────────
// VFXManager
// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Merkezi VFX yöneticisi.
///
/// Özellikler:
///   • ObjectPool tabanlı — her VFXType için ayrı bir pool.
///   • Otomatik geri dönüş — ParticleSystem bitince nesne pool'a döner.
///   • GameEvents entegrasyonu — HitFlesh, UnitDeath, BattleWin olaylarını dinler.
///   • Rotation desteği — efekti belirli bir yönde oynatabilirsiniz.
/// </summary>
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("─── VFX Kütüphanesi ───────────────────────────────────────")]
    [Tooltip("Her VFXType için bir VFXEntry tanımlayın.")]
    [SerializeField] private VFXEntry[] vfxLibrary;

    // ─────────────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────────────

    // VFXType → ObjectPool<ParticleSystem>
    private readonly Dictionary<VFXType, ObjectPool<ParticleSystem>> _pools =
        new Dictionary<VFXType, ObjectPool<ParticleSystem>>();

    // Reverse lookup: aktif ParticleSystem → hangi pool'a ait
    private readonly Dictionary<ParticleSystem, ObjectPool<ParticleSystem>> _psToPool =
        new Dictionary<ParticleSystem, ObjectPool<ParticleSystem>>();

    // VFXType → prefab referansı (pool oluştururken lazım)
    private readonly Dictionary<VFXType, ParticleSystem> _prefabMap =
        new Dictionary<VFXType, ParticleSystem>();

    private Transform _poolRoot;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _poolRoot = new GameObject("[VFXPool]").transform;
        _poolRoot.SetParent(transform);

        BuildPools();
    }

    private void OnEnable()
    {
        GameEvents.OnUnitDied      += OnUnitDied;
        GameEvents.OnLevelWin      += OnLevelWin;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitDied      -= OnUnitDied;
        GameEvents.OnLevelWin      -= OnLevelWin;
    }

    private void OnDestroy()
    {
        foreach (var pool in _pools.Values)
            pool.Dispose();

        _pools.Clear();
        _psToPool.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Belirtilen VFX tipini dünya konumunda oynatır.
    /// </summary>
    public void PlayVFX(VFXType type, Vector3 worldPosition)
    {
        PlayVFX(type, worldPosition, Quaternion.identity);
    }

    /// <summary>
    /// Belirtilen VFX tipini dünya konumunda ve belirtilen rotasyonda oynatır.
    /// </summary>
    public void PlayVFX(VFXType type, Vector3 worldPosition, Quaternion rotation)
    {
        if (!_pools.TryGetValue(type, out var pool)) return;

        ParticleSystem ps = pool.Get();
        if (ps == null) return;

        ps.transform.position = worldPosition;
        ps.transform.rotation = rotation;
        ps.Play();

        // Otomatik geri dönüş için coroutine başlat
        StartCoroutine(ReturnWhenFinished(ps, pool));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GameEvents Handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnUnitDied(BaseUnit unit)
    {
        if (unit == null) return;
        PlayVFX(VFXType.UnitDeath, unit.transform.position);
    }

    private void OnLevelWin(string _)
    {
        // Zafer efektini sahnenin ortasında oynat
        PlayVFX(VFXType.BattleWin, Vector3.zero);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pool — Dahili
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildPools()
    {
        if (vfxLibrary == null) return;

        foreach (VFXEntry entry in vfxLibrary)
        {
            if (entry.prefab == null)
            {
                Debug.LogWarning($"[VFXManager] '{entry.vfxType}' için prefab atanmamış. Atlanıyor.");
                continue;
            }

            if (_pools.ContainsKey(entry.vfxType))
            {
                Debug.LogWarning($"[VFXManager] '{entry.vfxType}' için mükerrer giriş. İlk giriş kullanılacak.");
                continue;
            }

            _prefabMap[entry.vfxType] = entry.prefab;

            VFXEntry capturedEntry = entry;
            ObjectPool<ParticleSystem>[] poolHolder = new ObjectPool<ParticleSystem>[1];

            var pool = new ObjectPool<ParticleSystem>(
                createFunc: () =>
                {
                    ParticleSystem ps = Instantiate(capturedEntry.prefab, _poolRoot);
                    ps.gameObject.SetActive(false);
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _psToPool[ps] = poolHolder[0];
                    return ps;
                },
                actionOnGet: ps =>
                {
                    ps.gameObject.SetActive(true);
                },
                actionOnRelease: ps =>
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.gameObject.SetActive(false);
                    ps.transform.SetParent(_poolRoot);
                },
                actionOnDestroy: ps =>
                {
                    _psToPool.Remove(ps);
                    if (ps != null) Destroy(ps.gameObject);
                },
                collectionCheck: true,
                defaultCapacity: capturedEntry.initialPoolSize,
                maxSize: capturedEntry.maxPoolSize
            );

            poolHolder[0] = pool;
            _pools[entry.vfxType] = pool;

            // Pool'u ısıt (warm up)
            WarmUpPool(pool, entry.initialPoolSize);
        }
    }

    private void WarmUpPool(ObjectPool<ParticleSystem> pool, int count)
    {
        var temp = new ParticleSystem[count];
        for (int i = 0; i < count; i++)
            temp[i] = pool.Get();
        for (int i = 0; i < count; i++)
            pool.Release(temp[i]);
    }

    private IEnumerator ReturnWhenFinished(ParticleSystem ps, ObjectPool<ParticleSystem> pool)
    {
        // ParticleSystem bitene kadar bekle
        yield return new WaitWhile(() => ps != null && ps.isPlaying);

        if (ps != null && _psToPool.ContainsKey(ps))
            pool.Release(ps);
    }
}
