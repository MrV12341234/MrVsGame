using System;
using System.Collections;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.SceneManagement;

public class FFAGameEndScreen : MonoBehaviourPun
{
    public GameObject gameEndScreenHolder;
    public TextMeshProUGUI winningPlayerNameText;

    private bool isGameEnded = false;

    public void ShowGameEndScreen()
    {
        winningPlayerNameText.text = LeaderboardManager.WinningPlayerName;
        gameEndScreenHolder.SetActive(true);

        isGameEnded = true;

        // Start the automatic return countdown
        StartCoroutine(AutoLeaveAfterDelay());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.X) && isGameEnded)
        {
            StartCoroutine(LeaveAndReturn());
        }
    }

    
    IEnumerator LeaveAndReturn()
    {
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


    IEnumerator AutoLeaveAfterDelay()
    {
        yield return new WaitForSeconds(30f);

        if (PhotonNetwork.InRoom)
        {
            StartCoroutine(LeaveAndReturn());
        }
    }
}