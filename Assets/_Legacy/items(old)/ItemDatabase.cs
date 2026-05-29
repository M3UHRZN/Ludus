using UnityEngine;
using System.Collections.Generic;

// Bu sýnýf her bir eþyanýn kimlik kartýdýr
[System.Serializable]
public class ItemData
{
    public ushort ItemId;       // Eþyanýn Numarasý (Örn: 0)
    public string ItemName;     // Eþyanýn Adý (Örn: "Yeþil Küp")
    public GameObject Prefab;   // 3D Modeli (Ele almak / yere atmak için)
    public Sprite Icon;         // 2D Resmi (UI / Çanta slotu için)
}

public class ItemDatabase : MonoBehaviour
{
    // Singleton yapýsý: Kodun her yerden kolayca çaðrýlmasýný saðlar
    public static ItemDatabase Instance { get; private set; }

    [Header("Tüm Oyundaki Eþyalar")]
    [Tooltip("Oyundaki her eþyayý buraya ekleyin ve ID'lerini belirleyin.")]
    public List<ItemData> AllItems = new List<ItemData>();

    private void Awake()
    {
        // Oyunda sadece 1 tane ItemDatabase olduðundan emin oluyoruz
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Sahneler deðiþse bile silinmesin
    }

    // ID'sini verdiðimiz eþyanýn 3D Prefab'ýný bulur
    public GameObject GetPrefab(ushort id)
    {
        var item = AllItems.Find(x => x.ItemId == id);
        if (item != null) return item.Prefab;

        Debug.LogError($"[ItemDatabase] HATA: {id} ID'li eþya veritabanýnda yok!");
        return null;
    }

    // ID'sini verdiðimiz eþyanýn 2D Resmini (UI Ýçin) bulur
    public Sprite GetIcon(ushort id)
    {
        var item = AllItems.Find(x => x.ItemId == id);
        if (item != null) return item.Icon;
        return null;
    }
}