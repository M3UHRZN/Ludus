using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PropPalette", menuName = "VoidHaul/Prop Palette")]
public class PropPaletteSO : ScriptableObject
{
    [Tooltip("Yerleştirilebilecek dekor modellerinin listesi")]
    public List<DecorPropEntry> entries = new List<DecorPropEntry>();
}
