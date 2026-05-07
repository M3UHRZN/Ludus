using UnityEngine;

public interface IGrabbable
{
    void OnGrab(PlayerStateMachine grabber);
    void OnRelease();
    void Throw(Vector3 direction, float chargeRatio);
    float Weight { get; }
    bool IsHeld { get; }
}
