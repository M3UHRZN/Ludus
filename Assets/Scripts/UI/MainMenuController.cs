using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Slider (Sürgülü bar) kontrolü için þart!

public class MainMenuController : MonoBehaviour
{
    [Header("Paneller")]
    public GameObject mainPanel;
    public GameObject settingsPanel;

    [Header("Ses Ayarlarý")]
    public Slider volumeSlider; // Inspector'dan slider'ý buraya baðlayacaðýz

    private void Start()
    {
        // Oyun ilk açýldýðýnda ana menü aktif, ayarlar gizli olsun
        mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Bilgisayara kaydedilmiþ eski bir ses ayarý varsa onu yükle, yoksa son ses (1f) aç
        if (volumeSlider != null)
        {
            volumeSlider.value = PlayerPrefs.GetFloat("GameVolume", 1f);
            AudioListener.volume = volumeSlider.value; // Unity'nin ana ses beynini güncelle

            // Slider hareket ettirildiðinde "SetVolume" fonksiyonunu otomatik çaðýr
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    // Sürgülü barý her kaydýrdýðýmýzda bu fonksiyon çalýþýr
    public void SetVolume(float value)
    {
        AudioListener.volume = value; // Tüm oyunun sesini (0 ile 1 arasýnda) deðiþtirir
        PlayerPrefs.SetFloat("GameVolume", value); // Oyuncunun ayarýný bilgisayara kaydet
    }

    public void PlayGame()
    {
        // AudioManager.Instance.PlaySFX(AudioManager.Instance.clickSound);
        SceneManager.LoadScene("LobbyScene"); // Lobi sahnesine geçiþ yap
    }

    public void OpenSettings()
    {
        // AudioManager.Instance.PlaySFX(AudioManager.Instance.clickSound);
        mainPanel.SetActive(false); // Ana butonlarý gizle
        settingsPanel.SetActive(true); // Ayarlar panelini göster
    }

    public void CloseSettings()
    {
        // AudioManager.Instance.PlaySFX(AudioManager.Instance.clickSound);
        settingsPanel.SetActive(false); // Ayarlar panelini gizle
        mainPanel.SetActive(true); // Ana butonlarý geri getir
    }

    public void QuitGame()
    {
        // AudioManager.Instance.PlaySFX(AudioManager.Instance.clickSound);
        Debug.Log("Oyundan çýkýlýyor...");
        Application.Quit(); // Oyunu kapatýr
    }
}