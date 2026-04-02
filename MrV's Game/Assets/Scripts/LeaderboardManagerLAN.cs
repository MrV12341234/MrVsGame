using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

[NetworkMode(NetworkMode.LAN)]
public class LeaderboardManagerLAN : NetworkBehaviour
{
    public static LeaderboardManagerLAN Instance;
    public static string WinningPlayerName = "";

    [Header("UI References")]
    public GameObject leaderboardUI;
    public Transform playerItemPrefabParent;
    public GameObject playerItemPrefab;

    [Header("Scoring")]
    [Tooltip("Points awarded to the attacker per successful hit")]
    public int pointsPerHit = 2;
    [Tooltip("Points awarded to the attacker per kill")]
    public int pointsPerKill = 5;
    [Tooltip("Points awarded for answering a trivia question correctly")]
    public int pointsPerCorrectAnswer = 0;
    [Tooltip("Points deducted for answering a trivia question incorrectly")]
    public int pointsPerWrongAnswer = -5;

    // --- internal authoritative state (server only) ---
    private readonly Dictionary<ulong, PlayerScoreData> _scores = new Dictionary<ulong, PlayerScoreData>();

    // --- client-side cache for UI (replicated via RPC ticks) ---
    private List<PlayerScoreData> _clientView = new List<PlayerScoreData>();
    private bool _ready;

    private void Awake() => Instance = this;

    private void Start()
    {
        // Tab key show/hide (works with old Input Manager – fine alongside new Input System)
        InvokeRepeating(nameof(RefreshLeaderboardUI), 0.5f, 0.5f);
    }

    private void Update()
    {
        if (leaderboardUI)
            leaderboardUI.SetActive(UnityEngine.Input.GetKey(KeyCode.Tab));
    }

