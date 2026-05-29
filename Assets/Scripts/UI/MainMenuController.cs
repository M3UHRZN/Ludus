using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Sahne Ayarlarý")]
    public string lobbySceneName = "LobbyScene"; 

    public void OnPlayClicked()
    {
        SceneManager.LoadScene(lobbySceneName);
    }

    //public void OnOptionsClicked()
    //{
    //    // Burada Ayarlar panelini açmak için UniversalSettings script'ine eriþebiliriz.
    //    // Eðer UniversalSettings script'i ana menü sahnesinde deðilse, bu fonksiyonun çalýþmasý için o script'in de ana menü sahnesine eklenmesi gerekir.
    //    UniversalSettings settings = FindObjectOfType<UniversalSettings>();
    //    if (settings != null)
    //    {
    //        settings.ToggleSettings();
    //    }
    //    else
    //    {
    //        Debug.LogWarning("UniversalSettings script'i bulunamadý! Ayarlar paneli açýlamayacak.");
    //    }
    //}

    public void OnQuitClicked()
    {
        Debug.Log("Oyundan Çýkýlýyor...");
        Application.Quit();
    }
}