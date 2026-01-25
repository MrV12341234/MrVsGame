using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// this script is for the gamertag that is displayed in the fp camera view, bottom left. Not the one above each players head

public class GamertagDisplayUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text nameText;
    
    [Tooltip("second TMP text that shows 'Host' for the room creator.")]
    [SerializeField] private TMP_Text hostLabelText;

    // tweak in Inspector if you want
    [SerializeField] private Color blueTeamBackground = new Color(0.10f, 0.25f, 0.55f, 1f);
    [SerializeField] private Color redTeamBackground  = new Color(0.55f, 0.08f, 0.08f, 1f);

    [Header("Update")]
    [Tooltip("How often to refresh UI (seconds). 0.2 = 5x/sec")]
    [SerializeField] private float refreshInterval = 0.2f;

    private PlayerSetupLan _setup;
    private string _lastName;
    private RoomManagerLan.TeamId _lastTeam;
    private bool _lastTeamsMode;
    private bool _lastIsHost;
    
    // cache the FFA color that you set in the Inspector on the Image
    private Color _ffaBackgroundFromInspector = Color.black;

    private void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>(true);
        
        // Cache the inspector color (what you set on the Image component)
        if (backgroundImage != null)
            _ffaBackgroundFromInspector = backgroundImage.color;

        // text should always be white
        if (nameText != null)
            nameText.color = Color.white;
        
        if (hostLabelText != null)
            hostLabelText.color = Color.green;
    }

    private void OnEnable()
    {
        CancelInvoke(nameof(Refresh));
        InvokeRepeating(nameof(Refresh), 0f, refreshInterval);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(Refresh));
    }

    private void Refresh()
    {
        if (_setup == null)
        {
            // GamerTagHolder is under FP_Camera, so PlayerSetupLan should be up the hierarchy
            _setup = GetComponentInParent<PlayerSetupLan>(true);
            if (_setup == null) return; // not ready yet
        }

        // Only care about local player HUD.
        // FP camera is only active for owner, but this keeps it extra safe.
        if (!_setup.IsOwner) return;

        // Get gamemode (prefer RoomManager; fallback to PlayerPrefs)
        bool teamsMode = false;
        if (RoomManagerLan.Instance != null)
            teamsMode = RoomManagerLan.Instance.IsTeamsMode;
        else
            teamsMode = (PlayerPrefs.GetInt("LAN_GameMode", 0) == 1);

        string playerName = _setup.GetPlayerNameString();
        var team = _setup.GetTeam();
        
        // Host detection: only the room creator machine has IsHost == true
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        // Only update UI if something changed (cheap + avoids spam)
        if (playerName == _lastName && team == _lastTeam && teamsMode == _lastTeamsMode)
            return;

        _lastName = playerName;
        _lastTeam = team;
        _lastTeamsMode = teamsMode;
        _lastIsHost = isHost;

        // Update Gamertag name text
        if (nameText != null)
            nameText.text = playerName;
        
        // Update "Host" label
        if (hostLabelText != null)
            hostLabelText.text = isHost ? "Host" : "";

        // Update background
        if (backgroundImage != null)
        {
            if (!teamsMode)
            {
                // FFA uses whatever color you set in the Inspector on the Image
                backgroundImage.color = _ffaBackgroundFromInspector;
            }
            else
            {
                backgroundImage.color = (team == RoomManagerLan.TeamId.Blue)
                    ? blueTeamBackground
                    : redTeamBackground;
            }
        }
    }
}
