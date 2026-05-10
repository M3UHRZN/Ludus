using UnityEngine;
using UnityEngine.InputSystem;

// Attach to Canvas in LobbyScene.
// Shows PlayerListPanel while the Table (Tab) action is held.
// Reads via PlayerInput.actions["Table"] — works regardless of which action map Table is in.
public class LobbyTableUI : MonoBehaviour
{
    [SerializeField] private GameObject playerListPanel;

    private PlayerInput _playerInput;
    private InputAction _tableAction;

    private void Awake()
    {
        _playerInput = FindFirstObjectByType<PlayerInput>();
    }

    private void OnEnable()
    {
        if (_playerInput == null) return;
        _tableAction = _playerInput.actions["Table"];
        if (_tableAction == null) return;
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
