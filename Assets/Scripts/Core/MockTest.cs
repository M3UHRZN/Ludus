//using UnityEngine;
//using UnityEngine.InputSystem;

//public class MockTest : MonoBehaviour
//{
//    public float timeRemaining = 60f;

//    void Update()
//    {
//        var kb = Keyboard.current;
//        if (kb == null) return;

//        // TimerEventTriggered olay�n� her frame'de yay�nlayarak HUDController'�n g�ncel zaman� almas�n� sa�l�yoruz
//        if (timeRemaining > 0)
//        {
//            timeRemaining -= Time.deltaTime;
//            // HUDController'�n duyabilece�i �ekilde olay� yay�nla
//            GameEventBus.Publish(new TimerEventTriggered(timeRemaining));
//        }


//        // --- 1. ADIM: OYUNCU SAYISINI AYARLAMA ---
//        // Klavyeden '4' tu�una basarsak 4 ki�ilik oyun ba�las�n
//        if (Input.GetKeyDown(KeyCode.Alpha4))
//        {
//            GameSessionManager.Instance.StartSession(4);
//            Debug.Log("Oyun 4 kişi başladı!");
//        }
//        if (kb.digit6Key.wasPressedThisFrame && GameSessionManager.Instance != null)
//        {
//            GameSessionManager.Instance.StartSession(6);
//            Debug.Log("Oyun 6 kişi başladı!");
//        }

//        // --- Ölüm Testleri ---
//        if (kb.kKey.wasPressedThisFrame)
//        {
//            GameEventBus.Publish(new PlayerDiedEvent(0, Vector3.zero));
//            Debug.Log("1. Oyuncu öldü!");
//        }
//        if (kb.lKey.wasPressedThisFrame)
//        {
//            GameEventBus.Publish(new PlayerDiedEvent(1, Vector3.zero));
//            Debug.Log("2. Oyuncu öldü!");
//        }
//        if (kb.mKey.wasPressedThisFrame)
//        {
//            GameEventBus.Publish(new PlayerDiedEvent(3, Vector3.zero));
//            Debug.Log("4. Oyuncu öldü!");
//        }

//        // --- Ağırlık Barı Testleri ---
//        if (kb.zKey.wasPressedThisFrame)
//        {
//            GameEventBus.Publish(new ItemPickedUpEvent("Küçük Hurda", 1, 10f));
//            Debug.Log("Mock: Küçük eşya alındı (+1 Ağırlık)");
//        }
//        if (kb.xKey.wasPressedThisFrame)
//        {
//            GameEventBus.Publish(new ItemPickedUpEvent("Orta Boy Motor", 3, 40f));
//            Debug.Log("Mock: Orta eşya alındı (+3 Ağırlık)");
//        }

//        // Klavyeden 'C' tu�una basarsak B�y�k (Large) e�ya alal�m (A��rl�k: 6)
//        if (Input.GetKeyDown(KeyCode.C))
//        {
//            GameEventBus.Publish(new ItemPickedUpEvent("A��r �ekirdek", 6, 100f));
//            Debug.Log("Mock: B�y�k e�ya al�nd� (+6 A��rl�k)");
//        }






//        // --- SPRINT 2: EXTRACTION EKRANI TEST� ---
//        // 'E' tu�una basarsak Ba�ar�l� Event f�rlat
//        if (Input.GetKeyDown(KeyCode.E))
//        {
//            GameEventBus.Publish(new LevelEndedEvent(true, 450, 112, 0.8f));
//            Debug.Log("Mock: Ba�ar�l� tur event'i f�rlat�ld�!");
//        }

//        // 'R' tu�una basarsak Ba�ar�s�z Event f�rlat
//        if (Input.GetKeyDown(KeyCode.R))
//        {
//            GameEventBus.Publish(new LevelEndedEvent(false, 120, 0, 0.3f));
//            Debug.Log("Mock: Ba�ar�s�z tur event'i f�rlat�ld�!");
//        }







//        // Klavyeden '1' tuşuna BASILI TUTTUĞUMUZ SÜRECE resimdeki arayüz çıksın
//        if (Input.GetKeyDown(KeyCode.Alpha1))
//        {
//            FindObjectOfType<InteractionUIController>().ShowInteraction("Pinwheel", 10f);
//        }

//        // '1' tuşundan ELİMİZİ ÇEKİNCE arayüz kapansın
//        if (Input.GetKeyUp(KeyCode.Alpha1))
//        {
//            FindObjectOfType<InteractionUIController>().HideInteraction();
//        }
//    }
//}
