using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerRowUI : MonoBehaviour
{
    public TMP_Text nameText;
    public Image background;

    [HideInInspector] public ulong clientId;

    private TeamLobbyUI lobbyUI;

    public void Setup(TeamLobbyUI ui, ulong id, string displayName)
    {
        lobbyUI = ui;
        clientId = id;

        if (nameText != null) nameText.text = displayName;

        // Hook click in code. The button is a child to the root. this script is attached to the root object
        var btn = GetComponentInChildren<Button>();

        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnRowButtonClicked);
        }
    }

    private void OnRowButtonClicked()
    {
        lobbyUI.SelectPlayer(clientId);
    }

    public void SetSelected(bool selected)
    {
        if (background == null) return;
        // simple highlight: brighter when selected
        background.color = selected ? new Color(1f, 1f, 0.6f, 1f) : Color.yellow;
    }
}