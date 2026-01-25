using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class LanRoomHostUI : MonoBehaviour
{
     public TMP_Dropdown mapDropdown;
     public TMP_InputField roomNameInputField;
    public TMP_Dropdown gameModeDropdown;
    public Button hostButton;
    public TMP_Text warningText;
   

    // below map name must match scene name exactly (name in build profiles). This is the dropdown map names in the menu
    private List<string> mapSceneNames = new List<string>
    {
        "Cartoon City",
        "Uplift",
        "Heaven",
        "Paper City",
        "Disaster Town",
        "Sky Arena",
        "Industry Baby",
        "Dust 2",
        "Mirage",
        "Rainbow Road",
        "RDF",
        "Test Room"
    };
    
    private List<string> gameModeOptions = new List<string>
    {
        "FFA",
        "Teams"
    };

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        warningText.text = "";
        SetupMapDropdown();
        SetupGameModeDropdown();
    }
    private void SetupMapDropdown()
    {
        mapDropdown.ClearOptions();
        mapDropdown.AddOptions(mapSceneNames);
    }
    
    private void SetupGameModeDropdown()
    {
        if (gameModeDropdown == null) return;

        gameModeDropdown.ClearOptions();
        gameModeDropdown.AddOptions(gameModeOptions);

        // Optional: default to FFA
        // gameModeDropdown.value = 0;
        gameModeDropdown.RefreshShownValue();
    }

    private void OnHostClicked()
    {
        string roomName = roomNameInputField.text.Trim();

        if (string.IsNullOrEmpty(roomName))
        {
            warningText.text = "Please enter a LAN room name.";
            return;
        }

        GameMode.IsLAN = true;
        PlayerPrefs.SetString("LAN_RoomName", roomName);
        PlayerPrefs.SetInt("LAN_IsHost", 1); // this player is the host
        
        // store selected game mode (0 = FFA, 1 = Teams)
        int selectedMode = (gameModeDropdown != null) ? gameModeDropdown.value : 0;
        PlayerPrefs.SetInt("LAN_GameMode", selectedMode);

        // Only load scene — do not start host yet
        string selectedScene = mapSceneNames[mapDropdown.value];
        SceneManager.LoadScene(selectedScene);
    }
}