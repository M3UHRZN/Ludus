using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Sahne Ayarlarý")]
    public string lobbySceneName = "LobbyScene";

    public void OnPlayClicked()
    {
        // Loading ekranýný çaðýrýyoruz!
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadSceneLocal(lobbySceneName);
        }
        else
        {
            // Güvenlik Önlemi: Eðer test yaparken LoadingManager sahnede yoksa oyun çökmesin diye direk yükleme yapýyoruz.
            SceneManager.LoadScene(lobbySceneName);
        }
    }

    public void OnQuitClicked()
    {
        Debug.Log("Oyundan Çýkýlýyor...");
        Application.Quit();
    }
}