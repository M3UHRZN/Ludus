using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode; // Multiplayer baðlantýsýný kesmek için

public class ReturnToLobby : MonoBehaviour
{
    [Header("Dönülecek Sahne")]
    public string lobbySceneName = "LobbyScene"; 

    public void ButonaBasildi()
    {
        Debug.Log("Lobiye dönülüyor...");

        // 1. Varsa çalan oyun içi müzikleri sustur 
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
        }

        // 2. Multiplayer baðlantýsýný GÜVENLÝ bir þekilde kes 
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // 3. Lobi sahnesini yükle
        SceneManager.LoadScene(lobbySceneName);
    }
}