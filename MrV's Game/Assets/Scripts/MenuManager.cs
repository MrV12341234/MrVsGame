using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviourPunCallbacks
{
    public enum GameMode { Online, LAN }
    public GameMode currentGameMode = GameMode.Online;

    [Header("Common UI")]
    public Button playOnlineButton;
    public Button playLanButton;
    public GameObject onlineUI;
    public GameObject lanUI;

    [Header("Online Room UI")]
    public List<string> mapRoomCodes;
    public List<TextMeshProUGUI> mapPlayerCountTexts;
    
    // we add buttons here for the Mapholder ui so we can ensure they are not interactable until the game connects to photon. See below
    public Button CartoonCity;
    public Button DesertStorm;
    public Button SkyArena;
    public Button IndustryBaby;
    public Button Dust2;

    private List<RoomInfo> currentRoomInfoList = new List<RoomInfo>();
    private bool hasSetRoomInfo;
    private bool isConnected;

    private void Start()
    {
        if (currentGameMode == GameMode.Online)
        {
            ConnectToPhoton();
        }
        else
        {
            ShowLANUI();
        }
    }

    public void ConnectToPhoton()
    {
        Debug.Log("Connecting to Photon...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public void SetGameModeOnline()
    {
        currentGameMode = GameMode.Online;
        ShowOnlineUI();
        ConnectToPhoton();
    }

    public void SetGameModeLAN()
    {
        currentGameMode = GameMode.LAN;
        ShowLANUI();
    }

    private void ShowOnlineUI()
    {
        onlineUI.SetActive(true);
        lanUI.SetActive(false);
    }

    private void ShowLANUI()
    {
        onlineUI.SetActive(false);
        lanUI.SetActive(true);
    }

    public void LoadSceneByIndex(int _index)
    {
        if (currentGameMode == GameMode.LAN)
        {
            // Load LAN scene directly (handled by LanNetworkManager)
            SceneManager.LoadScene(_index);
        }
        else
        {
            StartCoroutine(LoadSceneWhenConnected(_index));
        }
    }

    IEnumerator LoadSceneWhenConnected(int _index)
    {
        yield return new WaitUntil(() => isConnected);
        SceneManager.LoadScene(_index);
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Joining Photon lobby...");
        PhotonNetwork.JoinLobby();

        CartoonCity.interactable = true;
        DesertStorm.interactable = true;
        SkyArena.interactable = true;
        IndustryBaby.interactable = true;
        Dust2.interactable = true;
    }
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        base.OnRoomListUpdate(roomList);
        isConnected = true;

        if (!hasSetRoomInfo)
        {
            hasSetRoomInfo = true;
            currentRoomInfoList = roomList;
        }
        else
        {
            foreach (var roomInfo in roomList)
            {
                if (roomInfo.RemovedFromList)
                {
                    currentRoomInfoList.RemoveAll(r => r.Name == roomInfo.Name);
                }
                else
                {
                    currentRoomInfoList.RemoveAll(r => r.Name == roomInfo.Name);
                    currentRoomInfoList.Add(roomInfo);
                }
            }
        }

        UpdateOnlineUI();
    }

    private void UpdateOnlineUI()
    {
        foreach (var text in mapPlayerCountTexts)
        {
            text.text = "0";
        }

        foreach (var roomInfo in currentRoomInfoList)
        {
            for (int i = 0; i < mapRoomCodes.Count; i++)
            {
                if (roomInfo.Name == mapRoomCodes[i])
                {
                    mapPlayerCountTexts[i].text = roomInfo.PlayerCount.ToString();
                }
            }
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
