using UnityEditor;
using UnityEditor.SceneManagement;

public class SaveActiveScene
{
    public static void Execute()
    {
        EditorSceneManager.SaveOpenScenes();
    }
}
