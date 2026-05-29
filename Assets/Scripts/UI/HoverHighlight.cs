using UnityEngine;
using UnityEngine.EventSystems; // Fare (Mouse) hareketlerini algýlamak için þart!
using UnityEngine.UI;
using TMPro;

// IPointerEnterHandler (Fare üstüne gelince) ve IPointerExitHandler (Fare çýkýnca) çalýþýr
public class HoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Arka Plan Ayarlarý")]
    public Image rowBackgroundImage;
    public Color normalBgColor = new Color(0f, 0f, 0f, 0f); // Varsayýlan: Tamamen Saydam (Görünmez)
    public Color hoverBgColor = new Color(0.95f, 0.76f, 0.2f, 1f); // Mimesis Sarýsý (Tatlý bir sarý/turuncu)

    [Header("Yazý Ayarlarý (Ýsteðe Baðlý)")]
    public TextMeshProUGUI settingText;
    public Color normalTextColor = Color.white; // Faresizken beyaz
    public Color hoverTextColor = Color.black;  // Arka plan sarý olunca yazý siyah olsun (Okunabilirlik için)

    private void Start()
    {
        // Oyun baþlarken varsayýlan renklere dön
        if (rowBackgroundImage != null) rowBackgroundImage.color = normalBgColor;
        if (settingText != null) settingText.color = normalTextColor;
    }

    // FARE SATIRIN ÜSTÜNE GELDÝÐÝNDE
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (rowBackgroundImage != null) rowBackgroundImage.color = hoverBgColor;
        if (settingText != null) settingText.color = hoverTextColor;
    }

    // FARE SATIRDAN ÇIKTIÐINDA
    public void OnPointerExit(PointerEventData eventData)
    {
        if (rowBackgroundImage != null) rowBackgroundImage.color = normalBgColor;
        if (settingText != null) settingText.color = normalTextColor;
    }
}