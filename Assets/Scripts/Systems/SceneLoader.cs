using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Central additive scene loading/unloading system.
///
/// Architecture:
///   • CoreScene — hosts DontDestroyOnLoad singletons. Never unloaded.
///   • UpgradeScene / GridScene — loaded additively and unloaded during transitions.
///
/// Usage:
///   SceneLoader.Instance.LoadSceneAdditive("GridScene", onComplete: () => { ... });
///   SceneLoader.Instance.UnloadScene("UpgradeScene", onComplete: () => { ... });
///   SceneLoader.Instance.TransitionTo("GridScene", sceneToUnload: "UpgradeScene");
///
/// Fader:
///   A full-screen black Canvas (Sorting Order 999) is created at Awake as a
///   root-level DontDestroyOnLoad object so it survives all scene transitions.
///   TransitionRoutine fades in before heavy work and fades out after activation.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static SceneLoader Instance { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when a scene load starts. Parameter: scene name.</summary>
    public static event Action<string> OnSceneLoadStarted;

    /// <summary>Fired when a scene load completes. Parameter: scene name.</summary>
    public static event Action<string> OnSceneLoadCompleted;

    /// <summary>Fired when a scene unload starts. Parameter: scene name.</summary>
    public static event Action<string> OnSceneUnloadStarted;

    /// <summary>Fired when a scene unload completes. Parameter: scene name.</summary>
    public static event Action<string> OnSceneUnloadCompleted;

    /// <summary>Load progress during a transition (0–1).</summary>
    public static event Action<float> OnLoadProgress;

    // ── Config ────────────────────────────────────────────────────────────────

    [Tooltip("Duration of the fade-in (screen → black) in seconds.")]
    [SerializeField] private float fadeInDuration = 0.3f;

    [Tooltip("Duration of the fade-out (black → screen) in seconds.")]
    [SerializeField] private float fadeOutDuration = 0.4f;

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>Names of currently loaded additive scenes.</summary>
    private readonly HashSet<string> _loadedScenes = new HashSet<string>();

    /// <summary>Whether a transition is currently in progress.</summary>
    public bool IsTransitioning { get; private set; } = false;

    // ── Fader references ──────────────────────────────────────────────────────

    // The fader lives on a SEPARATE root GameObject so DontDestroyOnLoad works correctly.
    // (DontDestroyOnLoad only works on root GameObjects, not children.)
    private GameObject _faderRoot;
    private Image      _faderImage;

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

        // Reset transition state on every fresh start
        IsTransitioning = false;

        // Register scenes that are already loaded at startup
        RefreshLoadedSceneCache();

        // Build the full-screen fader overlay as a separate root object
        CreateFader();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            // Destroy the fader root when SceneLoader itself is destroyed
            if (_faderRoot != null)
                Destroy(_faderRoot);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the specified scene additively.
    /// If the scene is already loaded, onComplete is invoked immediately.
    /// </summary>
    public void LoadSceneAdditive(string sceneName, Action onComplete = null)
    {
        if (_loadedScenes.Contains(sceneName))
        {
            Debug.LogWarning($"[SceneLoader] '{sceneName}' is already loaded. Skipping.");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(LoadSceneAdditiveRoutine(sceneName, onComplete));
    }

    /// <summary>
    /// Unloads the specified scene.
    /// SceneCleanupPipeline runs before unloading.
    /// </summary>
    public void UnloadScene(string sceneName, Action onComplete = null, bool runCleanup = true)
    {
        if (!_loadedScenes.Contains(sceneName))
        {
            Debug.LogWarning($"[SceneLoader] '{sceneName}' is not loaded. Skipping unload.");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(UnloadSceneRoutine(sceneName, onComplete, runCleanup));
    }

    /// <summary>
    /// Full scene transition with fade: fades to black, loads the new scene,
    /// unloads the old one, then fades back in.
    /// </summary>
    /// <param name="targetScene">Name of the scene to load.</param>
    /// <param name="sceneToUnload">Name of the scene to unload (null = no unload).</param>
    /// <param name="onComplete">Callback invoked when the transition finishes.</param>
    public void TransitionTo(string targetScene, string sceneToUnload = null, Action onComplete = null)
    {
        if (IsTransitioning)
        {
            Debug.LogWarning($"[SceneLoader] Transition already in progress. Request for '{targetScene}' rejected.");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(TransitionRoutine(targetScene, sceneToUnload, onComplete));
    }

    /// <summary>Returns whether the specified scene is currently loaded.</summary>
    public bool IsSceneLoaded(string sceneName) => _loadedScenes.Contains(sceneName);

    // ── Fader ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a persistent full-screen black Canvas overlay.
    /// IMPORTANT: The Canvas lives on its OWN root GameObject (not a child of SceneLoader)
    /// so that DontDestroyOnLoad works correctly — Unity only supports DontDestroyOnLoad
    /// on root-level GameObjects.
    /// </summary>
    private void CreateFader()
    {
        // Create a standalone root GameObject for the fader
        _faderRoot = new GameObject("[SceneLoader_Fader]");
        DontDestroyOnLoad(_faderRoot); // Works because _faderRoot is a root object

        // Canvas — Screen Space Overlay, high sorting order so it renders on top of everything
        Canvas canvas = _faderRoot.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        // CanvasScaler for resolution independence
        CanvasScaler scaler = _faderRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // GraphicRaycaster intentionally omitted — fader must never block input

        // Full-screen black Image
        GameObject imageGO = new GameObject("[SceneLoader_FaderImage]");
        imageGO.transform.SetParent(_faderRoot.transform, false);

        _faderImage = imageGO.AddComponent<Image>();
        _faderImage.color         = new Color(0f, 0f, 0f, 0f); // Start fully transparent
        _faderImage.raycastTarget = false;                       // Never block input

        // Stretch to fill the entire canvas
        RectTransform rt = imageGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Debug.Log("[SceneLoader] Fader overlay created (sorting order 999, root DontDestroyOnLoad).");
    }

    /// <summary>
    /// Animates the fader image alpha from its current value to targetAlpha over duration seconds.
    /// Uses unscaledDeltaTime so it works even when Time.timeScale is 0.
    /// </summary>
    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (_faderImage == null)
        {
            Debug.LogWarning("[SceneLoader] FadeTo called but _faderImage is null. Skipping fade.");
            yield break;
        }

        float startAlpha = _faderImage.color.a;
        float elapsed    = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _faderImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        // Guarantee exact final value
        _faderImage.color = new Color(0f, 0f, 0f, targetAlpha);
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator LoadSceneAdditiveRoutine(string sceneName, Action onComplete)
    {
        Debug.Log($"[SceneLoader] Additive load starting: '{sceneName}'");
        OnSceneLoadStarted?.Invoke(sceneName);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (asyncLoad == null)
        {
            Debug.LogError($"[SceneLoader] '{sceneName}' could not be loaded. Is it registered in Build Settings?");
            yield break;
        }

        while (!asyncLoad.isDone)
        {
            OnLoadProgress?.Invoke(asyncLoad.progress);
            yield return null;
        }

        OnLoadProgress?.Invoke(1f);
        _loadedScenes.Add(sceneName);
        Debug.Log($"[SceneLoader] '{sceneName}' loaded successfully (additive).");
        OnSceneLoadCompleted?.Invoke(sceneName);
        onComplete?.Invoke();
    }

    private IEnumerator UnloadSceneRoutine(string sceneName, Action onComplete, bool runCleanup)
    {
        Debug.Log($"[SceneLoader] Unload starting: '{sceneName}'");
        OnSceneUnloadStarted?.Invoke(sceneName);

        if (runCleanup)
            yield return StartCoroutine(SceneCleanupPipeline.RunCleanup(sceneName));

        AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(sceneName);
        if (asyncUnload == null)
        {
            Debug.LogError($"[SceneLoader] '{sceneName}' could not be unloaded.");
            yield break;
        }

        while (!asyncUnload.isDone)
            yield return null;

        _loadedScenes.Remove(sceneName);
        Debug.Log($"[SceneLoader] '{sceneName}' unloaded successfully.");
        OnSceneUnloadCompleted?.Invoke(sceneName);
        onComplete?.Invoke();
    }

    private IEnumerator TransitionRoutine(string targetScene, string sceneToUnload, Action onComplete)
    {
        IsTransitioning = true;
        Debug.Log($"[SceneLoader] Transition starting: '{sceneToUnload}' → '{targetScene}'");

        // ── Step 1: Fade IN (screen → black) ──────────────────────────────────
        yield return StartCoroutine(FadeTo(1f, fadeInDuration));
        Debug.Log("[SceneLoader] Fade-in complete. Screen is black.");

        // ── Step 2: Cleanup and unload the old scene (hidden behind black screen) ──
        // Check both the internal cache AND Unity's actual scene list to be safe.
        bool shouldUnload = !string.IsNullOrEmpty(sceneToUnload) &&
                            SceneManager.GetSceneByName(sceneToUnload).IsValid();

        if (shouldUnload)
        {
            Debug.Log($"[SceneLoader] Unloading old scene: '{sceneToUnload}'");
            OnSceneUnloadStarted?.Invoke(sceneToUnload);

            // Run cleanup pipeline (Addressables, pools, events, GC)
            yield return StartCoroutine(SceneCleanupPipeline.RunCleanup(sceneToUnload));

            // Before unloading, make sure the scene to unload is NOT the active scene.
            // UnloadSceneAsync fails silently if the target is the only/active scene.
            // Set CoreScene as active so the old scene can be safely unloaded.
            Scene coreScene = SceneManager.GetSceneByName("CoreScene");
            if (coreScene.IsValid() && coreScene.isLoaded)
                SceneManager.SetActiveScene(coreScene);

            AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(sceneToUnload);
            if (asyncUnload != null)
            {
                while (!asyncUnload.isDone)
                {
                    OnLoadProgress?.Invoke(asyncUnload.progress * 0.4f);
                    yield return null;
                }
                Debug.Log($"[SceneLoader] '{sceneToUnload}' unloaded successfully.");
            }
            else
            {
                Debug.LogWarning($"[SceneLoader] UnloadSceneAsync returned null for '{sceneToUnload}'. It may already be unloaded.");
            }

            _loadedScenes.Remove(sceneToUnload);
            OnSceneUnloadCompleted?.Invoke(sceneToUnload);
        }

        // ── Step 3: Load the new scene in the background ──────────────────────
        // allowSceneActivation = false holds the scene at 90% until we are ready.
        Debug.Log($"[SceneLoader] Loading new scene: '{targetScene}'");
        OnSceneLoadStarted?.Invoke(targetScene);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
        if (asyncLoad == null)
        {
            Debug.LogError($"[SceneLoader] '{targetScene}' could not be loaded. Is it registered in Build Settings?");
            yield return StartCoroutine(FadeTo(0f, fadeOutDuration));
            IsTransitioning = false;
            yield break;
        }

        asyncLoad.allowSceneActivation = false;

        // Wait until the scene is preloaded (Unity stops at exactly 0.9 when activation is blocked)
        while (asyncLoad.progress < 0.9f)
        {
            OnLoadProgress?.Invoke(0.4f + asyncLoad.progress * 0.4f);
            yield return null;
        }

        Debug.Log($"[SceneLoader] '{targetScene}' preloaded at 90%. Activating...");

        // ── Step 4: Activate the new scene ────────────────────────────────────
        // CRITICAL: allowSceneActivation = true must be set BEFORE the while(!isDone) loop.
        // While activation is blocked, isDone is always false → infinite loop.
        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            OnLoadProgress?.Invoke(0.8f + asyncLoad.progress * 0.2f);
            yield return null;
        }

        _loadedScenes.Add(targetScene);
        OnLoadProgress?.Invoke(1f);
        Debug.Log($"[SceneLoader] '{targetScene}' activated and ready.");
        OnSceneLoadCompleted?.Invoke(targetScene);

        // Set the new scene as the active scene
        Scene newScene = SceneManager.GetSceneByName(targetScene);
        if (newScene.IsValid())
            SceneManager.SetActiveScene(newScene);

        IsTransitioning = false;
        onComplete?.Invoke();

        // ── Step 5: Fade OUT (black → screen) ─────────────────────────────────
        yield return StartCoroutine(FadeTo(0f, fadeOutDuration));
        Debug.Log("[SceneLoader] Fade-out complete. Transition done.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates the loaded-scene cache from Unity's current scene list.
    /// Called once at Awake to capture scenes already open at startup.
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
