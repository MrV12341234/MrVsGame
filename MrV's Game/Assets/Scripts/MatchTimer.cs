using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.Events;

public class MatchTimer : MonoBehaviourPunCallbacks
{
   // MasterClient ---> original player that created the room is called the "master client"
   // every 1second the roomhost will distribute the match time by RPC
   
   public float currentTime = 300;
   public TextMeshProUGUI matchTimeText;
   public UnityEvent onGameEndEvent; 
      
   IEnumerator Start()
   {
      yield return new WaitUntil (() => PhotonNetwork.InRoom);

      if (PhotonNetwork.LocalPlayer.IsMasterClient) // if the player who joined is the 1st person in the room
      {
         // start the timer reduce loop
         StartCoroutine(DecreaseTimer());
      }
   }

   IEnumerator DecreaseTimer()
   {
      currentTime -= 1;
      UpdateUI(); // calls update UI below to change the time
      
      photonView.RPC("RPC_SynchroniseTimer", RpcTarget.Others, currentTime);
      yield return new WaitForSeconds(1);
      StartCoroutine(DecreaseTimer());
   }

   private void UpdateUI() // show time
   {
      if (currentTime <= 0)
      {
         if (onGameEndEvent != null)
         {
            onGameEndEvent.Invoke();
         }
         matchTimeText.text = "Ended";

         return;
      }
      
      //converts the seconds into minutes and seconds 
      int minutes = Mathf.FloorToInt (currentTime / 60f);
      int seconds = Mathf.FloorToInt (currentTime % 60f);
      // format the text to MM:ss
      matchTimeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
   }

   public override void OnMasterClientSwitched(Player newMasterClient) // this is for when/if original master client leaves the room
   {
      base.OnMasterClientSwitched(newMasterClient);
      
      if (PhotonNetwork.LocalPlayer.UserId == newMasterClient.UserId) // if the player who joined is the 1st person in the room
      {
         // start the timer reduce loop
         StartCoroutine(DecreaseTimer());
      }
   }

   [PunRPC] // below syncs the timer over the network
   public void RPC_SynchroniseTimer(float _time)
   {
      currentTime = _time;
      UpdateUI();
   }
}
