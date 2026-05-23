// Assets/Scripts/Items/LootEntry.cs
using System;
using UnityEngine;

/// <summary>
/// NetworkedItemSpawner'in loot table'inda bir satir. Inspector'dan
/// duzenlenir: hangi prefab, hangi gorelinde olasilik (Weight) ve
/// oda basina kac kez (Min/Max) cikabilir.
///
/// - Prefab: BaseItem prefab'i; NetworkObject componenti SART. Yoksa
///   spawner runtime'da hata loglar ve atlar.
/// - Weight: Goreceli secilme agirligi. Diger entry'lerin Weight'lerine
///   gore normalize edilir; 0 veya negatif olursa entry asla secilmez.
/// - MinPerRoom / MaxPerRoom: Oda basina kac instance dusebilir.
///   Spawner her oda icin Random.Range(min, max+1) kullanarak hedef
///   sayiyi belirler. 0 / 0 = "bu prefab oda basina garantili degil,
///   sadece global agirlikla cikar" anlamina gelmez — 0/0 entry asla
///   spawn etmez. En azindan MaxPerRoom >= 1 vermek gerek.
/// </summary>
[Serializable]
public struct LootEntry
{
    [Tooltip("Spawn edilecek item prefab'i. NetworkObject componenti gerekli.")]
    public BaseItem Prefab;

    [Tooltip("Goreceli secilme agirligi (>0). Diger entry'lere oranla normalize edilir.")]
    [Min(0f)]
    public float Weight;

    [Tooltip("Oda basina minimum kopya sayisi (>=0).")]
    [Min(0)]
    public int MinPerRoom;

    [Tooltip("Oda basina maksimum kopya sayisi (>=Min).")]
    [Min(0)]
    public int MaxPerRoom;
}
