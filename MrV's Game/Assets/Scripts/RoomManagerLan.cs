using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
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

    [HideInInspector] public int correctAnswerCounter = 0;

    [Header("Name Entry UI")]
    public GameObject nameEntryUI;
    public TMP_InputField nameInputField;
    public GameObject connectingUI;
    public TextMeshProUGUI warningText;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (GameMode.IsLAN && !NetworkManager.Singleton.IsHost)
        {
            Debug.Log("[LAN] Showing name entry UI for client.");
            nameEntryUI?.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void OnJoinClicked()
    {
        string name = nameInputField?.text.Trim() ?? "";

        if (string.IsNullOrEmpty(name))
        {
            name = "Chocolate";
            nameInputField.text = name;
        }

        if (name.Length > 12)
            name = name.Substring(0, 12);

        PlayerPrefs.SetString("PlayerName", name);
        currentName = name;

        nameEntryUI?.SetActive(false);
        connectingUI?.SetActive(true);

        bool isHost = PlayerPrefs.GetInt("LAN_IsHost", 0) == 1;

        if (isHost)
        {
            Debug.Log("[RoomManagerLan] Starting Host...");
            NetworkManager.Singleton.OnServerStarted += () =>
            {
                Debug.Log("[RoomManagerLan] Host server started. Spawning player...");
                RespawnPlayer();
            };
            
            NetworkManager.Singleton.StartHost();
        }
        else
        {
            Debug.Log("[RoomManagerLan] Starting Client...");
            
            // Apply the stored IP before trying to connect
            string ip = PlayerPrefs.GetString("JoinLAN_IP", "127.0.0.1");
            var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            transport.ConnectionData.Address = ip;
            transport.ConnectionData.Port = 7777;

            Debug.Log($"[RoomManagerLan] Client will attempt to connect to: {ip}");
            
            NetworkManager.Singleton.OnClientConnectedCallback += (clientId) =>
            {
                if (clientId == NetworkManager.Singleton.LocalClientId)
                {
                    Debug.Log("[RoomManagerLan] Client connected. Waiting for host to spawn player...");
                }
            };
            
            NetworkManager.Singleton.StartClient();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && roomCamera != null)
        {
            roomCamera.SetActive(false);
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

    public void ShowQuiz()
    {
        if (showQuiz)
        {
            Quiz.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            correctAnswerCounter = 0;

            QuestionSetup setup = Quiz.GetComponentInChildren<QuestionSetup>();
            if (setup != null)
            {
                setup.InitializeNewQuestion();
            }
        }
        else
        {
            RequestRespawn();
        }
    }
    public void getCorrectAnswer()
    {
        if (IsServer)
        {
            ApplyCorrectAnswer();
        }
        else
        {
            SubmitCorrectAnswerServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitCorrectAnswerServerRpc(ServerRpcParams rpcParams = default)
    {
        ApplyCorrectAnswer();
    }

    private void ApplyCorrectAnswer()
    {
        correctAnswerCounter++;
        if (correctAnswerCounter == 3)
        {
            Quiz.SetActive(false);
            RequestRespawn();
        }
        StartCoroutine(showCorrectAnswer());
    }

    public void getWrongAnswer()
    {
        StartCoroutine(showWrongAnswer());
    }

    IEnumerator showCorrectAnswer()
    {
        correctAnswer.SetActive(true);
        yield return new WaitForSeconds(0.5f);
        correctAnswer.SetActive(false);
    }

    IEnumerator showWrongAnswer()
    {
        wrongAnswer.SetActive(true);
        yield return new WaitForSeconds(5f);
        wrongAnswer.SetActive(false);
    }

    public void RespawnPlayer()
    {
        if (!IsServer)
        {
            Debug.LogWarning("Only server can respawn players.");
            return;
        }

        Vector3 spawnPos = GetRandomSpawnPos();
        Debug.Log($"[LAN] Spawning player at: {spawnPos}");

        NetworkObject newPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity).GetComponent<NetworkObject>();
        newPlayer.SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId);
    }

    public void RequestRespawn()
    {
        if (IsServer)
        {
            RespawnPlayer();
        }
        else
        {
            RequestRespawnServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRespawnServerRpc(ServerRpcParams rpcParams = default)
    {
        RespawnPlayer();
    }

    public Vector3 GetRandomSpawnPos()
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("No spawn points assigned!");
            return Vector3.zero;
        }

        return spawnPoints[Random.Range(0, spawnPoints.Length)].position;
    }
}
