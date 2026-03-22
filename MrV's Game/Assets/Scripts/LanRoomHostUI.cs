using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class LanRoomHostUI : MonoBehaviour
{
    [Header("Map Selection")]
    public TMP_Dropdown mapDropdown;
    public Image mapPreviewImage;
    public List<Sprite> mapPreviewSprites; // must match mapSceneNames order

    [Header("Room Settings")]
    public TMP_InputField roomNameInputField;
    public TMP_Dropdown questionSetDropdown;

    [Header("Game Mode Toggles")]
    public Toggle ffaToggle;
    public Toggle teamsToggle;
    public Toggle ctfToggle;
    public GameObject ctfToggleRow; // assign the whole CTF row here

    [Header("CTF Supported Maps")]
    public List<string> ctfSupportedMaps = new List<string>();

    [Header("Buttons / Text")]
    public Button hostButton;
    public TMP_Text warningText;

    // map names must match scene names exactly
    // Drag your map thumbnail sprites into the mapPreviewSprites
    // list in the exact same order as:

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

    // Cache of available sets so dropdown value maps correctly. Type name into inspector exactly as the scene is named
    // only add maps that have Ctf
    private List<string> _availableQuestionSets = new List<string>();

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostClicked);

        if (mapDropdown != null)
            mapDropdown.onValueChanged.AddListener(OnMapDropdownChanged);

        warningText.text = "";

        SetupMapDropdown();
        RefreshQuestionSetsDropdown();
        SetupDefaultGameMode();
        UpdateMapPreview();
        UpdateAvailableGameModes();
    }

    private void OnEnable()
    {
        if (warningText != null)
            warningText.text = "";

        RefreshQuestionSetsDropdown();
        UpdateMapPreview();
        UpdateAvailableGameModes();
    }

    private void SetupMapDropdown()
    {
        if (mapDropdown == null) return;

        mapDropdown.ClearOptions();
        mapDropdown.AddOptions(mapSceneNames);
        mapDropdown.RefreshShownValue();
    }

    private void OnMapDropdownChanged(int index)
    {
        UpdateMapPreview();
        UpdateAvailableGameModes();
    }

    private void UpdateMapPreview()
    {
        if (mapPreviewImage == null) return;
        if (mapPreviewSprites == null || mapPreviewSprites.Count == 0) return;

        int index = 0;

        if (mapDropdown != null)
            index = Mathf.Clamp(mapDropdown.value, 0, mapPreviewSprites.Count - 1);

        if (index < mapPreviewSprites.Count && mapPreviewSprites[index] != null)
        {
            mapPreviewImage.sprite = mapPreviewSprites[index];
            mapPreviewImage.enabled = true;
        }
        else
        {
            mapPreviewImage.enabled = false;
        }
    }

    private void SetupDefaultGameMode()
    {
        if (ffaToggle != null && teamsToggle != null && ctfToggle != null)
        {
            if (!ffaToggle.isOn && !teamsToggle.isOn && !ctfToggle.isOn)
            {
                ffaToggle.isOn = true;
            }
        }
    }

    private void UpdateAvailableGameModes()
    {
        bool supportsCTF = SelectedMapSupportsCTF();

        if (ctfToggleRow != null)
            ctfToggleRow.SetActive(supportsCTF);

        // If CTF is currently selected, but this map does not support it,
        // force selection back to FFA
        if (!supportsCTF && ctfToggle != null && ctfToggle.isOn)
        {
            if (ffaToggle != null)
                ffaToggle.isOn = true;
        }
    }

    private bool SelectedMapSupportsCTF()
    {
        if (mapDropdown == null) return false;
        if (ctfSupportedMaps == null || ctfSupportedMaps.Count == 0) return false;

        string selectedScene = mapSceneNames[Mathf.Clamp(mapDropdown.value, 0, mapSceneNames.Count - 1)];
        return ctfSupportedMaps.Contains(selectedScene);
    }

    private int GetSelectedGameMode()
    {
        if (teamsToggle != null && teamsToggle.isOn)
            return 1; // Teams

        if (ctfToggle != null && ctfToggle.isOn)
            return 2; // CTF

        return 0; // FFA default
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
        string root = QuestionSetStorage.GetQuestionSetsRoot();

        if (!Directory.Exists(root))
            return new List<string>();

        // Each subfolder is a question set name
        var dirs = Directory.GetDirectories(root);

        // folder name only
        var names = dirs
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n) && !n.StartsWith("."))
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
        PlayerPrefs.SetInt("LAN_IsHost", 1);
        PlayerPrefs.DeleteKey("JoinLAN_IP");
        

        // store selected game mode (0 = FFA, 1 = Teams, 2 = CTF)
        int selectedMode = GetSelectedGameMode();
        PlayerPrefs.SetInt("LAN_GameMode", selectedMode);
        PlayerPrefs.Save();
        
        // Only load scene — do not start host yet
        string selectedScene = mapSceneNames[mapDropdown.value];
        SceneManager.LoadScene(selectedScene);
    }
}