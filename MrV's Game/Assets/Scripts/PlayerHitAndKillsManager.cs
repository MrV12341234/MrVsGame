using Photon.Pun;
using Photon.Pun.UtilityScripts;
using UnityEngine;

public class PlayerHitAndKillsManager : MonoBehaviour
{
    [Header("UI")] 
    public Animation hitMarkerAnimation;
    public AudioSource hitMarkerAudioSource;
    [Space]
    public Animation killMarkerAnimation;
    public AudioSource killMarkerAudioSource;

    public void GetHit(int _damage) // the old code had the player score added up based on the amount of _damage the selected gun did. So if the water gun takes 25 damage per hit, each hit would give 25 points to the players score.
    {
        hitMarkerAnimation.Stop();
        hitMarkerAnimation.Play();
        
        hitMarkerAudioSource.Stop();
        hitMarkerAudioSource.Play();
        // add points for each hit on another player
        PhotonNetwork.LocalPlayer.AddScore(2); // where there is a "2" here, there used to be "_damage" but i want it to be a single number for each hit and not the damage of weapon
        
    }
    
    
    public void GetKill(string _victimName)
    {
        killMarkerAnimation.Stop();
        killMarkerAnimation.Play();
        
        killMarkerAudioSource.Stop();
        killMarkerAudioSource.Play();
        // add points for each kill on another player
        PhotonNetwork.LocalPlayer.AddScore(5);
        
        LocalPlayerKDManager.Instance.GetKill();
        
        KillfeedManager.Instance.photonView.RPC("RPC_GetKill", RpcTarget.All, PhotonNetwork.LocalPlayer.NickName, _victimName);
    }
}
