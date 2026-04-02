using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Header("Host Only Team Lock")]
    public GameObject lockTeamsToggleObject;        // host only toggle object
    public Toggle lockTeamsToggle;                  // Toggle component on that object

    private RoomManagerLan rm;
    private ulong? selectedClientId = null;
    private bool updatingToggleVisual = false;

    private void Start()
    {
        rm = RoomManagerLan.Instance;
        RefreshHostOnlyButtons();
        RefreshTeamLockVisual();
    }

    public void ShowLobby()
    {
        gameObject.SetActive(true);
        rm = RoomManagerLan.Instance;

        RefreshHostOnlyButtons();   // <-- important when lobby opens
        RefreshTeamLockVisual();
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
        {
            rm.LobbyPlayers.OnListChanged += OnLobbyListChanged;
            rm.OnTeamsLockedChanged += OnTeamsLockedChanged;
        }

        RefreshHostOnlyButtons();   // <-- important if enabled later
        RefreshTeamLockVisual();
    }

    private void OnDisable()
    {
        if (rm != null)
        {
            rm.LobbyPlayers.OnListChanged -= OnLobbyListChanged;
            rm.OnTeamsLockedChanged -= OnTeamsLockedChanged;
        }
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

        // Lock Teams toggle: host only
        if (lockTeamsToggleObject != null)
            lockTeamsToggleObject.SetActive(isHost);

        // Optional: if not host, clear selection so nothing looks "selectable"
        if (!isHost)
            selectedClientId = null;
    }

    private void RefreshTeamLockVisual()
    {
        if (rm == null) return;

        bool teamsLocked = rm.AreTeamsLocked;

        // Hide the Switch Team button for everyone when teams are locked
        if (switchTeamButtonObject != null)
            switchTeamButtonObject.SetActive(!teamsLocked);

        // Keep the host toggle visually synced to the real networked value
        if (lockTeamsToggle != null)
        {
            updatingToggleVisual = true;
            lockTeamsToggle.isOn = teamsLocked;
            updatingToggleVisual = false;
        }
    }

    private void OnTeamsLockedChanged(bool isLocked)
    {
        RefreshTeamLockVisual();
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
        if (rm == null) return;
        if (rm.AreTeamsLocked) return;

        rm.SwitchTeamServerRpc();
    }

    // Hook this to your Lock Teams toggle OnValueChanged(bool)
    public void OnLockTeamsToggleChanged(bool isOn)
    {
        if (updatingToggleVisual) return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            return;

        if (rm == null) return;

        rm.SetTeamsLockedServerRpc(isOn);
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