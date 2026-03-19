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
        StartCoroutine(LeaveGameRoutine());
    }

    private IEnumerator LeaveGameRoutine()
    {
        IsGamePaused = false;

        // --- FULL LAN SHUTDOWN / RESET ---
        if (GameMode.IsLAN && Unity.Netcode.NetworkManager.Singleton != null)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;

            if (nm.IsListening || nm.IsClient || nm.IsServer || nm.IsHost)
            {
                nm.Shutdown();
                Debug.Log("[LAN] NetworkManager shutdown requested.");
            }

            // Wait a short moment for NGO to fully reset
            float timeout = 2f;
            while (timeout > 0f && (nm.IsListening || nm.IsClient || nm.IsServer || nm.IsHost))
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            // Clear leftover LAN session state
            GameMode.IsLAN = false;
            PlayerPrefs.SetInt("LAN_IsHost", 0);
            PlayerPrefs.DeleteKey("JoinLAN_IP");
            PlayerPrefs.DeleteKey("LAN_RoomName");
            PlayerPrefs.Save();

            Debug.Log("[LAN] LAN session state reset complete.");
        }

        // --- EXISTING CLEANUP for Photon ---
        yield return StartCoroutine(LeaderboardManager.ResetPlayerStatsAndWait());
        Debug.Log("Stats reset complete. Now leaving room.");

        if (PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
                yield return new WaitUntil(() => !PhotonNetwork.InRoom);
            }

            PhotonNetwork.Disconnect();
            yield return new WaitUntil(() => !PhotonNetwork.IsConnected);
        }

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