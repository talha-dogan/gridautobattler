using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyunun lokalizasyon sistemi.
/// Resources/Localization/ klasöründeki JSON dosyalarından dil verisi yükler.
///
/// Desteklenen diller: "tr" (Türkçe), "en" (İngilizce)
/// Varsayılan dil: "tr"
///
/// Kullanım:
///   // Basit metin al
///   string text = LocalizationManager.Get(LocalizationKeys.UI_START_BUTTON);
///
///   // Parametreli metin al ({0}, {1} yer tutucuları)
///   string text = LocalizationManager.Get(LocalizationKeys.BATTLE_LEVEL_CLEARED, goldAmount);
///
///   // Dil değiştir
///   LocalizationManager.SetLanguage("en");
///
///   // Dil değişikliğini dinle
///   LocalizationManager.OnLanguageChanged += RefreshUI;
/// </summary>
public static class LocalizationManager
{
    // ─────────────────────────────────────────────────────────────────────────
    // Sabitler
    // ─────────────────────────────────────────────────────────────────────────

    public const string DefaultLanguage      = "tr";
    private const string LocalizationFolder  = "Localization/";
    private const string FallbackLanguage    = "en";

    // ─────────────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────────────

    private static string _currentLanguage = DefaultLanguage;
    private static Dictionary<string, string> _strings = new Dictionary<string, string>();
    private static Dictionary<string, string> _fallbackStrings = new Dictionary<string, string>();
    private static bool _isInitialized = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Dil değiştiğinde tüm UI bileşenlerini yenilemek için abone olun.</summary>
    public static event Action OnLanguageChanged;

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Mevcut dil kodunu döner ("tr" veya "en").</summary>
    public static string CurrentLanguage => _currentLanguage;

    /// <summary>
    /// Lokalizasyon sistemini başlatır.
    /// GameSaveService varsa kayıtlı dili yükler, yoksa varsayılan dili kullanır.
    /// </summary>
    public static void Initialize()
    {
        string lang = DefaultLanguage;

        if (GameSaveService.Instance != null)
            lang = GameSaveService.Instance.GetLanguageCode();

        LoadLanguage(lang, silent: true);
        LoadFallback();
        _isInitialized = true;

        Debug.Log($"[LocalizationManager] Başlatıldı. Dil: '{_currentLanguage}'");
    }

