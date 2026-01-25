using Unity.Netcode;
using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
using Unity.Collections;

[NetworkMode(NetworkMode.LAN)]
public class RoomManagerLan : NetworkBehaviour
{
    public static RoomManagerLan Instance;

    [Header("Room Info")]
    public string roomCode = "Map1";
    public string roomNameToJoin = "test";
    private string currentName = "Chocolate";
    public enum LanGameMode { FFA = 0, Teams = 1 }
    
    [Header("Game Mode")]
    public LanGameMode gameMode = LanGameMode.FFA;
    public bool IsTeamsMode => gameMode == LanGameMode.Teams;
    [Header("Teams Lobby UI")]
    public TeamLobbyUI teamLobbyUI; // drag your TeamsLobbyCanvas (the object with TeamLobbyUI) here

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
    
    [Header("Disconnect UI")]
    public GameObject hostLeftPanel;            // Optional: assign a panel/canvas for host-left message
    public TextMeshProUGUI hostLeftText;        // Optional: the text element on that panel

    private bool _handledHostLeft = false;      // guard to avoid double handling

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

    
    [Header("Teams Mode")]
    [SerializeField] private bool enableTeamsLobby = true;

// Server decides when match starts
    private NetworkVariable<bool> matchStarted =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public bool IsMatchStarted => matchStarted.Value;
    

// Synced lobby list (everyone can read)
    public NetworkList<LobbyPlayerState> LobbyPlayers { get; private set; }
    
    public enum TeamId : byte
    {
        Blue = 0,
        Red = 1
    }
    
    // This struct is synced to all clients in a NetworkList
    public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
    {
        public ulong clientId;
        public FixedString32Bytes name;
        public TeamId team;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref name);

