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
    public GameObject startGameButtonObject; // assign your Start button GameObject
    public GameObject switchTeamButtonObject; // assign your Switch button GameObject
    public GameObject quitButtonObject;

    private RoomManagerLan rm;
    private ulong? selectedClientId = null;


    private void Start()
    {
        rm = RoomManagerLan.Instance;

        // Start button: host only
        if (startGameButtonObject != null)
            startGameButtonObject.SetActive(NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost);
    }

    public void ShowLobby()
    {
        gameObject.SetActive(true);
        rm = RoomManagerLan.Instance;
        RebuildLists();
    }

    public void HideLobby()
    {
        gameObject.SetActive(false);
    }


    // NOTE: Because removing anonymous delegates is annoying,
    // easiest beginner way: don't unsubscribe, or keep a named method.
    // We'll do named method below for correctness:

    private void OnEnable()
    {
        rm = RoomManagerLan.Instance;
        if (rm != null)
        {
            rm.LobbyPlayers.OnListChanged += OnLobbyListChanged;
        }
    }

    private void OnDisable()
    {
        if (rm != null)
        {
            rm.LobbyPlayers.OnListChanged -= OnLobbyListChanged;
        }
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

            // NEW: instantiate and setup the row script
            GameObject rowObj = Instantiate(nameRowPrefab, parent);

            LobbyPlayerRowUI row = rowObj.GetComponent<LobbyPlayerRowUI>();
            if (row != null)
            {
                row.Setup(this, p.clientId, p.name.ToString());
                row.SetSelected(selectedClientId.HasValue && selectedClientId.Value == p.clientId);
            }
            else
            {
                // fallback: at least set the text if the prefab doesn't have the script
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

        rm.MoveSelectedPlayerServerRpc(selectedClientId.Value); // add this RPC in RoomManagerLan
    }


    // Hook this to Start Game button OnClick
    public void OnClickStartGame()
    {
        if (rm != null) rm.StartMatchServerRpc();
    }
    
    public void SelectPlayer(ulong clientId)
    {
        // Only host should be able to select/move others
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsHost)
            return;

        selectedClientId = clientId;
        RebuildLists(); // rebuild highlights (simple approach)
    }
    
    // Hook this to your Quit button OnClick
    public void OnClickQuit()
    {
        // Optional: hide the lobby UI immediately
        gameObject.SetActive(false);

        // Stop being "host" next time if you store that flag
        PlayerPrefs.SetInt("LAN_IsHost", 0);

        // Shut down Netcode cleanly (works for both host and client)
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }

        // Load your menu scene (build index 0)
        SceneManager.LoadScene(0);
    }


}
