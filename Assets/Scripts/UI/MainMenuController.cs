using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Sahne Ayarlarý")]
    public string lobbySceneName = "LobbyScene";

    // --- OYUN AÇILINCA SES CALISIR ---
    private void Start()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.menuMusic != null)
        {
            AudioManager.Instance.PlayMusic(AudioManager.Instance.menuMusic);
        }
    }
    // --------------------------------------------------

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