using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitZoneInteractable : MonoBehaviour, IInteractable
{
    [Header("Scene")]
    public string lobbySceneName = "LobbyScene";

    [Header("Prompts")]
    public string readyPrompt    = "Return to Lobby [E]";
    public string notReadyPrompt = "Drop an item first!";

    private PlayerStateMachine _playerInZone;

    public string InteractPrompt =>
        (ExtractionManager.Instance != null && ExtractionManager.Instance.HasExtractedItems)
            ? readyPrompt
            : notReadyPrompt;

    public bool CanInteract(PlayerStateMachine machine)
    {
        if (ExtractionManager.Instance == null) return false;
        return ExtractionManager.Instance.HasExtractedItems;
    }

    public void Interact(PlayerStateMachine machine)
    {
        Debug.Log("[ExitZone] Interact çağrıldı!");
        if (!CanInteract(machine)) return;

        ShowResultsLocalUI();

        ExtractionManager.Instance?.ResetForNewRun();

        NetworkManager.Singleton.SceneManager.LoadScene(
            lobbySceneName,
            LoadSceneMode.Single);
    }

    private void OnTriggerEnter(Collider other)
    {
        var machine = other.GetComponent<PlayerStateMachine>();
        if (machine == null) machine = other.GetComponentInParent<PlayerStateMachine>();
        if (machine == null || !machine.IsOwner) return;

        _playerInZone = machine;
        Debug.Log("[ExitZone] Oyuncu girdi!");
    }

    private void OnTriggerExit(Collider other)
    {
        var machine = other.GetComponent<PlayerStateMachine>();
        if (machine == null) machine = other.GetComponentInParent<PlayerStateMachine>();
        if (machine == null) return;

        if (_playerInZone == machine)
            _playerInZone = null;
    }

    private void Update()
    {
        if (_playerInZone == null) return;
        if (!_playerInZone.IsOwner) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("[ExitZone] E basıldı!");
            Interact(_playerInZone);
        }
    }

    private void ShowResultsLocalUI()
    {
        var manager = ExtractionManager.Instance;
        if (manager == null) return;

        GameEventBus.Publish(new LevelEndedEvent(
            isSuccess:        true,
            collectedCredits: manager.TotalCredits.Value,
            penaltyAmount:    0,
            quotaFillAmount:  Mathf.Clamp01(manager.ExtractedItemCount.Value / 10f)
        ));
    }
}