            byte t = (byte)team;
            serializer.SerializeValue(ref t);
            if (serializer.IsReader) team = (TeamId)t;
        }

        // Required for NetworkList<T>
        public bool Equals(LobbyPlayerState other)
        {
            return clientId == other.clientId &&
                   name.Equals(other.name) &&
                   team == other.team;
        }

        public override bool Equals(object obj)
        {
            return obj is LobbyPlayerState other && Equals(other);
        }

        public override int GetHashCode()
        {
            // Simple stable hash
            unchecked
            {
                int hash = clientId.GetHashCode();
                hash = (hash * 397) ^ name.GetHashCode();
                hash = (hash * 397) ^ team.GetHashCode();
                return hash;
            }
        }
    }


    private void Awake()
    {
        Instance = this;
        LobbyPlayers = new NetworkList<LobbyPlayerState>();
    }

    private void Start()
    {
        // Read gamemode chosen in menu (host) or from discovered room (client)
        gameMode = (LanGameMode)PlayerPrefs.GetInt("LAN_GameMode", 0);
        
        if (GameMode.IsLAN && !NetworkManager.Singleton.IsHost)
        {
            if (nameEntryUI) nameEntryUI.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        
        Debug.Log("[RoomManagerLan] Game mode from prefs = " + gameMode);
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
       // if (connectingUI) connectingUI.SetActive(true);

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
            // If we're in Teams mode and match hasn't started yet, KEEP this camera on.
            bool isTeams = (gameMode == LanGameMode.Teams);

            if (!isTeams)
            {
                roomCamera.SetActive(false);
                var al = roomCamera.GetComponent<AudioListener>();
                if (al) al.enabled = false;
            }
            else
            {
                // Teams lobby needs a camera until players spawn
                roomCamera.SetActive(true);
                var al = roomCamera.GetComponent<AudioListener>();
                if (al) al.enabled = true;
            }
        }

        if (IsOwner)
        {
            Debug.Log("[LAN] RoomManagerLan.OnNetworkSpawn for local player.");
        }
        
        // on clients (non-server), send the chosen name right away so the server uses it pre-spawn
        if (IsClient && !IsServer)
        {
            var earlyName = PlayerPrefs.GetString("PlayerName", $"Player_{NetworkManager.Singleton.LocalClientId}");
            SubmitNameServerRpc(earlyName);
        }
        // handle late joiners: if match already started, kill room camera locally
        matchStarted.OnValueChanged += OnMatchStartedChanged;
        OnMatchStartedChanged(false, matchStarted.Value); // apply current value right now
        
    }
    
    private void OnMatchStartedChanged(bool oldValue, bool newValue)
    {
        if (!newValue) return;

        // Hide lobby UI if it exists
        if (teamLobbyUI != null)
            teamLobbyUI.HideLobby();

        // Disable the lobby/room camera locally so Camera.main becomes the player camera
        if (roomCamera != null)
        {
            roomCamera.SetActive(false);
            var al = roomCamera.GetComponent<AudioListener>();
            if (al) al.enabled = false;
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
    /// Global helper: resolve a clientId to the stored gamertag,
    /// or "Player_{id}" if we have nothing.
    /// </summary>
    public static string ResolvePlayerName(ulong clientId)
    {
        if (Instance == null)
            return $"Player_{clientId}";

        return Instance.GetStoredPlayerName(clientId);
    }

    /// <summary>
    /// LOCAL ONLY — show the quiz on this client (no RPC).
    /// Called by PlayerHealthLan when the local owner dies.
    /// </summary>
    public void ShowQuiz()
    {
        if (!showQuiz || !Quiz) return;
        
        if (teamLobbyUI != null)
            teamLobbyUI.HideLobby();

        
        if (connectingUI != null) connectingUI.SetActive(false);
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
        correctAnswerCounter = _localCorrectCount;

        if (_localCorrectCount < 3)
        {
            // Next question
            var setup = Quiz ? Quiz.GetComponentInChildren<QuestionSetup>() : null;
            if (setup)
            {
                if (setup.feedbackText) setup.feedbackText.text = "";
                setup.InitializeNewQuestion();
            }
            return;
        }

        // ---- PASSED QUIZ ----
        _localCorrectCount = 0;

        // Force feedback off (prevents “Correct” getting stuck)
        if (correctAnswer) correctAnswer.SetActive(false);
        if (wrongAnswer) wrongAnswer.SetActive(false);
        StopAllCoroutines(); // stops showCorrectAnswer/showWrongAnswer if they were mid-run

        // Are we already spawned?
        bool hasPlayerObject =
            NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null;

        // FIRST SPAWN GATE
        if (!hasPlayerObject && requireQuizBeforeFirstSpawn)
        {
            // Teams pre-game lobby
            if (gameMode == LanGameMode.Teams)
            {
                if (!IsMatchStarted)
                {
                    HideQuizOnly();
                    ShowTeamsLobbyLocal();

                    string n = PlayerPrefs.GetString("PlayerName", $"Player_{NetworkManager.Singleton.LocalClientId}");
                    EnterLobbyServerRpc(n);
                }
                else
                {
                    // Match running: never show lobby again
                    HideQuizAndLockCursorLocal();
                    RequestRespawn();
                }
                return;
            }

            //  FFA: after first quiz, spawn for the first time
            HideQuizAndLockCursorLocal();
            RequestInitialSpawn();
            return;
        }

        // POST-DEATH QUIZ (respawn)
        HideQuizAndLockCursorLocal();
        RequestRespawn();
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
    
    private void ShowTeamsLobbyLocal()
    {
        if (teamLobbyUI != null)
        {
            if (connectingUI != null) connectingUI.SetActive(false);
            teamLobbyUI.ShowLobby();

            // Lobby needs mouse
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Debug.LogWarning("[TeamsLobby] teamLobbyUI reference is not set in inspector!");
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
    
    // below used for teams mode so cursor is not locked after the first 3 answers.
    public void HideQuizOnly()
    {
        if (Quiz && Quiz.activeSelf) Quiz.SetActive(false);
    }

    // below 3 methods for teams lobby
    private int CountTeam(TeamId team)
    {
        int count = 0;
        for (int i = 0; i < LobbyPlayers.Count; i++)
            if (LobbyPlayers[i].team == team)
                count++;
        return count;
    }

    private TeamId GetTeamWithFewerPlayers()
    {
        int blue = CountTeam(TeamId.Blue);
        int red = CountTeam(TeamId.Red);
        return (blue <= red) ? TeamId.Blue : TeamId.Red;
    }

    private int FindLobbyIndex(ulong clientId)
    {
        for (int i = 0; i < LobbyPlayers.Count; i++)
            if (LobbyPlayers[i].clientId == clientId)
                return i;
        return -1;
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void EnterLobbyServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // If match already started, we won't lobby — we spawn you immediately on the team with fewest players
        if (matchStarted.Value)
        {
            // Late join: auto assign team and spawn immediately
            TeamId t = GetTeamWithFewerPlayers();
            SpawnPlayerFor_WithTeam(clientId, playerName, t);
            return;
        }

        if (string.IsNullOrWhiteSpace(playerName))
            playerName = $"Player_{clientId}";
        if (playerName.Length > 12)
            playerName = playerName.Substring(0, 12);

        // Store name in your existing server cache too
        StorePlayerName(clientId, playerName);

        TeamId team = GetTeamWithFewerPlayers();

        int idx = FindLobbyIndex(clientId);
        LobbyPlayerState state = new LobbyPlayerState
        {
            clientId = clientId,
            name = new FixedString32Bytes(playerName),
            team = team
        };

        if (idx >= 0) LobbyPlayers[idx] = state;
        else LobbyPlayers.Add(state);

        Debug.Log($"[TeamsLobby][Server] {playerName} entered lobby on team {team}");
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void SwitchTeamServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (matchStarted.Value) return;

        int idx = FindLobbyIndex(clientId);
        if (idx < 0) return;

        var s = LobbyPlayers[idx];
        s.team = (s.team == TeamId.Blue) ? TeamId.Red : TeamId.Blue;
        LobbyPlayers[idx] = s;

        Debug.Log($"[TeamsLobby][Server] {s.name} switched to {s.team}");
    }
    [ServerRpc(RequireOwnership = false)]
    public void StartMatchServerRpc(ServerRpcParams rpcParams = default)
    {
        // Only host/server can start
        if (!IsServer) return;
        if (matchStarted.Value) return;

        matchStarted.Value = true;
        Debug.Log("[TeamsLobby][Server] Match started!");

        // Spawn everyone in the lobby
        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            ulong id = LobbyPlayers[i].clientId;

            // if they already have a player object, skip
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(id, out var nc) && nc.PlayerObject != null)
                continue;

            string pname = LobbyPlayers[i].name.ToString();
            SpawnPlayerFor_WithTeam(id, pname, LobbyPlayers[i].team);
        }

        // Tell everyone to hide lobby UI
        HideLobbyClientRpc();
    }

    [ClientRpc]
    private void HideLobbyClientRpc()
    {
        // We'll have TeamLobbyUI listen for this too, but this is a simple “force hide”
        var ui = FindObjectOfType<TeamLobbyUI>();
        if (ui != null) ui.HideLobby();
        
        if (roomCamera != null)
        {
            roomCamera.SetActive(false);
            var al = roomCamera.GetComponent<AudioListener>();
            if (al) al.enabled = false;
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void MoveSelectedPlayerServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
    {
        // Only host/server should be allowed to do this
        if (!IsServer) return;

        // Optional extra protection: only allow the host client to request it
        if (rpcParams.Receive.SenderClientId != NetworkManager.Singleton.LocalClientId)
            return;

        if (matchStarted.Value) return; // don't allow changes after start

        int idx = FindLobbyIndex(targetClientId);
        if (idx < 0) return;

        var s = LobbyPlayers[idx];
        s.team = (s.team == TeamId.Blue) ? TeamId.Red : TeamId.Blue;
        LobbyPlayers[idx] = s;

        Debug.Log($"[TeamsLobby][Server] Host moved {s.name} to {s.team}");
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

        // --- NEW: teams-aware spawn ---
        if (IsTeamsMode)
        {
            // If we already know their team (lobby list), KEEP it (respawns/late join)
            if (!TryGetTeamForClient(clientId, out var t))
            {
                // If unknown (late join first time), assign balanced team
                t = GetTeamWithFewerPlayers();
            }

            SpawnPlayerFor_WithTeam(clientId, chosenName, t);
            return;
        }

        // --- FFA spawn (unchanged) ---
        var spawnPos = GetRandomSpawnPos();
        var go = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        var netObj = go.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);

        StorePlayerName(clientId, chosenName);

        var setup = go.GetComponent<PlayerSetupLan>();
        if (setup != null)
            setup.ServerSetName(chosenName);

        Debug.Log($"[LAN][Server] Spawned PlayerObject. RequestedOwner={clientId}, ActualOwner={netObj.OwnerClientId}, NetworkObjectId={netObj.NetworkObjectId}");
    }

    
    private void SpawnPlayerFor_WithTeam(ulong clientId, string chosenName, TeamId team)
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var nc) && nc.PlayerObject != null)
            return;
        
        StorePlayerName(clientId, chosenName);
        // ensure everyone (including late joiners) exists in LobbyPlayers for team lookup/UI
        UpsertLobbyPlayer(clientId, chosenName, team);

        var spawnPos = GetRandomSpawnPos();
        var go = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        var netObj = go.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);

        var setup = go.GetComponent<PlayerSetupLan>();
        if (setup != null)
        {
            setup.ServerSetName(chosenName);
            setup.ServerSetTeam(team); // <<< new (add in PlayerSetupLan below)
        }

        Debug.Log($"[TeamsLobby][Server] Spawned {chosenName} on {team}");
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
            string n = PlayerPrefs.GetString("PlayerName", $"Player_{NetworkManager.Singleton.LocalClientId}");
            RequestRespawnServerRpc(n);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRespawnServerRpc(string requestedName, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        requestedName = SanitizeName(requestedName, clientId);

        // Only set/overwrite if we don't already have a real name
        if (!playerNames.ContainsKey(clientId) || playerNames[clientId].StartsWith("Player_"))
            StorePlayerName(clientId, requestedName);

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

        return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].position;

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
            string n = PlayerPrefs.GetString("PlayerName", $"Player_{NetworkManager.Singleton.LocalClientId}");
            InitialQuizPassedServerRpc(n);

        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void InitialQuizPassedServerRpc(string requestedName, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        requestedName = SanitizeName(requestedName, clientId);
        
        
        // Only set if we don't already have a real name stored
        if (!playerNames.ContainsKey(clientId) || playerNames[clientId].StartsWith("Player_"))
            StorePlayerName(clientId, requestedName);

        Debug.Log($"[RoomManagerLan] Initial quiz passed by {clientId}. Spawning now...");
        SpawnPlayerFor(clientId, GetStoredPlayerName(clientId));
    }
    
    // send clients name before spawn (this way clients name appears in leaderboard before they answer the first 3 questions required for spawn.
    [ServerRpc(RequireOwnership = false)]
    private void SubmitNameServerRpc(string chosenName, ServerRpcParams rpcParams = default)
    {
        var clientId = rpcParams.Receive.SenderClientId;
        if (string.IsNullOrWhiteSpace(chosenName))
            chosenName = $"Player_{clientId}";
        if (chosenName.Length > 12)
            chosenName = chosenName.Substring(0, 12);

        StorePlayerName(clientId, chosenName); // updates the server cache used by ResolveName()
        Debug.Log($"[RoomManagerLan] (Server) Received early name '{chosenName}' from {clientId}");
    }
    private string SanitizeName(string s, ulong clientId)
    {
        if (string.IsNullOrWhiteSpace(s))
            s = $"Player_{clientId}";
        s = s.Trim();
        if (s.Length > 12) s = s.Substring(0, 12);
        return s;
    }

    
    public bool TryGetTeamForClient(ulong clientId, out TeamId teamId)
    {
        // Default
        teamId = default;

        if (LobbyPlayers == null) return false;

        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].clientId == clientId)
            {
                teamId = LobbyPlayers[i].team;
                return true;
            }
        }

        return false;
    }
    private void UpsertLobbyPlayer(ulong clientId, string playerName, TeamId team)
    {
        int idx = FindLobbyIndex(clientId);
        var state = new LobbyPlayerState
        {
            clientId = clientId,
            name = new FixedString32Bytes(playerName),
            team = team
        };

        if (idx >= 0) LobbyPlayers[idx] = state;
        else LobbyPlayers.Add(state);
    }


    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[RoomManagerLan] OnClientDisconnected {clientId}");

        // Server bookkeeping. // Remove from name dictionary when player disconnects
        if (IsServer && playerNames.ContainsKey(clientId))
        {
            playerNames.Remove(clientId);
        }
        
        if (IsServer)
        {
            int idx = FindLobbyIndex(clientId);
            if (idx >= 0)
                LobbyPlayers.RemoveAt(idx);
        }

        // --- Client-side host-left handling ---
        // If we are a CLIENT (not the server/host) and "we" are the one that just got disconnected,
        // it usually means the host shut down or we lost connection to host.
        if (!IsServer && clientId == NetworkManager.Singleton.LocalClientId && !_handledHostLeft)
        {
            _handledHostLeft = true;
            StartCoroutine(HandleHostLeftAndReturnToMenu());
        }
        
    }
    
    private IEnumerator HandleHostLeftAndReturnToMenu()
    {
        // Show message. This overrides what is typed in the inspector
        ShowHostLeftOverlay("Host left. Disconnecting...");

        // Make sure the cursor is available so players see the message.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return new WaitForSecondsRealtime(3f);

        // Prefer to reuse your PauseMenuManager cleanup path
        var pause = FindObjectOfType<PauseMenuManager>();
        if (pause != null)
        {
            pause.LeaveGame();
            yield break;
        }

        // Fallback: do a simple shutdown + scene load if PauseMenuManager isn't found.
        if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("[LAN] Client shutdown after host left.");
        }

        // If you want to reset stats here, you can, or just go straight to menu:
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    private void ShowHostLeftOverlay(string msg)
    {
        // Preferred: explicit host-left UI panel + text if you wired them.
        if (hostLeftPanel != null)
        {
            hostLeftPanel.SetActive(true);
            if (hostLeftText != null) hostLeftText.text = msg;
            return;
        }

        // Backup: reuse your existing warningText if present.
        if (warningText != null)
        {
            warningText.gameObject.SetActive(true);
            warningText.text = msg;
            return;
        }

        // Last resort: log it.
        Debug.LogWarning($"[LAN] {msg}");
    }
}