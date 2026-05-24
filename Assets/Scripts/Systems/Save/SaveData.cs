using System;
using System.Collections.Generic;

/// <summary>
/// Oyunun tüm kalıcı verilerini tutan veri modeli.
/// JSON olarak serileştirilir ve binary (AES-şifreli) olarak diske yazılır.
/// </summary>
[Serializable]
public class SaveData
{
    // ─────────────────────────────────────────────────────────────────────────
    // Meta Data
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Save dosyasının format versiyonu. Migration için kullanılır.</summary>
    public int saveVersion = SaveManager.CurrentSaveVersion;

    /// <summary>Kayıt tarihi (ISO 8601 formatında).</summary>
    public string savedAt = DateTime.UtcNow.ToString("o");

    /// <summary>Toplam oyun süresi (saniye).</summary>
    public float totalPlayTimeSeconds = 0f;

    /// <summary>Kaç kez kaydedildi.</summary>
    public int saveCount = 0;

    /// <summary>Veri bütünlüğü için SHA-256 checksum.</summary>
    public string checksum = string.Empty;

    // ─────────────────────────────────────────────────────────────────────────
    // Oyun Verileri
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Oyuncunun mevcut altın miktarı.</summary>
    public int gold = 0;

    /// <summary>Mevcut level index'i (0-tabanlı).</summary>
    public int currentLevelIndex = 0;

    /// <summary>Açılmış pawn sayısı.</summary>
    public int unlockedPawnCount = 1;

    /// <summary>Her army slot için ekipman verileri.</summary>
    public List<ArmySlotSaveData> armySlots = new List<ArmySlotSaveData>();

    /// <summary>Envanterdeki (stash) ekipman asset adları.</summary>
    public List<string> inventoryAssetNames = new List<string>();

    // ─────────────────────────────────────────────────────────────────────────
    // Ayarlar
    // ─────────────────────────────────────────────────────────────────────────

    public float sfxVolume    = 1f;
    public float musicVolume  = 1f;
    public int   qualityLevel = 2;
    public int   resolutionIndex = -1;

    // ─────────────────────────────────────────────────────────────────────────
    // Lokalizasyon
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Seçili dil kodu ("tr" veya "en").</summary>
    public string languageCode = "tr";
}

/// <summary>
/// Tek bir army slot'unun kayıt verisi.
/// </summary>
[Serializable]
public class ArmySlotSaveData
{
    /// <summary>Slot index'i (0-7).</summary>
    public int slotIndex;

    /// <summary>Atanmış ekipman asset adları.</summary>
    public string weaponAssetName  = string.Empty;
    public string helmetAssetName  = string.Empty;
    public string vestAssetName    = string.Empty;
    public string pantsAssetName   = string.Empty;
    public string shieldAssetName  = string.Empty;

    /// <summary>Temel unit data asset adı.</summary>
    public string baseUnitDataName = string.Empty;
}
