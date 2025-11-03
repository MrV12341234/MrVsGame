using Unity.Netcode;
using UnityEngine;

public class PlayerGrenadeShooter : NetworkBehaviour
{
    [Header("Grenade Prefabs")]
    public GameObject launchedGrenadePrefab;
    public GameObject thrownGrenadePrefab;

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
        
        ThrownProjectileLAN projectile = grenade.GetComponent<ThrownProjectileLAN>();
        if (projectile != null)
        {
            projectile.SetOwner(OwnerClientId, gameObject);
            projectile.SetThrowForce(throwForce);
        }
    }
}