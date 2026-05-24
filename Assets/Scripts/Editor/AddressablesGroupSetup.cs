using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Addressables asset gruplarını otomatik olarak kategorize eden editor tool.
///
/// Menü: Tools → TDEV → Setup Addressables Groups
///
/// Oluşturulan gruplar:
///   • Units   — Assets/Art/Unit/ ve Assets/Prefabs/Units/ altındaki assetler
///   • Items   — Assets/Art/Items/ ve Assets/Prefabs/Items/ altındaki assetler
///   • UI      — Assets/Art/BackgraundMenu/ altındaki assetler
///   • Arena   — Assets/Art/Arena/ altındaki assetler
///   • Audio   — Assets/Art/Saund/ altındaki assetler
///
/// Her grup "Packed Assets" şemasıyla oluşturulur (production-ready).
/// Mevcut "Default Local Group" içindeki assetler ilgili gruplara taşınır.
/// </summary>
public static class AddressablesGroupSetup
{
    // ─────────────────────────────────────────────────────────────────────────
    // Grup tanımları: (grup adı, asset path prefix listesi)
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly (string groupName, string[] pathPrefixes)[] GroupDefinitions =
    {
        ("Units",  new[] { "Assets/Art/Unit/",    "Assets/Prefabs/Units/" }),
        ("Items",  new[] { "Assets/Art/Items/",   "Assets/Prefabs/Items/" }),
        ("UI",     new[] { "Assets/Art/BackgraundMenu/" }),
        ("Arena",  new[] { "Assets/Art/Arena/" }),
        ("Audio",  new[] { "Assets/Art/Saund/" }),
    };

    [MenuItem("Tools/TDEV/Setup Addressables Groups")]
    public static void SetupGroups()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

        if (settings == null)
        {
            Debug.LogError("[AddressablesGroupSetup] Addressable Asset Settings bulunamadı. " +
                           "Window → Asset Management → Addressables → Groups menüsünden önce oluşturun.");
            return;
        }

        int totalMoved   = 0;
        int totalCreated = 0;

        foreach (var (groupName, pathPrefixes) in GroupDefinitions)
        {
            // Grubu bul veya oluştur
            AddressableAssetGroup group = GetOrCreateGroup(settings, groupName, out bool wasCreated);
            if (wasCreated) totalCreated++;

            // Bu gruba ait path prefix'leriyle eşleşen assetleri bul ve taşı
            int moved = AssignAssetsToGroup(settings, group, pathPrefixes);
            totalMoved += moved;

            Debug.Log($"[AddressablesGroupSetup] '{groupName}' grubu: {moved} asset atandı.");
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[AddressablesGroupSetup] Tamamlandı. " +
                  $"{totalCreated} yeni grup oluşturuldu, {totalMoved} asset taşındı.");

        EditorUtility.DisplayDialog(
            "Addressables Grupları Kuruldu",
            $"{totalCreated} yeni grup oluşturuldu.\n{totalMoved} asset kategorize edildi.",
            "Tamam");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static AddressableAssetGroup GetOrCreateGroup(
        AddressableAssetSettings settings,
        string groupName,
        out bool wasCreated)
    {
        // Mevcut grubu ara
        AddressableAssetGroup existing = settings.FindGroup(groupName);
        if (existing != null)
        {
            wasCreated = false;
            return existing;
        }

        // Yeni grup oluştur — BundledAssetGroupSchema + ContentUpdateGroupSchema
        var schemas = new List<AddressableAssetGroupSchema>
        {
            ScriptableObject.CreateInstance<BundledAssetGroupSchema>(),
            ScriptableObject.CreateInstance<ContentUpdateGroupSchema>(),
        };

        AddressableAssetGroup newGroup = settings.CreateGroup(
            groupName,
            setAsDefaultGroup: false,
            readOnly: false,
            postEvent: false,
            schemasToCopy: schemas);

        // BundledAssetGroupSchema ayarları
        var bundleSchema = newGroup.GetSchema<BundledAssetGroupSchema>();
        if (bundleSchema != null)
        {
            bundleSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundleSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
        }

        wasCreated = true;
        Debug.Log($"[AddressablesGroupSetup] Yeni grup oluşturuldu: '{groupName}'");
        return newGroup;
    }

    private static int AssignAssetsToGroup(
        AddressableAssetSettings settings,
        AddressableAssetGroup targetGroup,
        string[] pathPrefixes)
    {
        int count = 0;

        // Tüm Addressable entry'lerini tara
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null || group == targetGroup) continue;

            // Entry listesinin kopyasını al (iterasyon sırasında değişebilir)
            var entries = new List<AddressableAssetEntry>(group.entries);

            foreach (AddressableAssetEntry entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.AssetPath)) continue;

                foreach (string prefix in pathPrefixes)
                {
                    if (entry.AssetPath.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    {
                        settings.MoveEntry(entry, targetGroup, readOnly: false, postEvent: false);
                        count++;
                        break;
                    }
                }
            }
        }

        // Henüz Addressable olarak işaretlenmemiş ama path'i eşleşen assetleri ekle
        string[] guids = AssetDatabase.FindAssets("", pathPrefixes);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Klasörleri atla
            if (AssetDatabase.IsValidFolder(path)) continue;

            // .meta dosyalarını atla
            if (path.EndsWith(".meta")) continue;

            // Zaten bu grupta mı?
            AddressableAssetEntry existingEntry = settings.FindAssetEntry(guid);
            if (existingEntry != null)
            {
                if (existingEntry.parentGroup == targetGroup) continue;
                settings.MoveEntry(existingEntry, targetGroup, readOnly: false, postEvent: false);
                count++;
            }
            else
            {
                // Yeni entry oluştur
                AddressableAssetEntry newEntry = settings.CreateOrMoveEntry(guid, targetGroup, readOnly: false, postEvent: false);
                if (newEntry != null)
                {
                    // Address olarak dosya adını kullan (uzantısız)
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                    newEntry.address = fileName;
                    count++;
                }
            }
        }

        return count;
    }
}