    /// <summary>
    /// Dili değiştirir, yeni dil dosyasını yükler ve OnLanguageChanged olayını tetikler.
    /// </summary>
    public static void SetLanguage(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode)) return;
        if (_currentLanguage == languageCode && _isInitialized) return;

        LoadLanguage(languageCode, silent: false);
        GameSaveService.Instance?.SetLanguageCode(_currentLanguage);
        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// Verilen key için lokalize metni döner.
    /// Key bulunamazsa önce fallback dile, sonra key'in kendisine döner.
    /// </summary>
    public static string Get(string key)
    {
        if (!_isInitialized) Initialize();

        if (_strings.TryGetValue(key, out string value))
            return value;

        if (_fallbackStrings.TryGetValue(key, out string fallback))
        {
            Debug.LogWarning($"[LocalizationManager] '{key}' anahtarı '{_currentLanguage}' dilinde bulunamadı. Fallback kullanılıyor.");
            return fallback;
        }

        Debug.LogWarning($"[LocalizationManager] '{key}' anahtarı hiçbir dilde bulunamadı.");
        return key;
    }

    /// <summary>
    /// Parametreli lokalize metin döner.
    /// JSON'daki {0}, {1} yer tutucuları verilen argümanlarla değiştirilir.
    ///
    /// Örnek: Get("battle.level_cleared", 150) → "SEVİYE GEÇİLDİ!\nTemel Ödül: 150 Altın"
    /// </summary>
    public static string Get(string key, params object[] args)
    {
        string raw = Get(key);
        try
        {
            return string.Format(raw, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[LocalizationManager] '{key}' için string.Format başarısız. Ham değer döndürülüyor.");
            return raw;
        }
    }

    /// <summary>
    /// Desteklenen tüm dil kodlarını döner.
    /// </summary>
    public static string[] GetSupportedLanguages() => new[] { "tr", "en" };

    /// <summary>
    /// Desteklenen dillerin görünen isimlerini döner (dropdown için).
    /// </summary>
    public static string[] GetLanguageDisplayNames() => new[] { "Türkçe", "English" };

    // ─────────────────────────────────────────────────────────────────────────
    // Private — Yükleme
    // ─────────────────────────────────────────────────────────────────────────

    private static void LoadLanguage(string languageCode, bool silent)
    {
        var loaded = LoadJsonFile(languageCode);

        if (loaded == null)
        {
            if (!silent)
                Debug.LogError($"[LocalizationManager] '{languageCode}' dil dosyası yüklenemedi!");
            return;
        }

        _strings = loaded;
        _currentLanguage = languageCode;

        if (!silent)
            Debug.Log($"[LocalizationManager] Dil yüklendi: '{languageCode}' ({_strings.Count} anahtar)");
    }

    private static void LoadFallback()
    {
        if (_currentLanguage == FallbackLanguage)
        {
            _fallbackStrings = _strings;
            return;
        }

        var loaded = LoadJsonFile(FallbackLanguage);
        _fallbackStrings = loaded ?? new Dictionary<string, string>();
    }

    private static Dictionary<string, string> LoadJsonFile(string languageCode)
    {
        string path = LocalizationFolder + languageCode;
        TextAsset asset = Resources.Load<TextAsset>(path);

        if (asset == null)
        {
            Debug.LogWarning($"[LocalizationManager] Dosya bulunamadı: Resources/{path}.json");
            return null;
        }

        try
        {
            var wrapper = JsonUtility.FromJson<LocalizationWrapper>(
                "{\"entries\":" + ConvertJsonObjectToArray(asset.text) + "}");

            var dict = new Dictionary<string, string>();
            if (wrapper?.entries != null)
            {
                foreach (var entry in wrapper.entries)
                    dict[entry.key] = entry.value;
            }

            return dict;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalizationManager] JSON parse hatası ({languageCode}): {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// {"key":"value",...} formatındaki JSON'u JsonUtility'nin anlayacağı
    /// [{"key":"...","value":"..."},...] array formatına dönüştürür.
    /// </summary>
    private static string ConvertJsonObjectToArray(string json)
    {
        // Basit JSON object → array dönüşümü
        // {"a":"1","b":"2"} → [{"key":"a","value":"1"},{"key":"b","value":"2"}]
        json = json.Trim();
        if (json.StartsWith("{")) json = json.Substring(1);
        if (json.EndsWith("}"))   json = json.Substring(0, json.Length - 1);

        var result = new System.Text.StringBuilder("[");
        bool first = true;

        // Satır satır parse et (her satır bir key:value çifti)
        string[] lines = json.Split('\n');
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim().TrimEnd(',');
            if (string.IsNullOrWhiteSpace(line)) continue;

            // "key": "value" formatını bul
            int colonIdx = line.IndexOf("\":", StringComparison.Ordinal);
            if (colonIdx < 0) continue;

            string keyPart   = line.Substring(0, colonIdx + 1).Trim().Trim('"');
            string valuePart = line.Substring(colonIdx + 2).Trim().Trim('"');

            // Escape karakterlerini koru
            keyPart   = keyPart.Replace("\\", "\\\\").Replace("\"", "\\\"");
            valuePart = valuePart.Replace("\\", "\\\\").Replace("\"", "\\\"");

            if (!first) result.Append(",");
            result.Append($"{{\"key\":\"{keyPart}\",\"value\":\"{valuePart}\"}}");
            first = false;
        }

        result.Append("]");
        return result.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Serialization helpers (JsonUtility için)
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    private class LocalizationEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    private class LocalizationWrapper
    {
        public List<LocalizationEntry> entries;
    }
}
