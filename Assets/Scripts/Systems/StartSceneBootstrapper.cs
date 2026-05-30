using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// StartScene'e eklenir. Eğer CoreScene yüklü değilse (Editor'da doğrudan
/// StartScene'den Play'e basıldığında) CoreScene'i additive olarak yükler
/// ve singleton'ların hazır olmasını bekler.
/// </summary>
public class StartSceneBootstrapper : MonoBehaviour
{
    private const string CoreSceneName = "CoreScene";

    private IEnumerator Start()
    {
        // CoreScene zaten yüklüyse bir şey yapma
        Scene coreScene = SceneManager.GetSceneByName(CoreSceneName);
        if (coreScene.IsValid() && coreScene.isLoaded)
            yield break;

        Debug.Log("[StartSceneBootstrapper] CoreScene yüklü değil. Additive yükleniyor...");

        AsyncOperation op = SceneManager.LoadSceneAsync(CoreSceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError("[StartSceneBootstrapper] CoreScene yüklenemedi! Build Settings'te kayıtlı mı?");
            yield break;
        }

        yield return op;

        // SceneLoader singleton'ının Awake'ini çalıştırması için bir frame bekle
        yield return null;

        // StartScene'i aktif sahne olarak ayarla (CoreScene değil)
        Scene startScene = SceneManager.GetSceneByName("StartScene");
        if (startScene.IsValid())
            SceneManager.SetActiveScene(startScene);

        Debug.Log("[StartSceneBootstrapper] CoreScene yüklendi. StartScene aktif sahne olarak ayarlandı.");
    }
}
