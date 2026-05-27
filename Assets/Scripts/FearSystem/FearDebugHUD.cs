using UnityEngine;

/// <summary>
/// GECICI test yardimcisi — aktif FearSystem'in korku seviyesini ekrana cizer.
/// "Fear calisiyor mu / Enemy Layer dogru mu" diye hizlica gormek icin. Sahnedeki
/// herhangi bir objeye (orn. TestController) eklenir. Teslim/sunum oncesi silinir.
/// </summary>
public class FearDebugHUD : MonoBehaviour
{
    private FearSystem _fear;

    private void OnGUI()
    {
        // Aktif (owner'da acik) FearSystem'i bul ve cache'le.
        if (_fear == null || !_fear.isActiveAndEnabled)
            _fear = FindFirstObjectByType<FearSystem>();

        if (_fear == null)
        {
            GUI.Label(new Rect(12, 10, 520, 30), "FearDebugHUD: aktif FearSystem bulunamadi (component kapali olabilir).");
            return;
        }

        float f = _fear.FearLevel;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(12, 8, 520, 30),
            $"FEAR: {f:F0} / 100{(_fear.IsInPanic ? "    [PANIK!]" : "")}", style);

        // Bar arka plani
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(12, 40, 304, 20), Texture2D.whiteTexture);
        // Bar dolulugu (yesil -> kirmizi)
        GUI.color = Color.Lerp(Color.green, Color.red, f / 100f);
        GUI.DrawTexture(new Rect(14, 42, 300f * (f / 100f), 16), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
