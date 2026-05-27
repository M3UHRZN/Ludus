using UnityEngine;
using Unity.Cinemachine;

public class DeadState : IPlayerState
{
    public void Enter(PlayerStateMachine machine)
    {
        // === SES TETİKLEYİCİ ===
        // machine (yani karakterin kök objesi) içindeki modelleri tarayıp ses kodumuzu bulur
        var audioController = machine.GetComponentInChildren<PlayerAudioController>();
        if (audioController != null)
        {
            audioController.PlayDeathSound();
        }
        // =========================================================

        // Oyuncu kamerasını kapat
        var look = machine.Look;
        if (look != null && look.CameraTarget != null)
        {
            var vcam = look.CameraTarget.GetComponent<CinemachineCamera>();
            if (vcam != null) vcam.enabled = false;
        }

        machine.SwitchActionMap("Spectator");
        machine.SetComponentsEnabled(
            movement: false,
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