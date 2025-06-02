using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class LanRoomListUI : MonoBehaviour
{
    public GameObject roomEntryPrefab;
    public Transform roomListParent;
    public Button refreshButton;
    public Button joinRoomButton;
    public TMP_Text warningText;

    private LanDiscovery lanDiscovery;
    private LanRoomInfo selectedRoom;
    private GameObject lastSelectedButton;
    

    private void Start()
    {
        lanDiscovery = FindObjectOfType<LanDiscovery>();

        refreshButton.onClick.AddListener(RefreshRoomList);
        joinRoomButton.onClick.AddListener(OnJoinRoomClicked);

        warningText.text = "";
        RefreshRoomList();
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
        if (selectedRoom == null)
        {
            warningText.text = "Please select a room first.";
            return;
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

        // Load the map scene (you must know which scene to load here)
        SceneManager.LoadScene(selectedRoom.sceneName);
    }


    private System.Collections.IEnumerator DelayedJoin()
    {
        yield return new WaitForSeconds(0.1f); // small delay to avoid race condition after shutdown

        Debug.Log("[LAN JOIN] Starting client after delay...");
        bool success = NetworkManager.Singleton.StartClient();

        
        if (!success)
        {
            warningText.text = "Failed to join room.";
            Debug.LogWarning("[LAN JOIN] Client failed to start.");
        }
    }

}
