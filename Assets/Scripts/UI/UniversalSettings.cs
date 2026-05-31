using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // Sahne deðiþtirmek için eklendi
using Unity.Netcode; // Sunucudan güvenli çýkýþ yapmak için eklendi
using UnityEngine.Audio; // AudioMixer kütüphanesi eklendi!

public class UniversalSettings : MonoBehaviour
{
    [Header("Görsel Panel")]
    public GameObject settingsPanel;

    [Header("DJ Masasý ve Ses Ayarlarý (Sliderlar)")]
    public AudioMixer mainMixer;     // Ürettiðimiz MainMixer buraya sürüklenecek
    public Slider musicSlider;       // Lobi/Arka plan müziði için
    public Slider sfxSlider;         // Ayak sesi, düþman, zýplama için
    public Slider uiSlider;          // Buton, satýn alma vs. için

    [Header("Ayarlar (Sliderlar)")]
    public Slider volumeSlider;
    public Slider sensitivitySlider; // Fare hassasiyeti çubuðu

    [Header("Sahne Ayarlarý")]
    public string firstMainMenuScene = "esmnr-MainMenu"; // Dönülecek ilk ekranýn adý

    private void Start()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // --- SESLERÝ YÜKLEME ---
        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float savedSFX = PlayerPrefs.GetFloat("SFXVolume", 1f);
        float savedUI = PlayerPrefs.GetFloat("UIVolume", 1f);

        // Müzik Slider'ýný Ayarla ve Dinlemeye Baþla
        if (musicSlider != null)
        {
            musicSlider.value = savedMusic;
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
            SetMusicVolume(savedMusic); // Oyun açýlýr açýlmaz sesi DJ masasýna uygula
        }

        // SFX Slider'ýný Ayarla ve Dinlemeye Baþla
        if (sfxSlider != null)
        {
            sfxSlider.value = savedSFX;
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
            SetSFXVolume(savedSFX);
        }

        // UI Slider'ýný Ayarla ve Dinlemeye Baþla
        if (uiSlider != null)
        {
            uiSlider.value = savedUI;
            uiSlider.onValueChanged.AddListener(SetUIVolume);
            SetUIVolume(savedUI);
        }

        // --- HASSASÝYET YÜKLEME ---
        float savedSens = PlayerPrefs.GetFloat("MouseSensitivity", 2f);
        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = savedSens;
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }
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

        if (!isActive) // Menü AÇILDIYSA
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else // Menü KAPANDIYSA
        {
            // Sadece Ana Menüde DEÐÝLSEK fareyi kilitle
            if (SceneManager.GetActiveScene().name != firstMainMenuScene)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    // --- 3'LÜ SES FONKSÝYONLARI ---
    public void SetMusicVolume(float sliderValue)
    {
        if (mainMixer != null) mainMixer.SetFloat("MusicVol", Mathf.Log10(sliderValue) * 20);
        PlayerPrefs.SetFloat("MusicVolume", sliderValue);
    }

    public void SetSFXVolume(float sliderValue)
    {
        if (mainMixer != null) mainMixer.SetFloat("SFXVol", Mathf.Log10(sliderValue) * 20);
        PlayerPrefs.SetFloat("SFXVolume", sliderValue);
    }

    public void SetUIVolume(float sliderValue)
    {
        if (mainMixer != null) mainMixer.SetFloat("UIVol", Mathf.Log10(sliderValue) * 20);
        PlayerPrefs.SetFloat("UIVolume", sliderValue);
    }

    // --- HASSASÝYET FONKSÝYONU ---
    public void OnSensitivityChanged(float sens)
    {
        // Hassasiyeti diske kaydediyoruz.
        PlayerPrefs.SetFloat("MouseSensitivity", sens);
    }

    // --- BUTON FONKSÝYONLARI ---

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