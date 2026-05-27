using System;
using UnityEngine;

[Serializable]
public class DecorPropEntry
{
    [Tooltip("Yerleştirilecek dekor modeli prefabı")]
    public GameObject prefab;

    [Tooltip("Bu model hangi anchor kategorilerine uygun")]
    public PropCategory[] categories = new PropCategory[0];

    [Tooltip("Ağırlıklı random seçim ağırlığı (0 = asla seçilmez)")]
    [Min(0f)] public float weight = 1f;

    [Tooltip("Y ekseninde rastgele döndür")]
    public bool randomYaw = true;

    [Tooltip("Random ölçek aralığı (x=min, y=max çarpan). 1,1 = kapalı")]
    public Vector2 scaleJitter = Vector2.one;

    public bool SupportsCategory(PropCategory category)
    {
        if (categories == null) return false;
        for (int i = 0; i < categories.Length; i++)
            if (categories[i] == category) return true;
        return false;
    }
}
