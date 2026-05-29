using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // Sahne deðiþtirmek için eklendi
using Unity.Netcode; // Sunucudan güvenli çýkýþ yapmak için eklendi

public class UniversalSettings : MonoBehaviour
{
    [Header("Görsel Panel")]
    public GameObject settingsPanel;

    [Header("Ayarlar (Sliderlar)")]
    public Slider volumeSlider;
    public Slider sensitivitySlider; // YENÝ: Fare hassasiyeti çubuðu

    [Header("Sahne Ayarlarý")]
    public string firstMainMenuScene = "esmnr-MainMenu"; // Dönülecek ilk ekranýn adý

    private void Start()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // --- SES YÜKLEME ---
        float savedVolume = PlayerPrefs.GetFloat("GameVolume", 1f);
        AudioListener.volume = savedVolume;
        if (volumeSlider != null) volumeSlider.value = savedVolume;

        // --- YENÝ: HASSASÝYET YÜKLEME ---
        float savedSens = PlayerPrefs.GetFloat("MouseSensitivity", 2f); // Varsayýlan hýz 2.0 olsun
        if (sensitivitySlider != null) sensitivitySlider.value = savedSens;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleSettings();
        }
    }

    public void ToggleSettings()
    {
        if (settingsPanel == null) return;

        bool isActive = settingsPanel.activeSelf;
        settingsPanel.SetActive(!isActive);

        if (!isActive) // Menü açýldýysa
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else // Menü kapandýysa (Oyuna dönüldüyse)
        {
            // Oyundayken farenin tekrar gizlenmesi ve kilitlenmesi lazým
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // --- SLIDER FONKSÝYONLARI ---
    public void OnVolumeChanged(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("GameVolume", volume);
    }

    public void OnSensitivityChanged(float sens)
    {
        // Hassasiyeti diske kaydediyoruz. (Kamera kodunu yazan arkadaþ bu veriyi okuyacak!)
        PlayerPrefs.SetFloat("MouseSensitivity", sens);
    }

    // --- YENÝ EKLENEN BUTON FONKSÝYONLARI ---

    public void ResumeGame()
    {
        ToggleSettings(); // Menüyü kapatýr ve fareyi kilitler
    }

    public void LeaveMatch()
    {
        // 1. Eðer aða baðlýysak baðlantýyý GÜVENLÝ bir þekilde kes
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // 2. Paneli gizle ve fareyi serbest býrak
        if (settingsPanel != null) settingsPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. Ýlk ana menü sahnesine geri dön
        SceneManager.LoadScene(firstMainMenuScene);
    }

    public void QuitGame()
    {
        Debug.Log("Masaüstüne Çýkýlýyor...");
        Application.Quit();
    }
}