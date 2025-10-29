using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;

[NetworkMode(NetworkMode.LAN)]
public class RoomManagerLan : NetworkBehaviour
{
    public static RoomManagerLan Instance;

    [Header("Room Info")]
    public string roomCode = "Map1";
    public string roomNameToJoin = "test";
    private string currentName = "Chocolate";

    [Header("Player Setup")]
    public GameObject playerPrefab;
    public Transform[] spawnPoints;
    public GameObject roomCamera;

    [Header("Trivia System")]
    public GameObject Quiz;
    public GameObject correctAnswer;
    public GameObject wrongAnswer;
    public bool showQuiz;
    [SerializeField] private bool requireQuizBeforeFirstSpawn = true;

    // Legacy public counter (UI only)
    [HideInInspector] public int correctAnswerCounter = 0;

    // Local-only counter for this client's quiz progress
    private int _localCorrectCount = 0;

    [Header("Name Entry UI")]
    public GameObject nameEntryUI;
    public TMP_InputField nameInputField;
    public GameObject connectingUI;
    public TextMeshProUGUI warningText;
    
    // Add a dictionary to track player names on the server
    private readonly Dictionary<ulong, string> playerNames = new Dictionary<ulong, string>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (GameMode.IsLAN && !NetworkManager.Singleton.IsHost)
        {
            Debug.Log("[LAN] Showing name entry UI for client.");
            if (nameEntryUI) nameEntryUI.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void OnJoinClicked()
    {
        Debug.Log("[LAN DEBUG] NetworkManager.Singleton = " + NetworkManager.Singleton);

        string name = nameInputField ? nameInputField.text.Trim() : "";

        if (string.IsNullOrEmpty(name))
        {
            name = "Chocolate";
            if (nameInputField) nameInputField.text = name;
        }

        if (name.Length > 12)
            name = name.Substring(0, 12);

        PlayerPrefs.SetString("PlayerName", name);
        currentName = name;
        
        if (nameEntryUI) nameEntryUI.SetActive(false);
        if (connectingUI) connectingUI.SetActive(true);

        bool isHost = PlayerPrefs.GetInt("LAN_IsHost", 0) == 1;

        if (isHost)
        {
            Debug.Log("[RoomManagerLan] Starting Host...");
            NetworkManager.Singleton.StartHost();
            // We spawn players in OnClientConnected; do not spawn here.
        }
        else
        {
            Debug.Log("[RoomManagerLan] Starting Client...");
            string ip = PlayerPrefs.GetString("JoinLAN_IP", "127.0.0.1");
            Debug.Log($"[RoomManagerLan] Client will attempt to connect to: {ip}");
            LanNetworkManager.Instance.JoinLanGame(ip); // calls StartClient()
        }
    }

    public override void OnNetworkSpawn()
    {
        // Disable the menu/room camera on this peer
        if (roomCamera != null)
        {
            roomCamera.SetActive(false);
            var al = roomCamera.GetComponent<AudioListener>();
            if (al) al.enabled = false;
        }

        if (IsOwner)
        {
            Debug.Log("[LAN] RoomManagerLan.OnNetworkSpawn for local player.");
        }
    }

    public void ChangeName(string _name)
    {
        if (!string.IsNullOrWhiteSpace(_name))
        {
            if (_name.Length > 12)
                _name = _name.Substring(0, 12);

            currentName = _name;
        }
    }
    
    /// <summary>
    /// Store player name when they first connect
    /// </summary>
    public void StorePlayerName(ulong clientId, string name)
    {
        if (!IsServer) return;
        
        playerNames[clientId] = name;
        Debug.Log($"[RoomManagerLan] Stored name '{name}' for client {clientId}");
    }

    /// <summary>
    /// Get stored player name for respawn
    /// </summary>
    public string GetStoredPlayerName(ulong clientId)
    {
        if (playerNames.ContainsKey(clientId))
        {
            return playerNames[clientId];
        }
        return $"Player_{clientId}";
    }

    /// <summary>
    /// LOCAL ONLY — show the quiz on this client (no RPC).
    /// Called by PlayerHealthLan when the local owner dies.
    /// </summary>
    public void ShowQuiz()
    {
        if (!showQuiz || !Quiz) return;

        Quiz.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        correctAnswerCounter = 0;    // legacy UI counter (visual only)
        _localCorrectCount = 0;      // local logic counter

        var setup = Quiz.GetComponentInChildren<QuestionSetup>();
        if (setup != null)
        {
            if (setup.feedbackText) setup.feedbackText.text = "";
            setup.InitializeNewQuestion();
        }
    }

    /// <summary>
    /// LOCAL ONLY — button handler for "Correct".
    /// Do not gate with IsOwner: this object is server-owned; the UI is local.
    /// </summary>
    public void getCorrectAnswer()
    {
        // Local feedback only
        StartCoroutine(showCorrectAnswer());

        _localCorrectCount++;
        correctAnswerCounter = _localCorrectCount; // keep UI value in sync if other code reads it

        if (_localCorrectCount >= 3)
        {
            _localCorrectCount = 0;
            HideQuizAndLockCursorLocal();

            // Are we already spawned?
            bool hasPlayerObject =
                NetworkManager.Singleton.LocalClient != null &&
                NetworkManager.Singleton.LocalClient.PlayerObject != null;

            if (!hasPlayerObject && requireQuizBeforeFirstSpawn)
            {
                // This is the initial gate – spawn for the first time.
                RequestInitialSpawn();
            }
            else
            {
                // This is a post-death quiz – do a regular respawn.
                RequestRespawn();
            }
        }
        else
        {
            // Load next question locally (unchanged)
            var setup = Quiz ? Quiz.GetComponentInChildren<QuestionSetup>() : null;
            if (setup)
            {
                if (setup.feedbackText) setup.feedbackText.text = "";
                setup.InitializeNewQuestion();
            }
        }

    }

    /// <summary>
    /// LOCAL ONLY — button handler for "Wrong".
    /// </summary>
    public void getWrongAnswer()
    {
        // Show local “wrong” feedback only. DO NOT advance questions here.
        StartCoroutine(showWrongAnswer());
    }

    private IEnumerator showCorrectAnswer()
    {
        if (correctAnswer)
        {
            correctAnswer.SetActive(true);
            yield return new WaitForSeconds(0.5f);
            correctAnswer.SetActive(false);
        }
    }

    private IEnumerator showWrongAnswer()
    {
        if (wrongAnswer)
        {
            wrongAnswer.SetActive(true);
            yield return new WaitForSeconds(0.5f);
            wrongAnswer.SetActive(false);
        }
    }

    /// <summary>
    /// Local convenience to ensure UI is hidden and cursor relocked.
    /// Also safe to call from PlayerSetupLan.OnNetworkSpawn after respawn.
    /// </summary>
    public void HideQuizAndLockCursorLocal()
    {
        if (Quiz && Quiz.activeSelf) Quiz.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SpawnPlayerFor(ulong clientId, string chosenName)
    {
        if (!IsServer)
        {
            Debug.LogWarning("Only server can spawn players.");
            return;
        }

        // Duplicate guard
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var nc) && nc.PlayerObject != null)
        {
            Debug.Log($"[RoomManagerLan] Client {clientId} already has a PlayerObject. Skipping spawn.");
            return;
        }

        var spawnPos = GetRandomSpawnPos();
        var go = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        
        var netObj = go.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);  
        
