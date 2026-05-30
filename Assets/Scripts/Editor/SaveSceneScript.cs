using UnityEditor;
using UnityEditor.SceneManagement;

public class SaveSceneScript
{
    public static void Execute()
    {
        EditorSceneManager.SaveOpenScenes();
        UnityEngine.Debug.Log("[SaveSceneScript] All open scenes saved.");
    }
}
