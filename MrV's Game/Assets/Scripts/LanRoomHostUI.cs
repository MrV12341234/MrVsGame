using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class LanRoomHostUI : MonoBehaviour
{
    public TMP_InputField roomNameInputField;
    public Button hostButton;
    public TMP_Text warningText;
    public TMP_Dropdown mapDropdown;

    // must match scene name exactly (name in build profiles). This is the drop down map names in the menu
    private List<string> mapSceneNames = new List<string>
    {
        "Cartoon City",
        "Sky Arena",
        "Desert Storm",
        "Industry Baby",
        "Dust 2"
    };

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        warningText.text = "";
        SetupMapDropdown();
    }
    private void SetupMapDropdown()
    {
        mapDropdown.ClearOptions();
        mapDropdown.AddOptions(mapSceneNames);
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

        // Only load scene — do not start host yet
        string selectedScene = mapSceneNames[mapDropdown.value];
        SceneManager.LoadScene(selectedScene);
    }
}