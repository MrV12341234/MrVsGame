using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

[NetworkMode(NetworkMode.LAN)]
public class TeamScoreManagerLan : NetworkBehaviour
{
    [Header("UI Roots (boxes)")]
    public GameObject blueBoxRoot;
    public GameObject redBoxRoot;

    [Header("Score Text")]
    public TextMeshProUGUI blueScoreText;
    public TextMeshProUGUI redScoreText;

    [Header("Update Rate")]
    public float updateInterval = 0.5f;

    private NetworkVariable<int> blueScore =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> redScore =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    //called in CTFGameManagerLan
    public static TeamScoreManagerLan Instance;
    private void Awake() => Instance = this;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Show/hide based on mode (works on host + clients)
        RefreshVisibility();

        // Update UI when values change
        blueScore.OnValueChanged += (_, __) => UpdateUI();
        redScore.OnValueChanged += (_, __) => UpdateUI();

        UpdateUI();

        if (IsServer)
        {
            // If CTF, scores are flag captures, not summed player points (like in teams mode)
            if (RoomManagerLan.Instance != null && RoomManagerLan.Instance.IsCTFMode)
                Server_ResetCTFScores();
            else
                StartCoroutine(Server_RecomputeTotalsLoop());
        }
    }

    private void RefreshVisibility()
    {
        bool isTeams = RoomManagerLan.Instance != null && RoomManagerLan.Instance.IsTeamsMode;

        if (blueBoxRoot) blueBoxRoot.SetActive(isTeams);
        if (redBoxRoot) redBoxRoot.SetActive(isTeams);
    }

    private void UpdateUI()
    {
        if (blueScoreText) blueScoreText.text = blueScore.Value.ToString();
        if (redScoreText) redScoreText.text = redScore.Value.ToString();
    }
    
    public void Server_ResetCTFScores()
    {
        if (!IsServer) return;
        blueScore.Value = 0;
        redScore.Value = 0;
    }

    public void Server_AddCTFCapture(RoomManagerLan.TeamId team)
    {
        if (!IsServer) return;

        if (team == RoomManagerLan.TeamId.Blue) blueScore.Value++;
        else redScore.Value++;
        // after point scored, run check to see if points-to-win has been reached
        Server_CheckForTeamPointsWin();
    }

    private IEnumerator Server_RecomputeTotalsLoop()
    {
        var wait = new WaitForSeconds(updateInterval);

        while (IsServer && isActiveAndEnabled)
        {
            // If not teams, keep hidden/zero and skip work
            if (RoomManagerLan.Instance == null || !RoomManagerLan.Instance.IsTeamsMode)
            {
                blueScore.Value = 0;
                redScore.Value = 0;
                
                yield return wait;
                continue;
            }

            int b = 0;
            int r = 0;

            var lb = LeaderboardManagerLAN.Instance;
            var rm = RoomManagerLan.Instance;

            if (lb != null && rm != null)
            {
                // Sum scores for every known player in the leaderboard
                foreach (var clientId in lb.Server_GetAllClientIds())
                {
                    int s = lb.Server_GetScore(clientId);

                    if (rm.TryGetTeamForClient(clientId, out var team))
                    {
                        if (team == RoomManagerLan.TeamId.Blue) b += s;
                        else r += s;
                    }
                }
            }

            // update team points
            blueScore.Value = b;
            redScore.Value = r;
            
            //check if points-to-win amount has been reached
            Server_CheckForTeamPointsWin();

            yield return wait;
        }
    }
    
    private void Server_CheckForTeamPointsWin()
    {
        if (!IsServer) return;

        var rm = RoomManagerLan.Instance;
        if (rm == null) return;
        if (!rm.IsTeamsMode) return;
        if (!rm.UsesPoints) return;
        if (rm.TargetPointsToWin <= 0) return;
        if (!rm.IsMatchStarted) return;

        if (blueScore.Value >= rm.TargetPointsToWin || redScore.Value >= rm.TargetPointsToWin)
        {
            GameEndScreenLan.Instance?.Server_ShowGameOverFromPoints();
        }
    }
    
    // used in EndGameMenu script
    public int GetBlueScore()
    {
        return blueScore.Value;
    }

    public int GetRedScore()
    {
        return redScore.Value;
    }
}

