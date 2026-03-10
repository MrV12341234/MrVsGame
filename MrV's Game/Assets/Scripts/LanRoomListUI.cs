using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;

public class LanRoomListUI : MonoBehaviour
{
    public GameObject roomEntryPrefab;
    public Transform roomListParent;
    public Button refreshButton;
    public Button joinRoomButton;
    public TMP_Text warningText;

    // Question set dropdown (client)
    public TMP_Dropdown questionSetDropdown;

    private LanDiscovery lanDiscovery;
    private LanRoomInfo selectedRoom;
    private GameObject lastSelectedButton;

    private List<string> _availableQuestionSets = new List<string>();

    private void Start()
    {
        lanDiscovery = FindObjectOfType<LanDiscovery>();

        refreshButton.onClick.AddListener(RefreshRoomList);
        joinRoomButton.onClick.AddListener(OnJoinRoomClicked);

        warningText.text = "";
        RefreshRoomList();
        RefreshQuestionSetsDropdown();
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

        var dirs = Directory.GetDirectories(root);
        var names = dirs
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n) && !n.StartsWith("."))
            .OrderBy(n => n)
            .ToList();

        return names;
    }

    private void RefreshRoomList()
    {
        ClearRoomList();
        selectedRoom = null;
        warningText.text = "";

        if (lanDiscovery == null)
        {
            warningText.text = "LAN discovery not found.";
            return;
        }

        // Optional: refresh sets when refreshing rooms (handy if teacher added folders)
        RefreshQuestionSetsDropdown();

        // Use a HashSet to prevent duplicates
        HashSet<string> seenRooms = new HashSet<string>();

        foreach (LanRoomInfo room in lanDiscovery.discoveredRooms)
        {
            // Combine IP and room name to uniquely identify a room
            string roomKey = room.ipAddress + "_" + room.roomName;

            if (seenRooms.Contains(roomKey))
                continue; // Skip duplicates

            seenRooms.Add(roomKey); // Track this room as added

            GameObject entry = Instantiate(roomEntryPrefab, roomListParent);

            TMP_Text[] texts = entry.GetComponentsInChildren<TMP_Text>();
            if (texts.Length >= 2)
            {
                texts[0].text = room.roomName;
                texts[1].text = $"{room.playerCount}/16";
            }

            Button button = entry.GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                selectedRoom = room;

                if (lastSelectedButton != null)
                    lastSelectedButton.GetComponent<Image>().color = Color.white;

                entry.GetComponent<Image>().color = Color.yellow;
                lastSelectedButton = entry;
            });
        }
    }

    private void ClearRoomList()
    {
        foreach (Transform child in roomListParent)
        {
            Destroy(child.gameObject);
        }

        lastSelectedButton = null;
    }

    private void OnJoinRoomClicked()
    {
        warningText.text = "";

        if (selectedRoom == null)
        {
            warningText.text = "Please select a room first.";
            return;
        }

        // Ensure we have at least one question set folder
        
        if (_availableQuestionSets == null || _availableQuestionSets.Count == 0)
        {
            warningText.text = "There are no question sets in the QuestionSets folder.";
            return;
        }

        // Save client's selected set (each client can choose differently)
        if (questionSetDropdown != null)
        {
            string chosenSet = _availableQuestionSets[Mathf.Clamp(questionSetDropdown.value, 0, _availableQuestionSets.Count - 1)];
            PlayerPrefs.SetString("SelectedQuestionSet", chosenSet);
        }
        else
        {
            // Fallback if dropdown not assigned
            PlayerPrefs.SetString("SelectedQuestionSet", _availableQuestionSets[0]);
        }

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("[LAN JOIN] Cannot join — already connected or running.");
            warningText.text = "Already connected!";
            return;
        }

        // Set IP for Unity Transport
        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        transport.ConnectionData.Address = selectedRoom.ipAddress;
        transport.ConnectionData.Port = 7777;

        GameMode.IsLAN = true;

        Debug.Log($"[LAN JOIN] Loading scene before connecting to host at {selectedRoom.ipAddress}");

        // Store the IP to use after the scene loads
        PlayerPrefs.SetString("JoinLAN_IP", selectedRoom.ipAddress);
        PlayerPrefs.SetInt("LAN_IsHost", 0); // this player is a client

        // stores gamemode
        PlayerPrefs.SetInt("LAN_GameMode", selectedRoom.gameMode);

        // Load the map scene
        SceneManager.LoadScene(selectedRoom.sceneName);
    }
}