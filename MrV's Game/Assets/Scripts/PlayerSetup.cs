 using System;
using UnityEngine;
using Photon.Pun;
using TMPro;
using Unity.VisualScripting;
 
[NetworkMode(NetworkMode.PHOTON)]
public class PlayerSetup : MonoBehaviourPun
{
    public GameObject fpCamera;
    public Movement movement;
    public GameObject tpPlayer;
    [Space] 
    public TextMeshProUGUI nameText;

    private int hatSelected;

    [Header("Hat Set Up")] public Transform hatParent;

    private void Start()
    {
        Debug.Log("[PHOTON] PlayerSetup.cs is running!");
        fpCamera.SetActive(photonView.IsMine);
        movement.enabled = photonView.IsMine;
        
        tpPlayer.GameObject().SetActive(!photonView.IsMine);
        
        nameText.gameObject.SetActive(!photonView.IsMine); 
        
        nameText.text = photonView.Owner.NickName;

        if (photonView.IsMine)
        {
            // if you ever do the primary and secondary weapon tutorial, you'll added a bunch of code here. but i creted this if photon view for the hat selection
            hatSelected = PlayerPrefs.GetInt("hatSelected", 0);
            //keep this at the bottom of the if photonview function.
            photonView.RPC("RPC_SetTPHatSelected", RpcTarget.OthersBuffered, (byte)hatSelected);
        }
    }

    [PunRPC]
    public void RPC_SetTPHatSelected(byte _hatIndex)
    {
        for (int i = 0; i < hatParent.childCount; i++)
        {
            hatParent.GetChild(i).gameObject.SetActive(i == _hatIndex);
        }
    }
}
