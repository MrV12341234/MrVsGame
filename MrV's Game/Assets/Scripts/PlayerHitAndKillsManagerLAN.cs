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

        if (hitMarkerAnimation) { hitMarkerAnimation.Stop(); hitMarkerAnimation.Play(); }
        if (hitMarkerAudioSource) { hitMarkerAudioSource.Stop(); hitMarkerAudioSource.Play(); }

        // Scoring is awarded server-side by the weapon/damage code (grenade/bullets).
        // If you have any client-only hit sources, you can uncomment this:
        // LeaderboardManagerLAN.Instance?.ReportHitServerRpc(OwnerClientId);
    }
    
    public void GetKill(string _victimName)
    {
        if (!IsOwner) return;
        
        killMarkerAnimation.Stop();
        killMarkerAnimation.Play();
        
        killMarkerAudioSource.Stop();
        killMarkerAudioSource.Play();
        
        // Awarded on the server when the victim actually dies. This local call is just FX.
        LocalPlayerKDManagerLAN.Instance?.GetKill();
    }
    
    private string GetPlayerName(ulong clientId)
    {
        if (RoomManagerLan.Instance != null)
            return RoomManagerLan.Instance.GetStoredPlayerName(clientId);

        return $"Player_{clientId}";
    }
}