    public override void OnNetworkSpawn()
    {
        _ready = true;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            // Register everyone currently connected
            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
                Server_RegisterPlayerIfNeeded(kvp.Key);

            // Start small state tick to clients
            StartCoroutine(Server_TickLeaderboardToClients());
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // ---------- Server: player lifecycle ----------
    private void OnClientConnected(ulong clientId) => Server_RegisterPlayerIfNeeded(clientId);

    private void OnClientDisconnected(ulong clientId)
    {
        // remove from scores; clients will get the next tick and UI will clear
        if (IsServer && _scores.Remove(clientId))
        {
            // nothing else to do; next tick pushes to everyone
        }
    }

    private void Server_RegisterPlayerIfNeeded(ulong clientId)
    {
        if (!IsServer) return;
        if (_scores.ContainsKey(clientId)) return;

        _scores[clientId] = new PlayerScoreData
        {
            clientId = clientId,
            kills = 0,
            deaths = 0,
            score = 0
        };
    }

    // ---------- Public scoring API (call from weapons / gameplay) ----------
    // These are SERVER-SIDE helpers you can call directly from server code.
    // If you need to call from client, use the *ServerRpc variants below.*

    public void Server_AwardHit(ulong attackerClientId)
    {
        if (!IsServer) return;
        if (!_scores.TryGetValue(attackerClientId, out var row)) return;

        row.score += pointsPerHit;
        _scores[attackerClientId] = row;
        // after hit, check to see if score crossed the end of match threshold
        Server_CheckForFFAPointsWin();
    }

    public void Server_AwardKill(ulong attackerClientId)
    {
        if (!IsServer) return;
        if (!_scores.TryGetValue(attackerClientId, out var row)) return;

        row.kills += 1;
        row.score += pointsPerKill;
        _scores[attackerClientId] = row;
        // after kill, check to see if your score crossed the end of match threshold
        Server_CheckForFFAPointsWin();
    }

    public void Server_RegisterDeath(ulong victimClientId)
    {
        if (!IsServer) return;
        if (!_scores.TryGetValue(victimClientId, out var row)) return;

        row.deaths += 1;
        _scores[victimClientId] = row;
    }

    public void Server_AwardCorrect(ulong clientId)
    {
        if (!IsServer) return;
        if (!_scores.TryGetValue(clientId, out var row)) return;

        row.score += pointsPerCorrectAnswer;
        _scores[clientId] = row;
        // after points added for correct answer, check to see if total score crossed the end of match threshold
        Server_CheckForFFAPointsWin();
    }

    public void Server_AwardWrong(ulong clientId)
    {
        if (!IsServer) return;
        if (!_scores.TryGetValue(clientId, out var row)) return;

        row.score += pointsPerWrongAnswer;
        _scores[clientId] = row;
        // after wrong answer, check to see if score crossed the end of match threshold
        Server_CheckForFFAPointsWin();
    }

    // ---------- Client entry points (if you ever want to report from client) ----------
    [ServerRpc(RequireOwnership = false)]
    public void ReportHitServerRpc(ulong attackerClientId) => Server_AwardHit(attackerClientId);

    [ServerRpc(RequireOwnership = false)]
    public void ReportKillServerRpc(ulong attackerClientId) => Server_AwardKill(attackerClientId);

    [ServerRpc(RequireOwnership = false)]
    public void ReportDeathServerRpc(ulong victimClientId) => Server_RegisterDeath(victimClientId);

    [ServerRpc(RequireOwnership = false)]
    public void ReportCorrectAnswerServerRpc(ulong clientId) => Server_AwardCorrect(clientId);

    [ServerRpc(RequireOwnership = false)]
    public void ReportWrongAnswerServerRpc(ulong clientId) => Server_AwardWrong(clientId);

    // ---------- Server → clients replication tick ----------
    private IEnumerator Server_TickLeaderboardToClients()
    {
        var wait = new WaitForSeconds(0.5f);
        while (IsServer && isActiveAndEnabled)
        {
            // prepare payload (ordered)
            var rows = _scores.Values
                              .OrderByDescending(r => r.score)
                              .ToList();
            // also compute winning player for convenience
            var top = rows.FirstOrDefault();
            var topName = (top != null) ? ResolveName(top.clientId) : "";
            WinningPlayerName = topName;

            // serialize to DTOs
            var dto = rows.Select(r => new PlayerScoreDTO
            {
                clientId = r.clientId,
                score = r.score,
                kills = r.kills,
                deaths = r.deaths,
                // include name to avoid name lookups on clients each tick
                name = new FixedString64Bytes(ResolveName(r.clientId))
            }).ToArray();

            PushLeaderboardClientRpc(dto);

            yield return wait;
        }
    }

    [ClientRpc]
    private void PushLeaderboardClientRpc(PlayerScoreDTO[] rows)
    {
        if (IsServer) return; // server doesn’t need the cache

        _clientView = rows.Select(r => new PlayerScoreData
        {
            clientId = r.clientId,
            score = r.score,
            kills = r.kills,
            deaths = r.deaths,
            name = r.name.ToString()   // <-- keep the server-resolved name
        }).ToList();

        // cache winning name locally for easy HUD use
        WinningPlayerName = rows.Length > 0 ? rows[0].name.ToString() : "";
    }

    // ---------- UI ----------
    private void RefreshLeaderboardUI()
    {
        if (!_ready || playerItemPrefabParent == null || playerItemPrefab == null) return;

        // clear existing list
        for (int i = playerItemPrefabParent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(playerItemPrefabParent.GetChild(i).gameObject);

        List<(ulong id, string name, int score, int kills, int deaths)> ordered;

        if (IsServer)
        {
            // server renders from authoritative state
            var rows = _scores.Values.OrderByDescending(r => r.score).ToList();
            ordered = rows.Select(r => (r.clientId, ResolveName(r.clientId), r.score, r.kills, r.deaths)).ToList();
            WinningPlayerName = ordered.Count > 0 ? ordered[0].name : "";
        }
        else
        {
            // clients render from cached view; also resolve names locally (in case)
            var rows = _clientView.OrderByDescending(r => r.score).ToList();
            ordered = rows.Select(r => (r.clientId, r.name, r.score, r.kills, r.deaths)).ToList();
            WinningPlayerName = ordered.Count > 0 ? ordered[0].name : "";
        }

        foreach (var row in ordered)
        {
            var item = UnityEngine.Object.Instantiate(playerItemPrefab, playerItemPrefabParent);

            var isMe = row.id == NetworkManager.Singleton.LocalClientId;
            var displayName = isMe ? $"<color=#FFC200>{row.name}</color>" : row.name;

            item.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = displayName;
            item.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = row.score.ToString();
            item.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = $"{row.kills} / {row.deaths}";
        }
    }

    private string ResolveName(ulong clientId)
    {
        // 1) Prefer the replicated NetworkVariable on the Player object (works on host & clients)
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var nc) &&
            nc.PlayerObject != null)
        {
            var setup = nc.PlayerObject.GetComponent<PlayerSetupLan>();
            if (setup != null)
                return setup.GetPlayerNameString();
        }

        // 2) Server-only fallback for very early moments (pre-spawn / re-spawn) using the server cache
        if (IsServer && RoomManagerLan.Instance != null)
            return RoomManagerLan.Instance.GetStoredPlayerName(clientId);

        // 3) Last resort
        return $"Player_{clientId}";
    }
    // SERVER ONLY helpers for other server systems (TeamScoreManager, win conditions, etc.)
    public IEnumerable<ulong> Server_GetAllClientIds()
    {
        if (!IsServer) yield break;
        foreach (var id in _scores.Keys)
            yield return id;
    }

