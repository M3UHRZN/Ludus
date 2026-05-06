using UnityEngine;

public class MockTest : MonoBehaviour
{
    void Update()
    {
        // --- 1. ADIM: OYUNCU SAYISINI AYARLAMA ---
        // Klavyeden '4' tuþuna basarsak 4 kiþilik oyun baþlasýn
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            FindObjectOfType<HUDController>().SetPlayerCount(4);
            Debug.Log("Oyun 4 kiþi baþladý!");
        }

        // Klavyeden '6' tuþuna basarsak 6 kiþilik oyun baþlasýn
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            FindObjectOfType<HUDController>().SetPlayerCount(6);
            Debug.Log("Oyun 6 kiþi baþladý!");
        }

        // --- 2. ADIM: ÖLÜM TESTLERÝ ---
        // 'K' tuþu -> 1. oyuncuyu öldür (Ýndeks 0)
        if (Input.GetKeyDown(KeyCode.K))
        {
            GameEventBus.Publish(new PlayerDiedEvent(0, Vector3.zero));
            Debug.Log("1. Oyuncu öldü!");
        }

        // 'L' tuþu -> 2. oyuncuyu öldür (Ýndeks 1)
        if (Input.GetKeyDown(KeyCode.L))
        {
            GameEventBus.Publish(new PlayerDiedEvent(1, Vector3.zero));
            Debug.Log("2. Oyuncu öldü!");
        }

        // 'M' tuþu -> 4. oyuncuyu öldür (Ýndeks 3)
        if (Input.GetKeyDown(KeyCode.M))
        {
            GameEventBus.Publish(new PlayerDiedEvent(3, Vector3.zero));
            Debug.Log("4. Oyuncu öldü!");
        }

        // --- 3. ADIM: AÐIRLIK BARI TESTLERÝ ---
        // GDD'ye göre aðýrlýklar: Small = 1, Medium = 3, Large = 6

        // Klavyeden 'Z' tuþuna basarsak Küçük (Small) eþya alalým (Aðýrlýk: 1)
        if (Input.GetKeyDown(KeyCode.Z))
        {
            // ItemPickedUpEvent struct'ýn 3 parametre istiyordu: isim, aðýrlýk, kredi deðeri
            GameEventBus.Publish(new ItemPickedUpEvent("Küçük Hurda", 1, 10f));
            Debug.Log("Mock: Küçük eþya alýndý (+1 Aðýrlýk)");
        }

        // Klavyeden 'X' tuþuna basarsak Orta (Medium) eþya alalým (Aðýrlýk: 3)
        if (Input.GetKeyDown(KeyCode.X))
        {
            GameEventBus.Publish(new ItemPickedUpEvent("Orta Boy Motor", 3, 40f));
            Debug.Log("Mock: Orta eþya alýndý (+3 Aðýrlýk)");
        }

        // Klavyeden 'C' tuþuna basarsak Büyük (Large) eþya alalým (Aðýrlýk: 6)
        if (Input.GetKeyDown(KeyCode.C))
        {
            GameEventBus.Publish(new ItemPickedUpEvent("Aðýr Çekirdek", 6, 100f));
            Debug.Log("Mock: Büyük eþya alýndý (+6 Aðýrlýk)");
        }






        // --- SPRINT 2: EXTRACTION EKRANI TESTÝ ---
        // 'E' tuþuna basarsak Baþarýlý ve Cezalý bir tur sonu görelim
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Senaryo: Baþarýlý, 450 kredi topladýk, 112 ceza yedik, Kota %80 doldu (0.8f)
            FindObjectOfType<ExtractionUIController>().ShowExtractionScreen(true, 450, 112, 0.8f);
            Debug.Log("Mock: Baþarýlý tur sonu ekraný açýldý!");
        }

        // 'R' tuþuna basarsak Baþarýsýz ve Cezasýz bir tur sonu görelim
        if (Input.GetKeyDown(KeyCode.R))
        {
            // Senaryo: Baþarýsýz, 120 kredi topladýk, 0 ceza, Kota %30 doldu (0.3f)
            FindObjectOfType<ExtractionUIController>().ShowExtractionScreen(false, 120, 0, 0.3f);
            Debug.Log("Mock: Baþarýsýz tur sonu ekraný açýldý!");
        }
    }
}
