using System.Collections;
using UnityEngine;
using Unity.Netcode;

[NetworkMode(NetworkMode.LAN)]

public class ThrownProjectileLAN : NetworkBehaviour
{
    [Header("Projectile Settings")]
    public float randomRotationForce = 100f;
    public float lifetime = 3f; // This is in seconds - set in inspector
    public float throwForce = 50f;

    [Header("Explosion Settings")]
    public NetworkObject explosionPrefab;
    public int damage = 100;
    public float damageRadius = 5f;

    private Rigidbody rb;
    private bool hasExploded = false;
    private ulong ownerClientId;
    private GameObject ownerPlayer;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // DEBUG: Log the force being applied
        Debug.Log($"Applying throw force: {throwForce} to grenade, Lifetime: {lifetime} seconds");
        
        // Apply force in the forward direction
        rb.AddForce(transform.forward * throwForce, ForceMode.Impulse);
        
        // Add random rotation
        rb.AddTorque(new Vector3(
            Random.Range(-randomRotationForce, randomRotationForce),
            Random.Range(-randomRotationForce, randomRotationForce), 
            Random.Range(-randomRotationForce, randomRotationForce)
        ));

        // Schedule self-destruction with the inspector lifetime
        StartCoroutine(SelfDestructAfterDelay());
    }

    public void SetOwner(ulong clientId, GameObject player)
    {
        ownerClientId = clientId;
        ownerPlayer = player;
    }

    public void SetThrowForce(float force)
    {
        throwForce = force;
    }

    // REMOVED: SetLifetime method - we use the inspector value now

    void OnCollisionEnter(Collision collision)
    {
        // REMOVED: Explosion on collision
        // Grenade should bounce, not explode on contact
       // Debug.Log("Grenade bounced off: " + collision.gameObject.name);
    }

    IEnumerator SelfDestructAfterDelay()
    {
        // Debug.Log($"Grenade will explode in {lifetime} seconds");
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

        // Debug.Log("Grenade exploding!");

        // Spawn explosion effect on all clients
        if (IsServer)
        {
            SpawnExplosionClientRpc(transform.position);
            ApplyDamage();
            GetComponent<NetworkObject>().Despawn(true);
        }
    }

    [ClientRpc]
    private void SpawnExplosionClientRpc(Vector3 position)
    {
        // Instantiate explosion locally on all clients
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

                    // Notify hit/kill manager
                    if (ownerPlayer != null)
                    {
                        PlayerHitAndKillsManagerLAN hitManager = ownerPlayer.GetComponent<PlayerHitAndKillsManagerLAN>();
                        if (hitManager != null)
                        {
                            NotifyHitClientRpc(ownerClientId, damage);
                        }
                    }
                }
            }
        }
    }

    [ClientRpc]
    private void NotifyHitClientRpc(ulong attackerClientId, int damageAmount)
    {
        // Only the attacker should see the hit marker
        if (NetworkManager.Singleton.LocalClientId == attackerClientId)
        {
            PlayerHitAndKillsManagerLAN hitManager = GetComponent<PlayerHitAndKillsManagerLAN>();
            if (hitManager != null)
            {
                hitManager.GetHit(damageAmount);
            }
        }
    }
}