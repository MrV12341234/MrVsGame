using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class LanRoomHostUI : MonoBehaviour
{
    public TMP_Dropdown mapDropdown;
    public TMP_InputField roomNameInputField;
    public TMP_Dropdown gameModeDropdown;

    // NEW: Question set dropdown (host)
    public TMP_Dropdown questionSetDropdown;

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
        "Teams",
        "CTF"
    };

    // Cache of available sets so dropdown value maps correctly
    private List<string> _availableQuestionSets = new List<string>();

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        warningText.text = "";

        SetupMapDropdown();
        SetupGameModeDropdown();
        RefreshQuestionSetsDropdown();
    }

    private void SetupMapDropdown()
    {
        mapDropdown.ClearOptions();
        mapDropdown.AddOptions(mapSceneNames);
        mapDropdown.RefreshShownValue();
    }

    private void SetupGameModeDropdown()
    {
        if (gameModeDropdown == null) return;

        gameModeDropdown.ClearOptions();
        gameModeDropdown.AddOptions(gameModeOptions);
        gameModeDropdown.RefreshShownValue();
    }

    private void RefreshQuestionSetsDropdown()
    {
        if (questionSetDropdown == null) return;

        _availableQuestionSets = GetQuestionSetFolderNames();

        questionSetDropdown.ClearOptions();
        questionSetDropdown.AddOptions(_availableQuestionSets);
        questionSetDropdown.value = 0;
        questionSetDropdown.RefreshShownValue();
    }

    private List<string> GetQuestionSetFolderNames()
    {
        // This also ensures QuestionSets root exists (your storage does CreateDirectory)
        string root = QuestionSetStorage.GetQuestionSetsRoot();

        if (!Directory.Exists(root))
            return new List<string>();

        // Each subfolder is a question set name
        var dirs = Directory.GetDirectories(root);

        // folder name only
        var names = dirs
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n) && !n.StartsWith(".")) // ignore hidden-ish
            .OrderBy(n => n)
            .ToList();

        return names;
    }

    private void OnHostClicked()
    {
        warningText.text = "";

        string roomName = roomNameInputField.text.Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            warningText.text = "Please enter a LAN room name.";
            return;
        }

        // Ensure we have at least one question set folder
        
        if (_availableQuestionSets == null || _availableQuestionSets.Count == 0)
        {
            warningText.text = "There are no question sets in the QuestionSets folder.";
            return;
        }

        // Save host's selected set for gameplay
        string chosenSet = _availableQuestionSets[Mathf.Clamp(questionSetDropdown.value, 0, _availableQuestionSets.Count - 1)];
        PlayerPrefs.SetString("SelectedQuestionSet", chosenSet);

        GameMode.IsLAN = true;
        PlayerPrefs.SetString("LAN_RoomName", roomName);
        PlayerPrefs.SetInt("LAN_IsHost", 1); // this player is the host
        PlayerPrefs.DeleteKey("JoinLAN_IP"); 

        // store selected game mode (0 = FFA, 1 = Teams, 2 = CTF)
        int selectedMode = (gameModeDropdown != null) ? gameModeDropdown.value : 0;
        PlayerPrefs.SetInt("LAN_GameMode", selectedMode);
        PlayerPrefs.Save();

        // Only load scene — do not start host yet
        string selectedScene = mapSceneNames[mapDropdown.value];
        SceneManager.LoadScene(selectedScene);
    }
}