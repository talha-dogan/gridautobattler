using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Sahne geçişlerinde sistematik memory cleanup pipeline'ı.
///
/// Cleanup sırası (SceneLoader tarafından unload öncesinde çağrılır):
///   1. Addressables handle'larını release et
///   2. UnitFactory ObjectPool'larını temizle
///   3. VFXManager pool'larını temizle
///   4. SoundManager aktif ses kaynaklarını durdur
///   5. GameEvents subscriber'larını temizle
///   6. Resources.UnloadUnusedAssets() çağır
///   7. GC.Collect() çağır
///
/// Kullanım:
///   yield return StartCoroutine(SceneCleanupPipeline.RunCleanup("GridScene"));
/// </summary>
public static class SceneCleanupPipeline
{
    // Addressables handle'larını takip eden global registry.
    // Herhangi bir sistem handle yüklediğinde buraya kaydetmeli.
    private static readonly List<AsyncOperationHandle> _trackedHandles =
        new List<AsyncOperationHandle>();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Belirtilen sahne için tam cleanup pipeline'ını çalıştırır.
    /// Coroutine olarak yield edilebilir.
    /// </summary>
    public static IEnumerator RunCleanup(string sceneName)
    {
        Debug.Log($"[SceneCleanupPipeline] '{sceneName}' için cleanup başlıyor...");

        // 1. Addressables handle'larını release et
        ReleaseAddressableHandles();
        yield return null;

        // 2. UnitFactory pool'larını temizle (GridScene'e özgü)
        ClearUnitPools();
        yield return null;

        // 3. VFXManager pool'larını temizle
        ClearVFXPools();
        yield return null;

        // 4. SoundManager aktif seslerini durdur (BGM hariç — DontDestroyOnLoad)
        StopActiveSounds();
        yield return null;

        // 5. GameEvents subscriber'larını temizle
        // NOT: DontDestroyOnLoad singleton'ları (SoundManager, VFXManager) kendi
        // OnDisable'larında unsubscribe eder. ClearAllEvents() sadece sahneye özgü
        // subscriber'ları temizler.
        GameEvents.ClearAllEvents();
        yield return null;

        // 6. Kullanılmayan asset'leri unload et (async — bir frame bekle)
        AsyncOperation unloadOp = Resources.UnloadUnusedAssets();
        while (!unloadOp.isDone)
            yield return null;

        // 7. GC
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();

        Debug.Log($"[SceneCleanupPipeline] '{sceneName}' cleanup tamamlandı.");
    }

    /// <summary>
    /// Addressables handle'ını cleanup pipeline'ına kaydet.
    /// Yükleme yapan her sistem bu metodu çağırmalı.
    /// </summary>
    public static void TrackHandle(AsyncOperationHandle handle)
    {
        if (handle.IsValid())
            _trackedHandles.Add(handle);
    }

    /// <summary>
    /// Generic versiyon — tip güvenli handle takibi.
    /// </summary>
    public static void TrackHandle<T>(AsyncOperationHandle<T> handle)
    {
        if (handle.IsValid())
            _trackedHandles.Add(handle);
    }

    // ── Private Cleanup Steps ─────────────────────────────────────────────────

    private static void ReleaseAddressableHandles()
    {
        int count = 0;
        for (int i = _trackedHandles.Count - 1; i >= 0; i--)
        {
            AsyncOperationHandle handle = _trackedHandles[i];
            if (handle.IsValid())
            {
                Addressables.Release(handle);
                count++;
            }
        }
        _trackedHandles.Clear();

        if (count > 0)
            Debug.Log($"[SceneCleanupPipeline] {count} Addressables handle release edildi.");
    }

    private static void ClearUnitPools()
    {
        if (UnitFactory.Instance == null) return;

        // Tüm aktif unit'leri pool'a geri döndür
        BaseUnit[] activeUnits = UnityEngine.Object.FindObjectsByType<BaseUnit>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        int count = 0;
        foreach (BaseUnit unit in activeUnits)
        {
            UnitFactory.Instance.ReleaseUnit(unit);
            count++;
        }

        if (count > 0)
            Debug.Log($"[SceneCleanupPipeline] {count} unit pool'a geri döndürüldü.");
    }

    private static void ClearVFXPools()
    {
        // VFXManager DontDestroyOnLoad değil — sahneyle birlikte yok olur.
        // Aktif coroutine'leri durdurmak için Instance'ı kontrol et.
        if (VFXManager.Instance == null) return;

        // VFXManager kendi OnDestroy'unda pool'ları dispose eder.
        // Burada sadece aktif particle'ları durduruyoruz.
        ParticleSystem[] activePSystems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (ParticleSystem ps in activePSystems)
        {
            if (ps != null && ps.isPlaying)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private static void StopActiveSounds()
    {
        // SoundManager DontDestroyOnLoad — BGM'i durdurmuyoruz, sadece SFX pool'unu temizliyoruz.
        // SoundManager kendi Update'inde bitmiş sesleri zaten pool'a döndürür.
        // Burada sadece log basıyoruz; gerekirse SoundManager'a StopAllSFX() eklenebilir.
        Debug.Log("[SceneCleanupPipeline] SoundManager SFX pool temizleme atlandı (DontDestroyOnLoad).");
    }
}
