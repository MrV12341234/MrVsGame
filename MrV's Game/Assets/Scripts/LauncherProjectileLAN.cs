using System.Collections;
using UnityEngine;
using Unity.Netcode;

[NetworkMode(NetworkMode.LAN)]
public class LauncherProjectileLAN : NetworkBehaviour
{
    [Header("Projectile Settings")]
    public float shootForce = 1000f;
    public float arcHeightMultiplier = 0.5f;
    public float maxLifetime = 20f;
    public float spinStrength = 5f;

    [Header("Explosion Settings")]
    public NetworkObject explosionPrefab;
    public int damage = 50;
    public float damageRadius = 5f;

    private Rigidbody rb;
    private bool hasExploded = false;
    private ulong ownerClientId;
    private GameObject ownerPlayer;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Launch forward with upward arc
        Vector3 arcDirection = (transform.forward + transform.up * arcHeightMultiplier).normalized;
        rb.AddForce(arcDirection * shootForce, ForceMode.Impulse);

        // Apply random twist/spin for realism
        rb.AddTorque(Random.insideUnitSphere * spinStrength, ForceMode.Impulse);

        // Schedule self-destruction
        StartCoroutine(SelfDestructAfterDelay());
    }

    public void SetOwner(ulong clientId, GameObject player)
    {
        ownerClientId = clientId;
        ownerPlayer = player;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!hasExploded)
        {
            Explode();
        }
    }

    IEnumerator SelfDestructAfterDelay()
    {
        yield return new WaitForSeconds(maxLifetime);

        if (!hasExploded && IsServer)
        {
            Explode();
        }
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

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
                            // We can't easily check health here, so we'll let PlayerHealthLan handle kill notifications
                            // Just trigger hit marker for now
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