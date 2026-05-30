using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Merkezi additive sahne yükleme/unload sistemi.
///
/// Mimari:
///   • CoreScene — DontDestroyOnLoad singleton'ları barındırır (GameBootstrap,
///     GameInputHandler, SoundManager). Hiçbir zaman unload edilmez.
///   • UpgradeScene / GridScene — additive olarak yüklenir ve geçiş sırasında
///     unload edilir. Sahneye özgü manager'lar (BattleManager, LevelManager,
///     UpgradeManager vb.) bu sahnelerde yaşar.
///
/// Kullanım:
///   SceneLoader.Instance.LoadSceneAdditive("GridScene", onComplete: () => { ... });
///   SceneLoader.Instance.UnloadScene("UpgradeScene", onComplete: () => { ... });
///   SceneLoader.Instance.TransitionTo("GridScene", unloadCurrent: "UpgradeScene");
///
/// Memory Cleanup:
///   Sahne unload edilmeden önce SceneCleanupPipeline çalıştırılır:
///   1. Addressables handle'ları release edilir
///   2. ObjectPool'lar temizlenir
///   3. GameEvents subscriber'ları temizlenir
///   4. Resources.UnloadUnusedAssets() çağrılır
///   5. GC.Collect() çağrılır
/// </summary>
public class SceneLoader : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static SceneLoader Instance { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Sahne yükleme başladığında tetiklenir. Parametre: sahne adı.</summary>
    public static event Action<string> OnSceneLoadStarted;

    /// <summary>Sahne yükleme tamamlandığında tetiklenir. Parametre: sahne adı.</summary>
    public static event Action<string> OnSceneLoadCompleted;

    /// <summary>Sahne unload başladığında tetiklenir. Parametre: sahne adı.</summary>
    public static event Action<string> OnSceneUnloadStarted;

    /// <summary>Sahne unload tamamlandığında tetiklenir. Parametre: sahne adı.</summary>
    public static event Action<string> OnSceneUnloadCompleted;

    /// <summary>Geçiş sırasında yükleme ilerlemesi (0-1).</summary>
    public static event Action<float> OnLoadProgress;

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>Şu an yüklü olan additive sahnelerin adları.</summary>
    private readonly HashSet<string> _loadedScenes = new HashSet<string>();

    /// <summary>Geçiş işlemi devam ediyor mu?</summary>
    public bool IsTransitioning { get; private set; } = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Her yeni başlangıçta geçiş durumunu sıfırla
        IsTransitioning = false;

