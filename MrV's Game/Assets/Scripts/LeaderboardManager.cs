using System;
using System.Collections;
using UnityEngine;
using System.Linq;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using TMPro;

public class LeaderboardManager : MonoBehaviour
{
    public static string WinningPlayerName = "";
    
    public GameObject leaderboardUI;
    [Space] public Transform playerItemPrefabParent;
    public GameObject playerItemPrefab;

    private void Start()
    {
        InvokeRepeating(nameof(UpdateLeaderboard), 0.5f, 0.5f);
    }

    void Update()
    {
        leaderboardUI.SetActive(Input.GetKey(KeyCode.Tab));
    }


    void UpdateLeaderboard()
    {
        for (int i = playerItemPrefabParent.childCount - 1; i >= 0; i--)
        {
            Destroy(playerItemPrefabParent.GetChild(i).gameObject);
        }
        var sortedPlayerList =
            (from player in PhotonNetwork.PlayerList orderby player.GetScore() descending select player).ToList();
        
        if (sortedPlayerList.Count>0) WinningPlayerName = sortedPlayerList[0].NickName;

        foreach (var _player in sortedPlayerList)
        {
            GameObject playerItem = Instantiate(playerItemPrefab, playerItemPrefabParent);

            string _nickname = _player.NickName;

            bool _isMe = _player.UserId == PhotonNetwork.LocalPlayer.UserId;

            if (_isMe) _nickname = "<color=#FFC200>" + _nickname + "</color>";
            
            playerItem.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text =_nickname;
            playerItem.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = _player.GetScore().ToString();

            int _kills = 0;
            int _deaths = 0;

            if (_player.CustomProperties["kills"] != null) _kills = (int)_player.CustomProperties["kills"];
            if (_player.CustomProperties["deaths"] != null) _deaths = (int)_player.CustomProperties["deaths"];
            
            playerItem.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = _kills + " / " + _deaths;



        }
    }
    
    public static IEnumerator ResetPlayerStatsAndWait()
    {
        if (PhotonNetwork.LocalPlayer == null)
            yield break;

        PhotonNetwork.LocalPlayer.SetScore(0);

        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            { "kills", 0 },
            { "deaths", 0 }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        yield return new WaitUntil(() =>
            PhotonNetwork.LocalPlayer.GetScore() == 0 &&
            PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("kills") &&
            PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("deaths") &&
            (int)PhotonNetwork.LocalPlayer.CustomProperties["kills"] == 0 &&
            (int)PhotonNetwork.LocalPlayer.CustomProperties["deaths"] == 0
        );

        Debug.Log("Score After Reset: " + PhotonNetwork.LocalPlayer.GetScore());
    }
}
