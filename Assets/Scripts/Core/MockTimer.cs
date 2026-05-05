using UnityEngine;

public class MockTimer : MonoBehaviour
{
    public float timeRemaining = 60f;

    void Update()
    {
        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
            // HUDController'ýn duyabileceði þekilde olayý yayýnla
            GameEventBus.Publish(new TimerEventTriggered(timeRemaining));
        }
    }
}