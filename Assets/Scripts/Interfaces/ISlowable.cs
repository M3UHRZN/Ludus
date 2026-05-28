/// <summary>
/// Dis kaynak (ornek: dusman mermisi) tarafindan gecici olarak yavaslatilabilen
/// nesneler. Server tarafinda tetiklenmeli; uygulayici taraf owner-RPC ile
/// yerel hareket scriptine slow'u iletir.
///
/// IKnockbackable / ISpeedModifiable ile ayni desen — gevsek bagli, multiplayer-uyumlu.
/// </summary>
public interface ISlowable
{
    /// <summary>
    /// Hedef hiz carpani. 1f = normal, 0.5f = yariya yavasla. Mevcut slow varsa
    /// duration max-extends edilir (yeni cagri eski sureyi uzatabilir), multiplier
    /// daha agir ise (daha kucuk) onceliklenir.
    /// </summary>
    /// <param name="multiplier">Hedef hiz carpani [0..1]; mantikli aralik 0.4 - 0.9</param>
    /// <param name="duration">Saniye cinsinden sure</param>
    void ApplySlow(float multiplier, float duration);
}
