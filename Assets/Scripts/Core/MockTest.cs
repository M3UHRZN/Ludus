using UnityEngine;
using UnityEngine.InputSystem;

public class MockTest : MonoBehaviour
{
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // --- Session Başlatma ---
        if (kb.digit4Key.wasPressedThisFrame && GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.StartSession(4);
            Debug.Log("Oyun 4 kişi başladı!");
        }
        if (kb.digit6Key.wasPressedThisFrame && GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.StartSession(6);
            Debug.Log("Oyun 6 kişi başladı!");
        }

        // --- Ölüm Testleri ---
        if (kb.kKey.wasPressedThisFrame)
        {
            GameEventBus.Publish(new PlayerDiedEvent(0, Vector3.zero));
            Debug.Log("1. Oyuncu öldü!");
        }
        if (kb.lKey.wasPressedThisFrame)
        {
            GameEventBus.Publish(new PlayerDiedEvent(1, Vector3.zero));
            Debug.Log("2. Oyuncu öldü!");
        }
        if (kb.mKey.wasPressedThisFrame)
        {
            GameEventBus.Publish(new PlayerDiedEvent(3, Vector3.zero));
            Debug.Log("4. Oyuncu öldü!");
        }

        // --- Ağırlık Barı Testleri ---
        if (kb.zKey.wasPressedThisFrame)
        {
            GameEventBus.Publish(new ItemPickedUpEvent("Küçük Hurda", 1, 10f));
            Debug.Log("Mock: Küçük eşya alındı (+1 Ağırlık)");
        }
        if (kb.xKey.wasPressedThisFrame)
        {
            GameEventBus.Publish(new ItemPickedUpEvent("Orta Boy Motor", 3, 40f));
            Debug.Log("Mock: Orta eşya alındı (+3 Ağırlık)");
        }
        if (kb.cKey.wasPressedThisFrame)
        {
            GameEventBus.Publish(new ItemPickedUpEvent("Ağır Çekirdek", 6, 100f));
            Debug.Log("Mock: Büyük eşya alındı (+6 Ağırlık)");
        }
    }
}
