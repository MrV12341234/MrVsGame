using UnityEngine;
using Unity.Netcode;

[NetworkMode(NetworkMode.LAN)]
public class PlayerHitAndKillsManagerLAN : NetworkBehaviour
{
    [Header("UI")] 
    public Animation hitMarkerAnimation;
    public AudioSource hitMarkerAudioSource;
    [Space]
    public Animation killMarkerAnimation;
    public AudioSource killMarkerAudioSource;

    public void GetHit(int _damage) 
    {
        if (!IsOwner) return;
        
        hitMarkerAnimation.Stop();
        hitMarkerAnimation.Play();
        
        hitMarkerAudioSource.Stop();
        hitMarkerAudioSource.Play();
        
        // TODO: Implement scoring system for LAN
        // Currently commented out as scoring isn't set up yet
        // NetworkScoringManagerLAN.Instance?.AddScore(OwnerClientId, 2);
    }
    
    public void GetKill(string _victimName)
    {
        if (!IsOwner) return;
        
        killMarkerAnimation.Stop();
        killMarkerAnimation.Play();
        
        killMarkerAudioSource.Stop();
        killMarkerAudioSource.Play();
        
        // TODO: Implement scoring system for LAN
        // NetworkScoringManagerLAN.Instance?.AddScore(OwnerClientId, 5);
        
        LocalPlayerKDManagerLAN.Instance?.GetKill();
        
        // Report kill to killfeed
        if (KillfeedManagerLAN.Instance != null)
        {
            string killerName = GetPlayerName(OwnerClientId);
            KillfeedManagerLAN.Instance.ReportKill(killerName, _victimName);
        }
    }
    
    private string GetPlayerName(ulong clientId)
    {
        // You might want to implement a proper player name resolution
        // This is a placeholder - you'll need to replace with your actual player name system
        return $"Player_{clientId}";
    }
}