using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Oyunun merkezi kayıt/yükleme sistemi.
///
/// Özellikler:
///   • Binary + AES şifreleme ile güvenli dosya yazma
///   • SHA-256 checksum ile veri bütünlüğü doğrulama
///   • Meta data (versiyon, tarih, oyun süresi, kayıt sayısı)
///   • Otomatik migration (eski save versiyonlarını yükseltir)
///   • PlayerPrefs'ten tamamen bağımsız
///
/// Kullanım:
///   SaveManager.Save(data);
///   SaveData data = SaveManager.Load();
/// </summary>
public static class SaveManager
{
    // ─────────────────────────────────────────────────────────────────────────
    // Sabitler
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Mevcut save format versiyonu. Her breaking change'de artır.</summary>
    public const int CurrentSaveVersion = 2;

    private const string SaveFileName  = "gamesave.dat";
    private const string BackupSuffix  = ".bak";

    // AES şifreleme için sabit anahtar ve IV (production'da güvenli bir yerde saklanmalı)
    private static readonly byte[] AesKey = Encoding.UTF8.GetBytes("TDEV_GridBattler_AES_Key_32Bytes");// 32 byte
    private static readonly byte[] AesIV  = Encoding.UTF8.GetBytes("TDEV_AES_IV_16B!");                  // 16 byte

    // ─────────────────────────────────────────────────────────────────────────
    // Dosya Yolu
    // ─────────────────────────────────────────────────────────────────────────

    private static string SaveFilePath   => Path.Combine(Application.persistentDataPath, SaveFileName);
    private static string BackupFilePath => SaveFilePath + BackupSuffix;

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SaveData'yı diske yazar.
    /// Meta data otomatik güncellenir (savedAt, saveCount, checksum).
    /// Mevcut dosya önce yedeklenir.
    /// </summary>
    public static void Save(SaveData data)
    {
        if (data == null)
        {
            Debug.LogError("[SaveManager] Save çağrıldı ama data null!");
            return;
        }

        // Meta data güncelle
        data.saveVersion = CurrentSaveVersion;
        data.savedAt     = DateTime.UtcNow.ToString("o");
        data.saveCount++;

        // Checksum hesapla (checksum alanı hariç)
        data.checksum = string.Empty;
        string json   = JsonUtility.ToJson(data, prettyPrint: false);
        data.checksum = ComputeChecksum(json);

        // Tekrar serialize et (checksum dahil)
        json = JsonUtility.ToJson(data, prettyPrint: false);

        try
        {
            // Mevcut dosyayı yedekle
            if (File.Exists(SaveFilePath))
                File.Copy(SaveFilePath, BackupFilePath, overwrite: true);

            // Şifrele ve yaz
            byte[] encrypted = Encrypt(json);
            File.WriteAllBytes(SaveFilePath, encrypted);

            Debug.Log($"[SaveManager] Kaydedildi → v{data.saveVersion} | #{data.saveCount} | {data.savedAt}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Kayıt hatası: {ex.Message}");
        }
    }

    /// <summary>
    /// Diskten SaveData yükler.
    /// Dosya yoksa veya bozuksa yeni bir SaveData döner.
    /// Eski versiyon ise migration uygulanır.
    /// </summary>
    public static SaveData Load()
    {
        // Önce ana dosyayı dene, bozuksa yedeği dene
        SaveData data = TryLoadFile(SaveFilePath)
                     ?? TryLoadFile(BackupFilePath);

        if (data == null)
        {
            Debug.Log("[SaveManager] Save dosyası bulunamadı. Yeni oyun başlatılıyor.");
            return new SaveData();
        }

        // Migration uygula
        data = SaveMigrationService.Migrate(data);

        Debug.Log($"[SaveManager] Yüklendi → v{data.saveVersion} | #{data.saveCount} | {data.savedAt}");
        return data;
    }

    /// <summary>
    /// Save dosyasını siler (oyun sıfırlama için).
    /// </summary>
    public static void DeleteSave()
    {
        try
        {
            if (File.Exists(SaveFilePath))   File.Delete(SaveFilePath);
            if (File.Exists(BackupFilePath)) File.Delete(BackupFilePath);
            Debug.Log("[SaveManager] Save dosyası silindi.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Silme hatası: {ex.Message}");
        }
    }

    /// <summary>
    /// Save dosyasının var olup olmadığını döner.
    /// </summary>
    public static bool HasSave() => File.Exists(SaveFilePath);

    // ─────────────────────────────────────────────────────────────────────────
    // Private — Dosya Okuma
    // ─────────────────────────────────────────────────────────────────────────

    private static SaveData TryLoadFile(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            byte[] encrypted = File.ReadAllBytes(path);
            string json      = Decrypt(encrypted);

            SaveData data = JsonUtility.FromJson<SaveData>(json);

            if (!ValidateChecksum(data))
            {
                Debug.LogWarning($"[SaveManager] Checksum doğrulaması başarısız: {path}");
                return null;
            }

            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveManager] Dosya okunamadı ({path}): {ex.Message}");
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private — Checksum
    // ─────────────────────────────────────────────────────────────────────────

    private static string ComputeChecksum(string json)
    {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(hash);
    }

    private static bool ValidateChecksum(SaveData data)
    {
        if (data == null) return false;

        string storedChecksum = data.checksum;
        data.checksum = string.Empty;

        string json     = JsonUtility.ToJson(data, prettyPrint: false);
        string computed = ComputeChecksum(json);

        data.checksum = storedChecksum;
        return storedChecksum == computed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private — AES Şifreleme
    // ─────────────────────────────────────────────────────────────────────────

    private static byte[] Encrypt(string plainText)
    {
        using Aes aes = Aes.Create();
        aes.Key     = AesKey;
        aes.IV      = AesIV;
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
        return encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
    }

    private static string Decrypt(byte[] cipherBytes)
    {
        using Aes aes = Aes.Create();
        aes.Key     = AesKey;
        aes.IV      = AesIV;
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using ICryptoTransform decryptor = aes.CreateDecryptor();
        byte[] decrypted = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(decrypted);
    }
}
