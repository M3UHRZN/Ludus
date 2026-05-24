using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Standalone interact script for the Market test scene.
/// Attach to TestPlayer. Raycasts on E key and calls IInteractable directly.
/// Does not require NetworkBehaviour or PlayerStateMachine.
/// </summary>
public class MarketTestInteract : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Max interact distance in metres")]
    public float interactDistance = 6f;

    [Header("Market")]
    [Tooltip("Drag MarketUIController here")]
    public MarketUIController marketUI;
    [Tooltip("Drag MarketTransactionService here")]
    public MarketTransactionService transactionService;

    [Header("Debug")]
    public bool showGizmo = true;

    private Camera     _cam;
    private TestPlayer _testPlayer;

    private void Start()
    {
        _cam        = Camera.main;
        _testPlayer = GetComponent<TestPlayer>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.eKey.wasPressedThisFrame) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            Debug.Log("[MarketTestInteract] Nothing in range.");
            return;
        }

        // Check for MarketTerminal directly
        MarketTerminal terminal = hit.collider.GetComponentInParent<MarketTerminal>();
        if (terminal != null)
        {
            Debug.Log("[MarketTestInteract] Market terminal hit — opening UI.");
            if (marketUI != null && transactionService != null)
                marketUI.Open(transactionService, null, _testPlayer);
            else
                Debug.LogWarning("[MarketTestInteract] marketUI or transactionService not assigned.");

            return;
        }

        Debug.Log($"[MarketTestInteract] Hit '{hit.collider.name}' but no MarketTerminal found.");
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo || Camera.main == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * interactDistance);
    }
}
