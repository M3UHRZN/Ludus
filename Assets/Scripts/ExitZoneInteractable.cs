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
    private readonly List<CorpseItem> _corpsesInZone = new();
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
        // Player check
        var machine = other.GetComponent<PlayerStateMachine>();
        if (machine == null) machine = other.GetComponentInParent<PlayerStateMachine>();
        if (machine != null)
        {
            if (!_playersInZone.Contains(machine))
            {
                _playersInZone.Add(machine);
                Debug.Log($"[ExitZone] Oyuncu girdi: {machine.gameObject.name} | Toplam icerideki: {_playersInZone.Count}");
            }

            if (machine.IsOwner)
            {
                _localPlayerInZone = machine;
                Debug.Log("[ExitZone] Yerel oyuncu alana girdi!");
            }
            return;
        }

        // Corpse check (cesedi tasiyan bir oyuncu zone'a girdiginde sayilir)
        var corpse = other.GetComponent<CorpseItem>();
        if (corpse == null) corpse = other.GetComponentInParent<CorpseItem>();
        if (corpse != null && !_corpsesInZone.Contains(corpse))
        {
            _corpsesInZone.Add(corpse);
            Debug.Log($"[ExitZone] Ceset girdi: owner={corpse.OwnerName} (clientId={corpse.CorpseOwnerClientId}) | Toplam ceset: {_corpsesInZone.Count}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Player exit
        var machine = other.GetComponent<PlayerStateMachine>();
        if (machine == null) machine = other.GetComponentInParent<PlayerStateMachine>();
        if (machine != null)
        {
            if (_playersInZone.Contains(machine))
            {
                _playersInZone.Remove(machine);
                Debug.Log($"[ExitZone] Oyuncu cikti: {machine.gameObject.name} | Toplam icerideki: {_playersInZone.Count}");
            }

            if (_localPlayerInZone == machine)
            {
                _localPlayerInZone = null;
                Debug.Log("[ExitZone] Yerel oyuncu alandan cikti!");
            }
            return;
        }

        // Corpse exit
        var corpse = other.GetComponent<CorpseItem>();
        if (corpse == null) corpse = other.GetComponentInParent<CorpseItem>();
        if (corpse != null && _corpsesInZone.Contains(corpse))
        {
            _corpsesInZone.Remove(corpse);
            Debug.Log($"[ExitZone] Ceset cikti: owner={corpse.OwnerName} | Kalan ceset: {_corpsesInZone.Count}");
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
    /// Sadece SERVER uzerinde calistirilir. Kacis alanindaki canli oyunculari ve
    /// alana getirilmis cesetleri kurtarir; alana ulasamayanlari geride birakir.
    ///
    /// Tahliye basari kriterleri (oyuncu bazinda):
    ///   - Canli oyuncu + zone icinde            -> Kurtarildi, elindeki esya extract
    ///   - Olu oyuncu + cesedi zone icinde       -> Kurtarildi (baska bir takim arkadasi
    ///                                              cesedi getirdi)
    ///   - Canli oyuncu + zone disinda           -> Terk edildi, ceza uygula
    ///   - Olu oyuncu + cesedi zone disinda      -> Terk edildi, ceza uygula
    ///
    /// Boylece "herkesin canli bedeni veya cesedi tahliye edildi" senaryosunda
    /// hicbir ceza dusmez (GDD §6.4: max(0.25, playerCount/100) * grossCredits her ceset).
    /// </summary>
    public void PerformExtractionOnServer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        Debug.Log("[ExitZone] Sunucu tahliye islemlerini baslatiyor...");

        int rescuedAlive = 0;
        int rescuedCorpses = 0;
        int abandoned = 0;

        var allClients = NetworkManager.Singleton.ConnectedClients;
        foreach (var kvp in allClients)
        {
            var client = kvp.Value;
            if (client.PlayerObject == null) continue;

            var machine = client.PlayerObject.GetComponent<PlayerStateMachine>();
            if (machine == null) continue;

            bool isAlive = machine.IsAlive;
            bool playerInZone = _playersInZone.Contains(machine);

            if (isAlive && playerInZone)
            {
                // Canli oyuncu zone icinde — basariyla tahliye + elindeki esya kurtarilir
                Debug.Log($"[ExitZone] Canli oyuncu tahliye oldu: {machine.gameObject.name}");
                rescuedAlive++;

                // Oyuncunun elinde tasidigi esyayi kontrol et ve kurtar
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
                        credits = Mathf.RoundToInt(bItem.CreditValue); // Deger item'in kendi ItemDefinition'indan.
                    }
                    else
                    {
                        var item = heldItem.GetComponent<IItem>();
                        if (item != null) credits = Mathf.RoundToInt(item.CreditValue);
                    }
                    // --------------------------

                    Debug.Log($"[ExitZone] Elindeki esya kurtarildi: {heldItem.name} | Kredi: {credits}");
                    // Sahte 0 yerine gercek ItemId yolluyoruz (kayit dogru olsun)
                    ExtractionManager.Instance?.RegisterExtractedItem(heldItemId, credits);
                    Destroy(heldItem.gameObject);
                }
            }
            else if (!isAlive)
            {
                // Olu oyuncu — cesedi zone'da mi?
                var corpse = FindCorpseInZoneForClient(kvp.Key);
                if (corpse != null)
                {
                    Debug.Log($"[ExitZone] Ceset kurtarildi: {corpse.OwnerName} (clientId={kvp.Key})");
                    rescuedCorpses++;
                    // Ceset kurtarildi sayilir, ceza yok. Despawn ceset (lobby'ye gitmeyecek).
                    var corpseNo = corpse.GetComponent<NetworkObject>();
                    if (corpseNo != null && corpseNo.IsSpawned)
                        corpseNo.Despawn(true);
                }
                else
                {
                    Debug.LogWarning($"[ExitZone] Olu oyuncunun cesedi getirilmedi, terk edildi: clientId={kvp.Key}");
                    abandoned++;
                    GameSessionManager.Instance?.RegisterAbandonedCorpse();
                }
            }
            else
            {
                // Canli ama zone disinda — geride birakildi (canli abandonment)
                Debug.LogWarning($"[ExitZone] Canli oyuncu zone disinda geride birakildi: {machine.gameObject.name}");
                abandoned++;
                GameSessionManager.Instance?.RegisterAbandonedCorpse();
            }
        }

        Debug.Log($"[ExitZone] Tahliye ozeti: {rescuedAlive} canli + {rescuedCorpses} ceset kurtarildi, {abandoned} terk edildi.");

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

    /// <summary>
    /// _corpsesInZone setinde verilen clientId'ye ait CorpseItem var mi?
    /// </summary>
    private CorpseItem FindCorpseInZoneForClient(ulong clientId)
    {
        for (int i = 0; i < _corpsesInZone.Count; i++)
        {
            var corpse = _corpsesInZone[i];
            if (corpse == null) continue;
            if (corpse.CorpseOwnerClientId == clientId) return corpse;
        }
        return null;
    }
}