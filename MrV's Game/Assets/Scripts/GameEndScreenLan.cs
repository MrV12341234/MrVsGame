using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[NetworkMode(NetworkMode.LAN)]
public class GameEndScreenLan : NetworkBehaviour
{
    public static GameEndScreenLan Instance;

    [Header("End Game UI")]
    public GameObject gameEndBackground;              // Assign: MatchTimerCanvas > Game End Holder > Game End BG
    public TextMeshProUGUI titleText;                // Assign: Title
    public TextMeshProUGUI subtitleText;             // Assign: Subtitle
    public TextMeshProUGUI winningPlayerNameText;    // Assign: Winning Player Name text
    public TextMeshProUGUI secondPlaceNameText;      // Assign: 2ndPlaceName
    public TextMeshProUGUI thirdPlaceNameText;       // Assign: 3rdPlaceName
    public TextMeshProUGUI returnToMenuText;         // Assign: ReturnToMenuText

    [Header("Auto Return")]
    public float autoReturnDelaySeconds = 300f;      // 5 minutes

    private bool isGameEnded = false;
    private bool isLeaving = false;
    private bool serverGameEndTriggered = false;
    private Coroutine autoLeaveRoutine;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (gameEndBackground != null)
            gameEndBackground.SetActive(false);
    }

    private void Update()
    {
        if (!isGameEnded) return;

        if (Input.GetKeyDown(KeyCode.X))
        {
            BeginLeaveToMenu();
        }
    }

        public void Server_ShowGameOverFromTimer()
    {
        if (!IsServer) return;
        if (serverGameEndTriggered) return;

        serverGameEndTriggered = true;
        Server_ShowGameOverInternal();
    }

    public void Server_ShowGameOverFromPoints()
    {
        if (!IsServer) return;
        if (serverGameEndTriggered) return;

        serverGameEndTriggered = true;
        Server_ShowGameOverInternal();
    }

    private void Server_ShowGameOverInternal()
    {
        var rm = RoomManagerLan.Instance;
        if (rm == null) return;

        string subtitle = "Winner:";
        string winnerText = "No Winner";
        string secondText = "";
        string thirdText = "";
        bool showPlacements = !rm.IsTeamsMode;
        int winnerColorCode = 0; // 0 = white, 1 = blue, 2 = red

        if (!rm.IsTeamsMode)
        {
            var lb = LeaderboardManagerLAN.Instance;
            List<PlayerScoreData> orderedRows = (lb != null)
                ? lb.Server_GetOrderedSnapshot()
                : new List<PlayerScoreData>();

            if (orderedRows.Count > 0)
                winnerText = orderedRows[0].name;

            if (orderedRows.Count > 1)
                secondText = "2nd Place: " + orderedRows[1].name;

            if (orderedRows.Count > 2)
                thirdText = "3rd Place: " + orderedRows[2].name;
        }
        else
        {
            var teamScores = TeamScoreManagerLan.Instance;
            int blueScore = (teamScores != null) ? teamScores.GetBlueScore() : 0;
            int redScore = (teamScores != null) ? teamScores.GetRedScore() : 0;

            if (blueScore > redScore)
            {
                subtitle = "Winning Team:";
                winnerText = "BLUE TEAM";
                winnerColorCode = 1;
            }
            else if (redScore > blueScore)
            {
                subtitle = "Winning Team:";
                winnerText = "RED TEAM";
                winnerColorCode = 2;
            }
            else
            {
                subtitle = "Result:";
                winnerText = "TIE GAME";
                winnerColorCode = 0;
            }
        }

        ShowGameEndClientRpc(subtitle, winnerText, secondText, thirdText, showPlacements, winnerColorCode);
    }

    [ClientRpc]
    private void ShowGameEndClientRpc(string subtitle, string winnerText, string secondText, string thirdText, bool showPlacements, int winnerColorCode)
    {
        ShowGameEndLocally(subtitle, winnerText, secondText, thirdText, showPlacements, winnerColorCode);
    }

    private void ShowGameEndLocally(string subtitle, string winnerText, string secondText, string thirdText, bool showPlacements, int winnerColorCode)
    {
        // Hide other UI that could still be open
        if (RoomManagerLan.Instance != null)
        {
            if (RoomManagerLan.Instance.Quiz != null)
                RoomManagerLan.Instance.Quiz.SetActive(false);

            if (RoomManagerLan.Instance.teamLobbyUI != null)
                RoomManagerLan.Instance.teamLobbyUI.HideLobby();
        }

        if (titleText != null)
            titleText.text = "GAME OVER";

        if (subtitleText != null)
            subtitleText.text = subtitle;

        if (winningPlayerNameText != null)
        {
            winningPlayerNameText.text = winnerText;
            winningPlayerNameText.color = GetWinnerColor(winnerColorCode);
        }

        if (secondPlaceNameText != null)
        {
            secondPlaceNameText.gameObject.SetActive(showPlacements);
            secondPlaceNameText.text = showPlacements ? secondText : "";
        }

        if (thirdPlaceNameText != null)
        {
            thirdPlaceNameText.gameObject.SetActive(showPlacements);
            thirdPlaceNameText.text = showPlacements ? thirdText : "";
        }

        if (returnToMenuText != null)
            returnToMenuText.text = "Press X to return to menu";

        if (gameEndBackground != null)
            gameEndBackground.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        isGameEnded = true;

        if (autoLeaveRoutine != null)
            StopCoroutine(autoLeaveRoutine);

        autoLeaveRoutine = StartCoroutine(AutoLeaveAfterDelay());
    }

    private Color GetWinnerColor(int code)
    {
        switch (code)
        {
            case 1:
                return Color.blue;
            case 2:
                return Color.red;
            default:
                return Color.white;
        }
    }

    private void BeginLeaveToMenu()
    {
        if (isLeaving) return;

        isLeaving = true;
        StartCoroutine(LeaveToMenuRoutine());
    }

    private IEnumerator LeaveToMenuRoutine()
    {
        var pause = FindObjectOfType<PauseMenuManager>();
        if (pause != null)
        {
            pause.LeaveGame();
            yield break;
        }

        // Fallback in case PauseMenuManager is missing
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            yield return null;
        }

        SceneManager.LoadScene(0);
    }

    private IEnumerator AutoLeaveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(autoReturnDelaySeconds);

        if (isGameEnded)
        {
            BeginLeaveToMenu();
        }
    }
}