        // Store the name for future respawns
        StorePlayerName(clientId, chosenName);
        
        // Set the name on the PlayerSetup component
        var setup = go.GetComponent<PlayerSetupLan>();
        if (setup != null)
        {
            setup.ServerSetName(chosenName);
        }

        Debug.Log($"[LAN][Server] Spawned PlayerObject. RequestedOwner={clientId}, ActualOwner={netObj.OwnerClientId}, NetworkObjectId={netObj.NetworkObjectId}");
        // Note: The name is now set by the client in PlayerSetupLan.OnNetworkSpawn
        // but we ensure it's stored server-side for respawns
        
    }

    /// <summary>
    /// LOCAL entry point. Host can call directly (spawns for host).
    /// Clients call the ServerRpc (below).
    /// </summary>
    public void RequestRespawn()
    {
        if (IsServer)
        {
            // Host respawning self - use stored name
            string hostName = GetStoredPlayerName(NetworkManager.Singleton.LocalClientId);
            SpawnPlayerFor(NetworkManager.Singleton.LocalClientId, hostName);
        }
        else
        {
            // Client requests respawn - server will use stored name
            RequestRespawnServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRespawnServerRpc(ServerRpcParams rpcParams = default)
    {
        // Spawn a player object for the client who sent this RPC, pass in the gamertag also so host assigns
        ulong clientId = rpcParams.Receive.SenderClientId;
        string playerName = GetStoredPlayerName(clientId);
            
        SpawnPlayerFor(clientId, playerName);
    }


    public Vector3 GetRandomSpawnPos()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("No spawn points assigned!");
            return Vector3.zero;
        }

        return spawnPoints[Random.Range(0, spawnPoints.Length)].position;
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[RoomManagerLan] OnClientConnected {clientId} (IsServer={IsServer})");

        if (IsServer)
        {
            // Do NOT spawn here anymore. We wait until the client passes the quiz and asks to spawn.
            // Store a best-effort name now; client may set it later too.
            string playerName = $"Player_{clientId}";
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                playerName = PlayerPrefs.GetString("PlayerName", playerName);
            }
            StorePlayerName(clientId, playerName);
            Debug.Log($"[RoomManagerLan] (Server) Registered {playerName} for {clientId}. Waiting for initial quiz pass...");
        }

        // Local client UI housekeeping
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (connectingUI) connectingUI.SetActive(false);

            // If we’re gating first spawn, show the quiz now.
            if (requireQuizBeforeFirstSpawn)
            {
                ShowQuiz();
            }
            else
            {
                // If not gating, go ahead and spawn immediately.
                RequestInitialSpawn();
            }
        }
    }
    
    /// <summary>
    /// Client asks the server to spawn their PlayerObject for the first time,
    /// but only after they’ve passed the initial quiz.
    /// </summary>
    public void RequestInitialSpawn()
    {
        if (IsServer)
        {
            // Host spawning themselves
            string hostName = GetStoredPlayerName(NetworkManager.Singleton.LocalClientId);
            SpawnPlayerFor(NetworkManager.Singleton.LocalClientId, hostName);
        }
        else
        {
            InitialQuizPassedServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void InitialQuizPassedServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        string playerName = GetStoredPlayerName(clientId);
        Debug.Log($"[RoomManagerLan] Initial quiz passed by {clientId}. Spawning now...");
        SpawnPlayerFor(clientId, playerName);
    }



    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[RoomManagerLan] OnClientDisconnected {clientId}");
        // Remove from name dictionary when player disconnects
        if (IsServer && playerNames.ContainsKey(clientId))
        {
            playerNames.Remove(clientId);
        }
    }
}
