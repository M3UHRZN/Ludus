using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class ExitZoneInteractable : MonoBehaviour, IInteractable
{
    public static ExitZoneInteractable Instance { get; private set; }

    [Header("Prompts")]
    public string readyPrompt    = "Return to Lobby [E]";
    public string notReadyPrompt = "Drop an item first!";

    private readonly List<PlayerStateMachine> _playersInZone = new();
    private PlayerStateMachine _localPlayerInZone;

    public string InteractPrompt =>
        (ExtractionManager.Instance != null && ExtractionManager.Instance.HasExtractedItems)
            ? readyPrompt
            : notReadyPrompt;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool CanInteract(PlayerStateMachine machine)
    {
        if (machine == null || !_playersInZone.Contains(machine)) return false;
        if (ExtractionManager.Instance == null) return false;
        return ExtractionManager.Instance.HasExtractedItems;
    }

    public void Interact(PlayerStateMachine machine)
    {
        Debug.Log("[ExitZone] Interact çağrıldı!");
        if (!CanInteract(machine)) return;
        ExtractionManager.Instance?.RequestTeamExtractionRpc();
    }

    private void OnTriggerEnter(Collider other)
    {
        var machine = other.GetComponent<PlayerStateMachine>();
        if (machine == null) machine = other.GetComponentInParent<PlayerStateMachine>();
        if (machine == null) return;

        if (!_playersInZone.Contains(machine))
        {
            _playersInZone.Add(machine);
            Debug.Log($"[ExitZone] Oyuncu girdi: {machine.gameObject.name} | Toplam içerideki: {_playersInZone.Count}");
        }

        if (machine.IsOwner)
        {
            _localPlayerInZone = machine;
            Debug.Log("[ExitZone] Yerel oyuncu alana girdi!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var machine = other.GetComponent<PlayerStateMachine>();
        if (machine == null) machine = other.GetComponentInParent<PlayerStateMachine>();
        if (machine == null) return;

        if (_playersInZone.Contains(machine))
        {
            _playersInZone.Remove(machine);
            Debug.Log($"[ExitZone] Oyuncu çıktı: {machine.gameObject.name} | Toplam içerideki: {_playersInZone.Count}");
        }

        if (_localPlayerInZone == machine)
        {
            _localPlayerInZone = null;
            Debug.Log("[ExitZone] Yerel oyuncu alandan çıktı!");
        }
    }

    private void Update()
    {
        if (_localPlayerInZone == null) return;
        if (!_localPlayerInZone.IsOwner) return;

        var playerInput = _localPlayerInZone.PlayerInput;
        if (playerInput != null)
        {
            var interactAction = playerInput.actions.FindAction("Interact") ?? playerInput.actions["Gameplay/Interact"];
            if (interactAction != null && interactAction.WasPressedThisFrame())
            {
                Debug.Log("[ExitZone] Yerel oyuncu Interact aksiyonu tetikledi!");
                Interact(_localPlayerInZone);
            }
        }
    }

    /// <summary>
    /// Sadece SERVER üzerinde çalıştırılır. Kaçış alanındaki oyuncuları kurtarır,
    /// dışarıdakileri cezalandırır, eşyaları toplar ve lobiyi yükler.
    /// </summary>
    public void PerformExtractionOnServer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        Debug.Log("[ExitZone] Sunucu tahliye işlemlerini başlatıyor...");

        var allClients = NetworkManager.Singleton.ConnectedClients;
        foreach (var kvp in allClients)
        {
            var client = kvp.Value;
            if (client.PlayerObject == null) continue;

            var machine = client.PlayerObject.GetComponent<PlayerStateMachine>();
            if (machine == null) continue;

            // Oyuncu kaçış alanının içinde mi?
            if (_playersInZone.Contains(machine))
            {
                Debug.Log($"[ExitZone] Oyuncu başarıyla tahliye oldu: {machine.gameObject.name}");

                //// Oyuncunun elinde taşıdığı eşyayı kontrol et ve kurtar!
                //var interaction = client.PlayerObject.GetComponent<PlayerInteraction>();
                //if (interaction != null && interaction.HeldObject != null)
                //{
                //    var heldItem = interaction.HeldObject;
                //    int credits = 10;
                //    var item = heldItem.GetComponent<IItem>();
                //    if (item != null)
                //        credits = Mathf.RoundToInt(item.CreditValue);

                //    Debug.Log($"[ExitZone] Oyuncunun elindeki eşya kurtarılıyor: {heldItem.name} | Kredi: {credits}");
                //    ExtractionManager.Instance?.RegisterExtractedItem(0, credits);

                //    // Eşyayı yok et
                //    Destroy(heldItem.gameObject);

                // Oyuncunun elinde taşıdığı eşyayı kontrol et ve kurtar!
                var interaction = client.PlayerObject.GetComponent<PlayerInteraction>();
                if (interaction != null && interaction.HeldObject != null)
                {
                    var heldItem = interaction.HeldObject;
                    
                    // --- YENİ ZIRHLI SİSTEM ---
                    int credits = 0;
                    ushort heldItemId = 0;

                    if (heldItem.TryGetComponent<BaseItem>(out var bItem))
                    {
                        heldItemId = bItem.ItemId;
                        if (ItemDatabase.Instance != null)
                        {
                            var data = ItemDatabase.Instance.AllItems.Find(x => x.ItemId == heldItemId);
                            if (data != null) credits = (int)data.ItemPrice;
                        }
                    }
                    else
                    {
                        var item = heldItem.GetComponent<IItem>();
                        if (item != null) credits = Mathf.RoundToInt(item.CreditValue);
                    }
                    // --------------------------

                    Debug.Log($"[ExitZone] Oyuncunun elindeki eşya kurtarılıyor: {heldItem.name} | Kredi: {credits}");
                    
                    // Sahte 0 ve 10 yerine GERÇEK değerleri yolluyoruz!
                    ExtractionManager.Instance?.RegisterExtractedItem(heldItemId, credits);

                    // Eşyayı yok et
                    Destroy(heldItem.gameObject);
                }
            }
            else
            {
                // Dışarıdaki oyuncuyu geride bırak / ceset cezası uygula!
                Debug.LogWarning($"[ExitZone] Oyuncu geride bırakıldı: {machine.gameObject.name}");
                if (GameSessionManager.Instance != null)
                {
                    GameSessionManager.Instance.RegisterAbandonedCorpse();
                }
            }
        }

        // Cezalı oturum sonlandırmasını çalıştır
        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.EndSessionWithPenalty();
        }

        // Lobi sahnesini yükle
        string lobbyScene = "LobbyScene";
        if (ExtractionManager.Instance != null)
        {
            lobbyScene = ExtractionManager.Instance.LobbySceneName;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(lobbyScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}