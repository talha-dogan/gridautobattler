using UnityEngine;

/// <summary>
/// Oyunun ilk başlangıç noktası.
/// DontDestroyOnLoad ile tüm sahnelerde yaşar.
///
/// Sorumluluklar:
///   1. GameSaveService'in var olduğundan emin olur (prefab yoksa oluşturur).
///   2. LocalizationManager'ı başlatır.
///   3. Oyun süresini takip eder.
///
/// Kullanım:
///   StartScene'deki bir GameObject'e bu bileşeni ekle.
///   GameSaveService prefab'ını Inspector'dan ata (opsiyonel — yoksa otomatik oluşturulur).
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Prefab Referansları (Opsiyonel)")]
    [Tooltip("GameSaveService prefab'ı. Boş bırakılırsa otomatik oluşturulur.")]
    [SerializeField] private GameSaveService gameSaveServicePrefab;

    private static bool _isInitialized = false;

    private void Awake()
    {
        // Sadece bir kez çalış
        if (_isInitialized)
        {
            Destroy(gameObject);
            return;
        }

        _isInitialized = true;
        DontDestroyOnLoad(gameObject);

        // GameSaveService'i başlat
        EnsureGameSaveService();

        // Lokalizasyonu başlat (GameSaveService'ten dil kodunu okur)
        LocalizationManager.Initialize();

        Debug.Log("[GameBootstrap] Oyun sistemleri başlatıldı.");
    }

    private void Update()
    {
        // Oyun süresini kaydet (her frame, kapanışta flush edilir)
        GameSaveService.Instance?.AddPlayTime(Time.unscaledDeltaTime);
    }

    private void EnsureGameSaveService()
    {
        if (GameSaveService.Instance != null) return;

        if (gameSaveServicePrefab != null)
        {
            Instantiate(gameSaveServicePrefab);
        }
        else
        {
            // Prefab atanmamışsa boş bir GameObject üzerinde oluştur
            var go = new GameObject("GameSaveService");
            go.AddComponent<GameSaveService>();
            DontDestroyOnLoad(go);
        }
    }
}
