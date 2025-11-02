using Unity.Netcode;
using UnityEngine;

public class PlayerGrenadeShooter : NetworkBehaviour
{
    [Header("Grenade Prefab")]
    public GameObject grenadePrefab;

    [ServerRpc]
    public void ShootGrenadeServerRpc(Vector3 position, Quaternion rotation)
    {
        GameObject grenade = Instantiate(grenadePrefab, position, rotation);
        NetworkObject grenadeNetworkObject = grenade.GetComponent<NetworkObject>();
        grenadeNetworkObject.SpawnWithOwnership(OwnerClientId);
        
        // Set the player reference for damage attribution
        LauncherProjectileLAN projectile = grenade.GetComponent<LauncherProjectileLAN>();
        if (projectile != null)
        {
            projectile.SetOwner(OwnerClientId, gameObject);
        }
    }
}