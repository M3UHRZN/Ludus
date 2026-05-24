using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Attach to Canvas in LobbyScene.
// Shows PlayerListPanel while the Table (Tab) action is held.
public class LobbyTableUI : MonoBehaviour
{
    [SerializeField] private GameObject playerListPanel;

    private InputAction _tableAction;

    // Player spawns with a network delay; poll until PlayerInput is available.
    private IEnumerator Start()
    {
        PlayerInput playerInput = null;
        while (playerInput == null)
        {
            playerInput = FindFirstObjectByType<PlayerInput>();
            yield return null;
        }

        _tableAction = playerInput.actions["Table"];
        if (_tableAction == null) yield break;
        _tableAction.started  += OnTableStarted;
        _tableAction.canceled += OnTableCanceled;
    }

    private void OnDisable()
    {
        if (_tableAction == null) return;
        _tableAction.started  -= OnTableStarted;
        _tableAction.canceled -= OnTableCanceled;
    }

    private void OnTableStarted(InputAction.CallbackContext _)  => playerListPanel?.SetActive(true);
    private void OnTableCanceled(InputAction.CallbackContext _) => playerListPanel?.SetActive(false);
}
