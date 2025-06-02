using System;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LocalPlayerKDManager : MonoBehaviour
{
    public static LocalPlayerKDManager Instance;


    private void Awake()
    {
        Instance = this;
    }

    public int localPlayer_kills;
    public int localPlayer_deaths;

    public void GetKill()
    {
        localPlayer_kills++;
        
        SetHashes();
    }

    public void OnDied()
    {
        localPlayer_deaths++;
        
        SetHashes();
    }

    private void SetHashes()
    {
        try
        {
            Hashtable hash = new Hashtable
            {
                ["kills"] = localPlayer_kills,
                ["deaths"] = localPlayer_deaths
            };

            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
        }
        catch (Exception)
        {
            // Do nothing
        }
    }
    
}
