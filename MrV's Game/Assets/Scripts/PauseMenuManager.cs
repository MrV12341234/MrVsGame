using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using System.Collections;

public class PauseMenuManager : MonoBehaviour
{
    public GameObject pauseMenu;
    public static bool IsGamePaused { get; private set; }

    void Start()
    {
        IsGamePaused = false;
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            pauseMenu.SetActive(!pauseMenu.activeInHierarchy);

            bool isPauseMenuActive = pauseMenu.activeInHierarchy;
            Cursor.visible = isPauseMenuActive;
            Cursor.lockState = isPauseMenuActive ? CursorLockMode.None : CursorLockMode.Locked;

            IsGamePaused = isPauseMenuActive;
        }
    }

    public void LeaveGame()
    {
        // Shut down LAN session if running
        if (GameMode.IsLAN && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
            Debug.Log("[LAN] NetworkManager shut down.");
        }

        StartCoroutine(DelayedLoadMenuScene());
    }

    IEnumerator DelayedLoadMenuScene()
    {
        IsGamePaused = false;

        yield return StartCoroutine(LeaderboardManager.ResetPlayerStatsAndWait());
        {
            Debug.Log("Stats reset complete. Now leaving room.");
        }

        PhotonNetwork.LeaveRoom();
        yield return new WaitUntil(() => !PhotonNetwork.InRoom);

        PhotonNetwork.Disconnect();
        yield return new WaitUntil(() => !PhotonNetwork.IsConnected);

        SceneManager.LoadScene(0);
    }


    public void ClosePauseMenu()
    {
        pauseMenu.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        IsGamePaused = false;
    }
}