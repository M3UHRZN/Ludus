/// <summary>
/// Hareket hizi disaridan bir carpanla degistirilebilen oyuncu/varlik.
/// FearSystem (korku hiz cezasi) ve benzeri sistemler bu interface uzerinden
/// konusur; boylece somut PlayerMovement / TestPlayer sinifina bagimli kalmaz.
/// </summary>
public interface ISpeedModifiable
{
    /// <summary>1 = normal hiz, 0 = durur. Aradaki degerler oransal yavaslatir.</summary>
    void SetSpeedMultiplier(float multiplier);
}
