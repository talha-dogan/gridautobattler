// LayerSetup.cs
// Editor-only utility that ensures the "PlayerUnit" and "EnemyUnit" layers
// exist in the project's TagManager. Runs automatically on every domain reload
// so the layers are always present without any manual setup.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LayerSetup
{
    // Layer names used by the targeting system's Physics2D overlap queries.
    public const string PlayerUnitLayer = "PlayerUnit";
    public const string EnemyUnitLayer  = "EnemyUnit";

    static LayerSetup()
    {
        EnsureLayerExists(PlayerUnitLayer);
        EnsureLayerExists(EnemyUnitLayer);
    }

    private static void EnsureLayerExists(string layerName)
    {
        // TagManager is the serialized asset that stores layers.
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

        SerializedProperty layersProp = tagManager.FindProperty("layers");

        // Check whether the layer already exists.
        for (int i = 0; i < layersProp.arraySize; i++)
        {
            if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                return; // Already present — nothing to do.
        }

        // Find the first empty user-layer slot (indices 8–31 are user-assignable).
        for (int i = 8; i < layersProp.arraySize; i++)
        {
            SerializedProperty slot = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[LayerSetup] Created layer '{layerName}' at index {i}.");
                return;
            }
        }

        Debug.LogWarning($"[LayerSetup] Could not create layer '{layerName}': no free layer slots.");
    }
}
#endif
