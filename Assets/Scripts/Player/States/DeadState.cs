using UnityEngine;
using Unity.Cinemachine;

public class DeadState : IPlayerState
{
    public void Enter(PlayerStateMachine machine)
    {
        // Oyuncu kamerasını kapat
        var look = machine.Look;
        if (look != null && look.CameraTarget != null)
        {
            var vcam = look.CameraTarget.GetComponent<CinemachineCamera>();
            if (vcam != null) vcam.enabled = false;
        }

        machine.SwitchActionMap("Spectator");

        // movement'i KAPATMIYORUZ — PlayerMovement Update'i icinde ApplyGravity var
        // ve action map "Spectator"a gectigi icin Gameplay/Move girisleri sifir okur,
        // boylece yatay hareket yok ama yercekimi cesedi zemine yaslar. Aksi takdirde
        // CharacterController son olduren framedeki konumda donar (havada asili ceset).
        machine.SetComponentsEnabled(
            movement: true,
            look: false,
            interaction: false,
            inventory: false,
            spectator: true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Tick(PlayerStateMachine machine) { }
    public void Exit(PlayerStateMachine machine) { }
}