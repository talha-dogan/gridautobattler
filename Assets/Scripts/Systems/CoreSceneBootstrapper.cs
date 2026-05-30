using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// CoreScene'in giriş noktası.
///
/// CoreScene mimarisi:
///   CoreScene — Hiçbir zaman unload edilmez. Şu DontDestroyOnLoad singleton'larını barındırır:
///     • GameBootstrap      — Save sistemi, lokalizasyon
///     • GameInputHandler   — Input sistemi (zaten DontDestroyOnLoad)
///     • SoundManager       — Ses sistemi (zaten DontDestroyOnLoad)
///     • SceneLoader        — Additive sahne yönetimi (yeni)
///
///   UpgradeScene — Additive olarak yüklenir/unload edilir.
///     • UpgradeManager, PawnShopManager, LootBoxManager, UI Canvas
///
///   GridScene — Additive olarak yüklenir/unload edilir.
///     • BattleManager, LevelManager, UnitFactory, UnitSpawner, GridManager,
///       VFXManager, GameUIManager
///
/// Başlangıç akışı:
///   1. CoreScene yüklenir (ilk sahne)
///   2. CoreSceneBootstrapper Awake'de singleton'ları başlatır
///   3. İlk oyun sahnesi (UpgradeScene veya StartScene) additive olarak yüklenir
///
/// NOT: Eğer oyun StartScene'den başlıyorsa, CoreScene'i Build Settings'te
/// ilk sıraya koy ve StartScene'i additive olarak yükle.
/// Eğer CoreScene yoksa ve oyun doğrudan UpgradeScene'den başlıyorsa,
/// bu script UpgradeScene'deki bir GameObject'e de eklenebilir —
/// DontDestroyOnLoad sayesinde GridScene'e geçişte hayatta kalır.
/// </summary>
public class CoreSceneBootstrapper : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("İlk Yüklenecek Sahne")]
    [Tooltip("CoreScene yüklendikten sonra additive olarak yüklenecek ilk sahne.\n" +
             "Boş bırakılırsa otomatik yükleme yapılmaz.")]
    [SerializeField] private string initialScene = "UpgradeScene";

    [Header("Prefab Referansları")]
    [Tooltip("SceneLoader prefab'ı. Boş bırakılırsa otomatik oluşturulur.")]
    [SerializeField] private SceneLoader sceneLoaderPrefab;

    // ── State ─────────────────────────────────────────────────────────────────

    private static bool _isCoreInitialized = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Sadece bir kez çalış — sahne geçişlerinde tekrar tetiklenmesin
        if (_isCoreInitialized)
        {
            Destroy(gameObject);
            return;
        }

        _isCoreInitialized = true;
        DontDestroyOnLoad(gameObject);

        EnsureSceneLoader();

        Debug.Log("[CoreSceneBootstrapper] Core sistemler hazır.");
    }

    private void Start()
    {
        // İlk sahneyi additive olarak yükle — SADECE CoreScene tek başına yüklüyse.
        // Eğer başka bir sahne (StartScene, UpgradeScene vb.) zaten yüklüyse,
        // CoreScene o sahnelere additive olarak eklenmiş demektir; otomatik yükleme yapma.
        if (string.IsNullOrEmpty(initialScene)) return;

        // Kaç sahne yüklü? CoreScene dışında başka sahne varsa otomatik yükleme yapma.
        int loadedCount = SceneManager.sceneCount;
        if (loadedCount > 1)
        {
            Debug.Log($"[CoreSceneBootstrapper] {loadedCount} sahne yüklü. " +
                      $"Otomatik '{initialScene}' yüklemesi atlandı.");
            return;
        }

        // Hedef sahne zaten yüklüyse atla
        Scene existing = SceneManager.GetSceneByName(initialScene);
        if (existing.IsValid() && existing.isLoaded)
        {
            Debug.Log($"[CoreSceneBootstrapper] '{initialScene}' zaten yüklü. Additive yükleme atlandı.");
            return;
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadSceneAdditive(initialScene, onComplete: () =>
            {
                Scene loaded = SceneManager.GetSceneByName(initialScene);
                if (loaded.IsValid())
                    SceneManager.SetActiveScene(loaded);

                Debug.Log($"[CoreSceneBootstrapper] '{initialScene}' aktif sahne olarak ayarlandı.");
            });
        }
    }

    private void OnDestroy()
    {
        if (_isCoreInitialized && Instance == this)
            _isCoreInitialized = false;
    }

    // ── Singleton (opsiyonel — diğer sistemlerin erişimi için) ────────────────

    public static CoreSceneBootstrapper Instance { get; private set; }

    // Awake'de Instance'ı set et (DontDestroyOnLoad öncesinde)
    // Not: Awake'deki _isCoreInitialized kontrolü zaten duplicate'i önler.
    private void OnEnable()
    {
        if (Instance == null) Instance = this;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void EnsureSceneLoader()
    {
        if (SceneLoader.Instance != null) return;

        if (sceneLoaderPrefab != null)
        {
            Instantiate(sceneLoaderPrefab);
        }
        else
        {
            var go = new GameObject("[SceneLoader]");
            go.AddComponent<SceneLoader>();
            DontDestroyOnLoad(go);
        }

        Debug.Log("[CoreSceneBootstrapper] SceneLoader oluşturuldu.");
    }

    /// <summary>
    /// CoreScene'in başlatılmış olup olmadığını sıfırlar.
    /// Sadece Editor testleri için kullanılır.
    /// </summary>
    public static void ResetForTesting()
    {
        _isCoreInitialized = false;
    }
}
