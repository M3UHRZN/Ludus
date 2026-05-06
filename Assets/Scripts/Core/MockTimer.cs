#if false // GameSessionManager timer'ı devraldı — bu script artık kullanılmıyor
using UnityEngine;

public class MockTimer : MonoBehaviour
{
    public float timeRemaining = 60f;

    void Update()
    {
        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
            // HUDController'ın duyabileceği şekilde olayı yayınla
            GameEventBus.Publish(new TimerEventTriggered(timeRemaining));
        }
    }
}
#endif
