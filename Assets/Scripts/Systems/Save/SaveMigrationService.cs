using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eski save versiyonlarını mevcut versiyona yükseltir.
/// Her versiyon geçişi için ayrı bir migration metodu tanımlanır.
/// SaveManager.Load() tarafından otomatik olarak çağrılır.
/// </summary>
public static class SaveMigrationService
{
    /// <summary>
    /// Verilen SaveData'yı CurrentSaveVersion'a kadar adım adım migrate eder.
    /// </summary>
    public static SaveData Migrate(SaveData data)
    {
        if (data == null) return new SaveData();

        int startVersion = data.saveVersion;

        while (data.saveVersion < SaveManager.CurrentSaveVersion)
        {
            switch (data.saveVersion)
            {
                case 1:
                    data = MigrateV1ToV2(data);
                    break;
                default:
                    Debug.LogWarning($"[SaveMigration] Bilinmeyen versiyon: {data.saveVersion}. Migration durduruluyor.");
                    data.saveVersion = SaveManager.CurrentSaveVersion;
                    break;
            }
        }

        if (startVersion != data.saveVersion)
            Debug.Log($"[SaveMigration] v{startVersion} → v{data.saveVersion} migration tamamlandı.");

        return data;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Migration Adımları
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// v1 → v2: inventoryAssetNames listesi ve languageCode alanı eklendi.
    /// Eksik alanlar varsayılan değerlerle doldurulur.
    /// </summary>
    private static SaveData MigrateV1ToV2(SaveData old)
    {
        if (old.inventoryAssetNames == null)
            old.inventoryAssetNames = new List<string>();

        if (string.IsNullOrEmpty(old.languageCode))
            old.languageCode = "tr";

        if (old.armySlots == null)
            old.armySlots = new List<ArmySlotSaveData>();

        old.saveVersion = 2;
        return old;
    }
}
