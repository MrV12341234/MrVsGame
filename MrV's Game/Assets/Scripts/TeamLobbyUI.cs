using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class TeamLobbyUI : MonoBehaviour
{
    [Header("Parents where names are spawned")]
    public Transform blueListParent;
    public Transform redListParent;

    [Header("Row prefab (TMP_Text inside)")]
    public GameObject nameRowPrefab;

    [Header("Buttons")]
    public GameObject startGameButtonObject;        // Start button (host only)
    public GameObject switchTeamButtonObject;       // Switch team (everyone)
    public GameObject moveSelectedPlayerButtonObject; // <-- ADD THIS (host only)
    public GameObject quitButtonObject;

    private RoomManagerLan rm;
    private ulong? selectedClientId = null;

    private void Start()
    {
        rm = RoomManagerLan.Instance;
        RefreshHostOnlyButtons();
    }

    public void ShowLobby()
    {
        gameObject.SetActive(true);
        rm = RoomManagerLan.Instance;

        RefreshHostOnlyButtons();   // <-- important when lobby opens
        RebuildLists();
    }

    public void HideLobby()
    {
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        rm = RoomManagerLan.Instance;
        if (rm != null)
            rm.LobbyPlayers.OnListChanged += OnLobbyListChanged;

        RefreshHostOnlyButtons();   // <-- important if enabled later
    }

    private void OnDisable()
    {
        if (rm != null)
            rm.LobbyPlayers.OnListChanged -= OnLobbyListChanged;
    }

    private void RefreshHostOnlyButtons()
    {
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        // Start button: host only
        if (startGameButtonObject != null)
            startGameButtonObject.SetActive(isHost);

        // Move Selected Player button: host only
        if (moveSelectedPlayerButtonObject != null)
            moveSelectedPlayerButtonObject.SetActive(isHost);

        // Optional: if not host, clear selection so nothing looks "selectable"
        if (!isHost)
            selectedClientId = null;
    }

    private void OnLobbyListChanged(Unity.Netcode.NetworkListEvent<RoomManagerLan.LobbyPlayerState> changeEvent)
    {
        RebuildLists();
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void RebuildLists()
    {
        if (rm == null || nameRowPrefab == null) return;

        ClearChildren(blueListParent);
        ClearChildren(redListParent);

        for (int i = 0; i < rm.LobbyPlayers.Count; i++)
        {
            var p = rm.LobbyPlayers[i];
            Transform parent = (p.team == RoomManagerLan.TeamId.Blue) ? blueListParent : redListParent;

            GameObject rowObj = Instantiate(nameRowPrefab, parent);

            LobbyPlayerRowUI row = rowObj.GetComponent<LobbyPlayerRowUI>();
            if (row != null)
            {
                row.Setup(this, p.clientId, p.name.ToString());
                row.SetSelected(selectedClientId.HasValue && selectedClientId.Value == p.clientId);
            }
            else
            {
                TMP_Text text = rowObj.GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = p.name.ToString();
            }
        }
    }

    // Hook this to your Switch Team button OnClick
    public void OnClickSwitchTeam()
    {
        if (rm != null) rm.SwitchTeamServerRpc();
    }

    public void OnClickMoveSelectedPlayer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;
        if (rm == null) return;
        if (!selectedClientId.HasValue) return;

        rm.MoveSelectedPlayerServerRpc(selectedClientId.Value);
    }

    // Hook this to Start Game button OnClick
    public void OnClickStartGame()
    {
        if (rm != null) rm.StartMatchServerRpc();
    }

    public void SelectPlayer(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            return;

        selectedClientId = clientId;
        RebuildLists();
    }

    // Hook this to your Quit button OnClick
    public void OnClickQuit()
    {
        gameObject.SetActive(false);

        PlayerPrefs.SetInt("LAN_IsHost", 0);

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(0);
    }
}
