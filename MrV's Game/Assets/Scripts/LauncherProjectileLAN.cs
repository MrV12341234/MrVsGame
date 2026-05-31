using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("If the projectile falls below this world Y position, it explodes/despawns.")]
    public float selfDestructBelowY = -100f;

    [Header("Explosion Settings")]
    public NetworkObject explosionPrefab;
    public int damage = 50;
    public float damageRadius = 5f;

    private Rigidbody rb;
    private bool hasExploded = false;
    private ulong ownerClientId;
    private GameObject ownerPlayer;
    
    // custom for the helicopter guns
    private bool useCustomLaunchVelocity = false;
    private Vector3 customLaunchVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (useCustomLaunchVelocity)
        {
            // Helicopter guns use exact speed, not impulse force.
            rb.linearVelocity = customLaunchVelocity;
        }
        else
        {
            // Handheld launcher keeps using the old behavior.
            Vector3 arcDirection = (transform.forward + transform.up * arcHeightMultiplier).normalized;
            rb.AddForce(arcDirection * shootForce, ForceMode.Impulse);
        }

        // Apply random twist/spin for realism
        rb.AddTorque(Random.insideUnitSphere * spinStrength, ForceMode.Impulse);

        // Schedule self-destruction
        StartCoroutine(SelfDestructAfterDelay());
    }
    
    void Update()
    {
        if (!IsServer)
            return;

        if (hasExploded)
            return;

        if (transform.position.y < selfDestructBelowY)
        {
            Explode();
        }
    }

    public void SetOwner(ulong clientId, GameObject player)
    {
        ownerClientId = clientId;
        ownerPlayer = player;
    }
    
    public void SetCustomLaunchVelocity(Vector3 launchVelocity)
    {
        useCustomLaunchVelocity = true;
        customLaunchVelocity = launchVelocity;
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

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            damageRadius,
            ~0,
            QueryTriggerInteraction.Collide
        );

        HashSet<ClaymoreMineLAN> damagedMines = new HashSet<ClaymoreMineLAN>();

        foreach (Collider collider in hits)
        {
            // DAMAGE MINES
            ClaymoreMineLAN mine = collider.GetComponentInParent<ClaymoreMineLAN>();
            if (mine != null && !damagedMines.Contains(mine))
            {
                damagedMines.Add(mine);
                mine.Server_ApplyMineDamage(damage);
                continue;
            }

            // DAMAGE PLAYERS
            if (collider.CompareTag("Player"))
            {
                PlayerHealthLan targetHealth = collider.GetComponent<PlayerHealthLan>();
                if (targetHealth != null)
                {
                    targetHealth.TakeDamageServerRpc(damage, ownerClientId);

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