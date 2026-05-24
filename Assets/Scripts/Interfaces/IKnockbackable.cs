using UnityEngine;

/// <summary>
/// Yakin dovus darbesiyle geri itilebilen ve kisa sure sersemleyebilen nesneler.
/// Dusman AttackBehavior temas aninda bunu cagirir; oyuncu (PlayerStateMachine)
/// uygular. Enemy ve Player arasinda gevsek bagli kalmak icin interface kullanildi
/// (IDamageable / ISpectatable ile ayni desen).
/// </summary>
public interface IKnockbackable
{
    /// <summary>
    /// Geri itme + stun uygular. Server tarafinda tetiklenmeli (AI server-only kosar).
    /// </summary>
    /// <param name="sourcePosition">Darbeyi veren kaynagin konumu (itme yonu bundan hesaplanir)</param>
    /// <param name="force">Yatay itme hizi (m/s)</param>
    /// <param name="stunDuration">Hareketsiz kalma suresi (sn)</param>
    void ApplyKnockback(Vector3 sourcePosition, float force, float stunDuration);
}