    public int Server_GetScore(ulong clientId)
    {
        if (!IsServer) return 0;
        if (_scores.TryGetValue(clientId, out var row))
            return row.score;
        return 0;
    }
    
    private void Server_CheckForFFAPointsWin()
    {
        if (!IsServer) return;

        var rm = RoomManagerLan.Instance;
        if (rm == null) return;
        if (rm.IsTeamsMode) return;
        if (!rm.UsesPoints) return;
        if (rm.TargetPointsToWin <= 0) return;
        if (GameEndScreenLan.Instance == null) return;

        int spawnedPlayers = 0;

        if (NetworkManager.Singleton != null)
        {
            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                if (kvp.Value != null && kvp.Value.PlayerObject != null)
                    spawnedPlayers++;
            }
        }

        // Prevent ending the match before at least two real players are in the FFA match
        if (spawnedPlayers < 2) return;

        foreach (var row in _scores.Values)
        {
            if (row.score >= rm.TargetPointsToWin)
            {
                GameEndScreenLan.Instance.Server_ShowGameOverFromPoints();
                return;
            }
        }
    }
    public List<PlayerScoreData> Server_GetOrderedSnapshot()
    {
        if (!IsServer)
            return new List<PlayerScoreData>();

        return _scores.Values
            .OrderByDescending(r => r.score)
            .ThenBy(r => r.deaths)
            .Select(r => new PlayerScoreData
            {
                clientId = r.clientId,
                score = r.score,
                kills = r.kills,
                deaths = r.deaths,
                name = ResolveName(r.clientId)
            })
            .ToList();
    }
}

// Internal data for server & clients
[Serializable]
public class PlayerScoreData
{
    public ulong clientId;
    public int score;
    public int kills;
    public int deaths;
    public string name;
}


// Compact replication DTO (includes name for client convenience)
public struct PlayerScoreDTO : INetworkSerializable
{
    public ulong clientId;
    public int score;
    public int kills;
    public int deaths;
    public FixedString64Bytes name;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref score);
        serializer.SerializeValue(ref kills);
        serializer.SerializeValue(ref deaths);
        serializer.SerializeValue(ref name);
    }
}

