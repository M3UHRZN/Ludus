// Gecici slow alabilen nesne. Server'da tetiklenir, owner RPC ile yerel hareket scriptine iletilir.
public interface ISlowable
{
    // multiplier 0..1, duration saniye. Yeni cagri esitse veya daha agirsa onceliklenir.
    void ApplySlow(float multiplier, float duration);
}
