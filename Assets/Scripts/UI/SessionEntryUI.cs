using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the session browser list.
/// Populated by MainMenuUI.RenderSessionList.
/// </summary>
public class SessionEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text sessionNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button   joinButton;

    private System.Action<string> _onJoin;
    private string                _sessionId;

    /// <summary>
    /// Fills the row UI and wires the Join button.
    /// </summary>
    public void Init(ISessionInfo info, System.Action<string> onJoin)
    {
        _sessionId = info.Id;
        _onJoin    = onJoin;

        sessionNameText.text = info.Name;
        playerCountText.text = $"{info.MaxPlayers - info.AvailableSlots}/{info.MaxPlayers}";

        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnJoinClicked() => _onJoin?.Invoke(_sessionId);
}