        // Başlangıçta aktif sahneleri kaydet
        RefreshLoadedSceneCache();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Belirtilen sahneyi additive olarak yükler.
    /// Sahne zaten yüklüyse onComplete hemen çağrılır.
    /// </summary>
    public void LoadSceneAdditive(string sceneName, Action onComplete = null)
    {
        if (_loadedScenes.Contains(sceneName))
        {
            Debug.LogWarning($"[SceneLoader] '{sceneName}' zaten yüklü. Atlanıyor.");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(LoadSceneAdditiveRoutine(sceneName, onComplete));
    }

    /// <summary>
    /// Belirtilen sahneyi unload eder.
    /// Unload öncesinde SceneCleanupPipeline çalıştırılır.
    /// </summary>
    public void UnloadScene(string sceneName, Action onComplete = null, bool runCleanup = true)
    {
        if (!_loadedScenes.Contains(sceneName))
        {
            Debug.LogWarning($"[SceneLoader] '{sceneName}' yüklü değil. Unload atlanıyor.");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(UnloadSceneRoutine(sceneName, onComplete, runCleanup));
    }

    /// <summary>
    /// Tam sahne geçişi: önce yeni sahneyi yükler, sonra eskisini unload eder.
    /// allowSceneActivation = false pattern'i ile yeni sahne hazır olana kadar
    /// aktivasyon geciktirilir.
    /// </summary>
    /// <param name="targetScene">Yüklenecek sahne adı.</param>
    /// <param name="sceneToUnload">Unload edilecek sahne adı (null ise unload yapılmaz).</param>
    /// <param name="onComplete">Geçiş tamamlandığında çağrılır.</param>
    public void TransitionTo(string targetScene, string sceneToUnload = null, Action onComplete = null)
    {
        if (IsTransitioning)
        {
            Debug.LogWarning($"[SceneLoader] Geçiş zaten devam ediyor. '{targetScene}' isteği reddedildi.");
            onComplete?.Invoke(); 
            return;
        }

        StartCoroutine(TransitionRoutine(targetScene, sceneToUnload, onComplete));
    }

    /// <summary>
    /// Belirtilen sahnenin şu an yüklü olup olmadığını döner.
    /// </summary>
    public bool IsSceneLoaded(string sceneName) => _loadedScenes.Contains(sceneName);

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator LoadSceneAdditiveRoutine(string sceneName, Action onComplete)
    {
        Debug.Log($"[SceneLoader] Additive yükleme başlıyor: '{sceneName}'");
        OnSceneLoadStarted?.Invoke(sceneName);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (asyncLoad == null)
        {
            Debug.LogError($"[SceneLoader] '{sceneName}' yüklenemedi. Build Settings'te kayıtlı mı?");
            yield break;
        }

        while (!asyncLoad.isDone)
        {
            OnLoadProgress?.Invoke(asyncLoad.progress);
            yield return null;
        }

        OnLoadProgress?.Invoke(1f);

        _loadedScenes.Add(sceneName);
        Debug.Log($"[SceneLoader] '{sceneName}' başarıyla yüklendi (additive).");
        OnSceneLoadCompleted?.Invoke(sceneName);

        onComplete?.Invoke();
    }

    private IEnumerator UnloadSceneRoutine(string sceneName, Action onComplete, bool runCleanup)
    {
        Debug.Log($"[SceneLoader] Unload başlıyor: '{sceneName}'");
        OnSceneUnloadStarted?.Invoke(sceneName);

        // Cleanup pipeline'ı çalıştır
        if (runCleanup)
        {
            yield return StartCoroutine(SceneCleanupPipeline.RunCleanup(sceneName));
        }

        AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(sceneName);
        if (asyncUnload == null)
        {
            Debug.LogError($"[SceneLoader] '{sceneName}' unload edilemedi.");
            yield break;
        }

        while (!asyncUnload.isDone)
            yield return null;

        _loadedScenes.Remove(sceneName);
        Debug.Log($"[SceneLoader] '{sceneName}' başarıyla unload edildi.");
        OnSceneUnloadCompleted?.Invoke(sceneName);

        onComplete?.Invoke();
    }

    private IEnumerator TransitionRoutine(string targetScene, string sceneToUnload, Action onComplete)
    {
        IsTransitioning = true;
        Debug.Log($"[SceneLoader] Geçiş başlıyor: '{sceneToUnload}' → '{targetScene}'");

        // 1. Yeni sahneyi arka planda yükle (aktivasyon geciktirilmiş)
        OnSceneLoadStarted?.Invoke(targetScene);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
        if (asyncLoad == null)
        {
            Debug.LogError($"[SceneLoader] '{targetScene}' yüklenemedi. Build Settings'te kayıtlı mı?");
            IsTransitioning = false;
            yield break;
        }

        // 1. Önce eski sahneyi cleanup + unload et
        if (!string.IsNullOrEmpty(sceneToUnload) && _loadedScenes.Contains(sceneToUnload))
        {
            OnSceneUnloadStarted?.Invoke(sceneToUnload);
            yield return StartCoroutine(SceneCleanupPipeline.RunCleanup(sceneToUnload));

            AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(sceneToUnload);
            if (asyncUnload != null)
            {
                while (!asyncUnload.isDone)
                {
                    OnLoadProgress?.Invoke(asyncUnload.progress * 0.5f);
                    yield return null;
                }
            }

            _loadedScenes.Remove(sceneToUnload);
            Debug.Log($"[SceneLoader] '{sceneToUnload}' unload edildi.");
            OnSceneUnloadCompleted?.Invoke(sceneToUnload);
        }

        // 2. Yeni sahneyi yükle ve tamamlanmasını bekle
        while (!asyncLoad.isDone)
        {
            OnLoadProgress?.Invoke(0.5f + asyncLoad.progress * 0.5f);
            yield return null;
        }

        _loadedScenes.Add(targetScene);
        OnLoadProgress?.Invoke(1f);
        Debug.Log($"[SceneLoader] '{targetScene}' aktive edildi.");
        OnSceneLoadCompleted?.Invoke(targetScene);

        // 4. Yeni sahneyi aktif sahne olarak ayarla
        Scene newScene = SceneManager.GetSceneByName(targetScene);
        if (newScene.IsValid())
            SceneManager.SetActiveScene(newScene);

        IsTransitioning = false;
        onComplete?.Invoke();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Başlangıçta Unity'nin yüklü sahnelerini cache'e ekler.
    /// </summary>
    private void RefreshLoadedSceneCache()
    {
        _loadedScenes.Clear();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
                _loadedScenes.Add(scene.name);
        }
    }
}