using Unity.Netcode;
using UnityEngine;

public class PlayerGrenadeShooter : NetworkBehaviour
{
    [Header("Grenade Prefabs")]
    public GameObject launchedGrenadePrefab;
    public GameObject thrownGrenadePrefab;

    [Header("Claymore Prefab")]
    // Assign your ClaymoreMine prefab here in the inspector
    public GameObject claymorePrefab;

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
            // owner is the player GameObject that this component is on
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

    // NEW: Claymore planting RPC
    [ServerRpc]
    public void PlantClaymoreServerRpc(Vector3 position, Quaternion rotation)
    {
        if (claymorePrefab == null)
        {
            Debug.LogError("Claymore prefab not assigned!");
            return;
        }

        GameObject claymore = Instantiate(claymorePrefab, position, rotation);
        NetworkObject claymoreNetworkObject = claymore.GetComponent<NetworkObject>();
        if (claymoreNetworkObject == null)
        {
            Debug.LogError("Claymore prefab is missing a NetworkObject component!");
            return;
        }

        // Give ownership to the player who owns THIS shooter
        claymoreNetworkObject.SpawnWithOwnership(OwnerClientId);

        // Pass owner info into the mine (same pattern as your grenades)
        ClaymoreMineLAN mine = claymore.GetComponent<ClaymoreMineLAN>();
        if (mine != null)
        {
            mine.SetOwner(OwnerClientId, gameObject);
        }
    }
}
