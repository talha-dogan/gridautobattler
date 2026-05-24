using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// Tüm oyun sistemleri ile SaveManager arasındaki köprü.
/// Singleton MonoBehaviour — DontDestroyOnLoad ile sahneler arası yaşar.
///
/// Sorumluluklar:
///   • Oyun başlangıcında save dosyasını yükler ve sistemlere dağıtır.
///   • Kritik değişikliklerde (altın, level, ekipman) otomatik kaydeder.
///   • Uygulama kapanırken son kaydı yapar.
/// </summary>
public class GameSaveService : MonoBehaviour
{
    public static GameSaveService Instance { get; private set; }

    // Bellekteki aktif save verisi
    private SaveData _current;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _current = SaveManager.Load();
    }

    private void OnApplicationQuit()
    {
        Flush();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Flush();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Okuma
    // ─────────────────────────────────────────────────────────────────────────

    public SaveData GetData() => _current;

    public int   GetGold()              => _current.gold;
    public int   GetLevelIndex()        => _current.currentLevelIndex;
    public int   GetUnlockedPawnCount() => _current.unlockedPawnCount;
    public float GetSFXVolume()         => _current.sfxVolume;
    public float GetMusicVolume()       => _current.musicVolume;
    public int   GetQualityLevel()      => _current.qualityLevel;
    public int   GetResolutionIndex()   => _current.resolutionIndex;
    public string GetLanguageCode()     => _current.languageCode;
    public float GetTotalPlayTime()     => _current.totalPlayTimeSeconds;

    public List<ArmySlotSaveData>  GetArmySlots()   => _current.armySlots;
    public List<string>            GetInventory()   => _current.inventoryAssetNames;

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Yazma (otomatik flush)
    // ─────────────────────────────────────────────────────────────────────────

    public void SetGold(int value)
    {
        _current.gold = value;
        Flush();
    }

    public void SetLevelIndex(int index)
    {
        _current.currentLevelIndex = index;
        Flush();
    }

    public void SetUnlockedPawnCount(int count)
    {
        _current.unlockedPawnCount = count;
        Flush();
    }

    public void SetSFXVolume(float value)
    {
        _current.sfxVolume = value;
        Flush();
    }

    public void SetMusicVolume(float value)
    {
        _current.musicVolume = value;
        Flush();
    }

    public void SetQualityLevel(int index)
    {
        _current.qualityLevel = index;
        Flush();
    }

    public void SetResolutionIndex(int index)
    {
        _current.resolutionIndex = index;
        Flush();
    }

    public void SetLanguageCode(string code)
    {
        _current.languageCode = code;
        Flush();
    }

    public void AddPlayTime(float seconds)
    {
        _current.totalPlayTimeSeconds += seconds;
        // Oyun süresi sık değiştiği için flush tetiklemez; kapanışta kaydedilir.
    }

    /// <summary>
    /// Army slot ekipmanlarını günceller ve kaydeder.
    /// </summary>
    public void SetArmySlots(List<ArmySlotSaveData> slots)
    {
        _current.armySlots = slots ?? new List<ArmySlotSaveData>();
        Flush();
    }

    /// <summary>
    /// Envanter listesini günceller ve kaydeder.
    /// </summary>
    public void SetInventory(List<string> assetNames)
    {
        _current.inventoryAssetNames = assetNames ?? new List<string>();
        Flush();
    }

    /// <summary>
    /// Tüm oyun verisini sıfırlar (ayarlar korunur).
    /// </summary>
    public void ResetGameData()
    {
        float sfx     = _current.sfxVolume;
        float music   = _current.musicVolume;
        int   quality = _current.qualityLevel;
        int   res     = _current.resolutionIndex;
        string lang   = _current.languageCode;

        _current = new SaveData
        {
            sfxVolume       = sfx,
            musicVolume     = music,
            qualityLevel    = quality,
            resolutionIndex = res,
            languageCode    = lang
        };

        Flush();
        Debug.Log("[GameSaveService] Oyun verisi sıfırlandı (ayarlar korundu).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Mevcut veriyi diske yazar.</summary>
    private void Flush()
    {
        SaveManager.Save(_current);
    }
}
