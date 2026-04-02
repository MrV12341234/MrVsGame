using UnityEngine;
using TMPro;
using Unity.Netcode;

[NetworkMode(NetworkMode.LAN)]
public class MatchTimerLAN : NetworkBehaviour
{
    public static MatchTimerLAN Instance;

    [Header("UI")]
    public TextMeshProUGUI matchTimeText;
    public GameObject timerRoot; // optional: assign the timer background/root if you want to hide whole timer UI
    public TextMeshProUGUI pointsToWinText; // assign the empty TMP text for points matches

    [Header("Optional Announcement UI")]
    public TextMeshProUGUI announcementText;
    public string waitingForPlayersMessage = "Waiting for another player to join before starting";
    
    private bool serverTimerEndHandled = false; 

    private NetworkVariable<bool> timerStarted =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<double> matchStartServerTime =
        new NetworkVariable<double>(0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> cachedDurationSeconds =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool TimerStarted => timerStarted.Value;
    public int DurationSeconds => cachedDurationSeconds.Value;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            var rm = RoomManagerLan.Instance;
            if (rm != null)
            {
                cachedDurationSeconds.Value = rm.MatchLengthSeconds;
            }
        }

        timerStarted.OnValueChanged += OnTimerStartedChanged;
        cachedDurationSeconds.OnValueChanged += OnDurationChanged;

        RefreshVisualState();
        UpdateTimerTextImmediate();
        UpdatePointsToWinMessage();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        timerStarted.OnValueChanged -= OnTimerStartedChanged;
        cachedDurationSeconds.OnValueChanged -= OnDurationChanged;
    }

    private void Update()
    {
        // Keep the timer/points UI state refreshed in case RoomManagerLan's
        // synced end-condition settings arrive slightly after this object spawns
        RefreshVisualState();
        UpdateTimerTextImmediate();
        UpdatePointsToWinMessage();

        // check if timer ends and trigger end game screen
        if (IsServer &&
            !serverTimerEndHandled &&
            timerStarted.Value &&
            GetRemainingSeconds() <= 0)
        {
            serverTimerEndHandled = true;
            GameEndScreenLan.Instance?.Server_ShowGameOverFromTimer();
        }
    }

    private void OnTimerStartedChanged(bool oldValue, bool newValue)
    {
        RefreshVisualState();
        UpdateTimerTextImmediate();
    }

    private void OnDurationChanged(int oldValue, int newValue)
    {
        RefreshVisualState();
        UpdateTimerTextImmediate();
    }

    private void RefreshVisualState()
    {
        var rm = RoomManagerLan.Instance;
        bool usesTimer = rm != null && rm.UsesTimer;

        if (timerRoot != null)
            timerRoot.SetActive(usesTimer);

        if (!usesTimer)
        {
            if (matchTimeText != null)
                matchTimeText.text = "";

            if (announcementText != null)
                announcementText.text = "";

            return;
        }

        // FFA waiting message before timer has started
        if (!timerStarted.Value && rm != null && !rm.IsTeamsMode)
        {
            if (announcementText != null)
                announcementText.text = waitingForPlayersMessage;
        }
        else
        {
            if (announcementText != null && announcementText.text == waitingForPlayersMessage)
                announcementText.text = "";
        }
        UpdatePointsToWinMessage();
    }

    private void UpdateTimerTextImmediate()
    {
        var rm = RoomManagerLan.Instance;
        if (rm == null || !rm.UsesTimer)
        {
            if (matchTimeText != null)
                matchTimeText.text = "";

            return;
        }

        int remainingSeconds;

        if (!timerStarted.Value)
        {
            remainingSeconds = cachedDurationSeconds.Value;
        }
        else
        {
            double elapsed = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.ServerTime.Time - matchStartServerTime.Value
                : 0d;

            remainingSeconds = Mathf.Max(0, cachedDurationSeconds.Value - Mathf.FloorToInt((float)elapsed));
        }

        if (matchTimeText != null)
            matchTimeText.text = FormatTime(remainingSeconds);
    }

    private string FormatTime(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        // If there is less than 10 minutes, only show one digit for the minutes.
        return string.Format("{0}:{1:00}", minutes, seconds);
    }

    public void Server_StartTimer()
    {
        if (!IsServer) return;

        var rm = RoomManagerLan.Instance;
        if (rm == null || !rm.UsesTimer) return;
        if (timerStarted.Value) return;

        cachedDurationSeconds.Value = rm.MatchLengthSeconds;
        matchStartServerTime.Value = NetworkManager.Singleton.ServerTime.Time;
        timerStarted.Value = true;

        Debug.Log($"[MatchTimerLAN] Timer started. Duration={cachedDurationSeconds.Value}s StartTime={matchStartServerTime.Value}");
    }

    public int GetRemainingSeconds()
    {
        var rm = RoomManagerLan.Instance;
        if (rm == null || !rm.UsesTimer)
            return 0;

        if (!timerStarted.Value)
            return cachedDurationSeconds.Value;

        double elapsed = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ServerTime.Time - matchStartServerTime.Value
            : 0d;

        return Mathf.Max(0, cachedDurationSeconds.Value - Mathf.FloorToInt((float)elapsed));
    }
    
    //text mesh pro in top corner
    private void UpdatePointsToWinMessage()
    {
        var rm = RoomManagerLan.Instance;
        bool usesPoints = rm != null && rm.UsesPoints;

        if (pointsToWinText == null)
            return;

        pointsToWinText.gameObject.SetActive(usesPoints);

        if (!usesPoints)
        {
            pointsToWinText.text = "";
            return;
        }

        pointsToWinText.text = $"First to {rm.TargetPointsToWin} points wins";
    }
}