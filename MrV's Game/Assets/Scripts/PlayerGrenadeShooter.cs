using Unity.Netcode;
using UnityEngine;

// attached to the player prefab. Used in LAN mode to spawn grenades, both thrown and launhed from the potato launcher

public class PlayerGrenadeShooter : NetworkBehaviour
{
    [Header("Grenade Prefabs")]
    public GameObject launchedGrenadePrefab;  // For potato launcher
    public GameObject thrownGrenadePrefab;    // For hand grenade

    [ServerRpc]
    public void ShootGrenadeServerRpc(Vector3 position, Quaternion rotation)
    {
        if (launchedGrenadePrefab == null)
        {
            Debug.LogError("Launched grenade prefab not assigned!");
            return;
        }

        GameObject grenade = Instantiate(launchedGrenadePrefab, position, rotation);
        NetworkObject grenadeNetworkObject = grenade.GetComponent<NetworkObject>();
        grenadeNetworkObject.SpawnWithOwnership(OwnerClientId);
        
        // Set the player reference for damage attribution
        LauncherProjectileLAN projectile = grenade.GetComponent<LauncherProjectileLAN>();
        if (projectile != null)
        {
            projectile.SetOwner(OwnerClientId, gameObject);
        }
    }

    [ServerRpc]
    public void ThrowGrenadeServerRpc(Vector3 position, Quaternion rotation, float throwForce)
    {
        if (thrownGrenadePrefab == null)
        {
            Debug.LogError("Thrown grenade prefab not assigned!");
            return;
        }

        GameObject grenade = Instantiate(thrownGrenadePrefab, position, rotation);
        NetworkObject grenadeNetworkObject = grenade.GetComponent<NetworkObject>();
        grenadeNetworkObject.SpawnWithOwnership(OwnerClientId);
        
        // Set the throw force and player reference
        ThrownProjectileLAN projectile = grenade.GetComponent<ThrownProjectileLAN>();
        if (projectile != null)
        {
            projectile.SetOwner(OwnerClientId, gameObject);
            projectile.SetThrowForce(throwForce);
            // REMOVED: SetLifetime call
        }
    }
}