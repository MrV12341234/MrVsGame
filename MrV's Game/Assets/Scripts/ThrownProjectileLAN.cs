using System.Collections;
using UnityEngine;
using Unity.Netcode;
[NetworkMode(NetworkMode.LAN)]
public class ThrownProjectileLAN : NetworkBehaviour
{
    [Header("Projectile Settings")]
    public float randomRotationForce = 100f;
    public float lifetime = 4f;
    
    // Network variable for synchronized force
    private NetworkVariable<float> networkThrowForce = new NetworkVariable<float>();
    
    [Header("Explosion Settings")]
    public NetworkObject explosionPrefab;
    public int damage = 100;
    public float damageRadius = 5f;

    private Rigidbody rb;
    private bool hasExploded = false;
    private ulong ownerClientId;
    private GameObject ownerPlayer;
    private bool forceApplied = false;

    public override void OnNetworkSpawn()
    {
        // Only apply force on clients when the NetworkVariable updates
        networkThrowForce.OnValueChanged += OnThrowForceChanged;
        
        rb = GetComponent<Rigidbody>();
        
        // If we're the server and have the force value, apply it immediately
        if (IsServer && networkThrowForce.Value > 0)
        {
            ApplyForce(networkThrowForce.Value);
        }
        
        // Start lifetime countdown on all instances
        StartCoroutine(SelfDestructAfterDelay());
    }

    private void OnThrowForceChanged(float oldValue, float newValue)
    {
        // Clients apply force when they receive the synchronized value
        if (!IsServer && newValue > 0 && rb != null)
        {
            ApplyForce(newValue);
        }
    }

    private void ApplyForce(float force)
    {
        if (forceApplied) return;
        forceApplied = true;
        
        Debug.Log($"Applying throw force: {force} to grenade, Lifetime: {lifetime} seconds");
        
        // Apply force in the forward direction
        rb.AddForce(transform.forward * force, ForceMode.Impulse);
        
        // Add random rotation (use the same seed for consistency)
        Random.InitState((int)(force * 1000)); // Seed based on force for consistency
        rb.AddTorque(new Vector3(
            Random.Range(-randomRotationForce, randomRotationForce),
            Random.Range(-randomRotationForce, randomRotationForce), 
            Random.Range(-randomRotationForce, randomRotationForce)
        ));
    }

    public void SetOwner(ulong clientId, GameObject player)
    {
        ownerClientId = clientId;
        ownerPlayer = player;
    }

    public void SetThrowForce(float force)
    {
        if (IsServer)
        {
            networkThrowForce.Value = force;
            ApplyForce(force);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Grenade should bounce, not explode on contact
    }

    IEnumerator SelfDestructAfterDelay()
    {
        yield return new WaitForSeconds(lifetime);

        if (!hasExploded && IsServer)
        {
            Explode();
        }
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // Spawn explosion effect on all clients at the CURRENT position
        if (IsServer)
        {
            // Use the current transform position which should be synchronized
            SpawnExplosionClientRpc(transform.position);
            ApplyDamage();
            GetComponent<NetworkObject>().Despawn(true);
        }
    }

    [ClientRpc]
    private void SpawnExplosionClientRpc(Vector3 position)
    {
        // Instantiate explosion locally on all clients at the server-provided position
        Instantiate(explosionPrefab, position, Quaternion.identity);
    }

    private void ApplyDamage()
    {
        if (!IsServer) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius);

        foreach (Collider collider in hits)
        {
            if (collider.CompareTag("Player"))
            {
                PlayerHealthLan targetHealth = collider.GetComponent<PlayerHealthLan>();
                if (targetHealth != null)
                {
                    // Apply damage to the player
                    targetHealth.TakeDamageServerRpc(damage, ownerClientId);
                    
                    // Award hit points on server
                    if (LeaderboardManagerLAN.Instance != null)
                    {
                        LeaderboardManagerLAN.Instance.Server_AwardHit(ownerClientId);
                    }

                    // Notify hit/kill manager
                    if (ownerPlayer != null)
                    {
                        PlayerHitAndKillsManagerLAN hitManager = ownerPlayer.GetComponent<PlayerHitAndKillsManagerLAN>();
                        if (hitManager != null)
                        {
                            NotifyHitClientRpc(ownerClientId);
                        }
                    }
                }
            }
        }
    }

    [ClientRpc]
    private void NotifyHitClientRpc(ulong attackerClientId)
    {
        // Only the attacker should see the hit marker
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.LocalClientId == attackerClientId)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
            var hitMgr = localPlayer ? localPlayer.GetComponent<PlayerHitAndKillsManagerLAN>() : null;
            if (hitMgr != null)
                hitMgr.GetHit(0);
        }
    }
}