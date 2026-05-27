using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

[NetworkMode(NetworkMode.LAN)]
public class ClaymoreMineLAN : NetworkBehaviour
{
    [Header("Radii")]
    [Tooltip("Distance at which the claymore will trigger when an enemy walks near.")]
    public float triggerRadius = 3f;

    [Tooltip("Distance at which damage is applied when it explodes.")]
    public float blastRadius = 6f;

    [Header("Timing")]
    [Tooltip("Time after being placed before it can trigger.")]
    public float armDelay = 2f;

    [Tooltip("Failsafe lifetime before the mine deletes itself.")]
    public float maxLifetime = 300f;

    [Header("Damage")]
    public int damage = 100;
    [Header("Mine Health")]
    [Tooltip("How much damage the mine can take before it detonates.")]
    public int maxHealth = 50;

    private int currentHealth;

    [Header("Explosion VFX")]
    [Tooltip("Explosion prefab (particle, SFX, etc.) – visible on all clients.")]
    public NetworkObject explosionPrefab;

    private bool isArmed = false;
    private bool hasExploded = false;

    private ulong ownerClientId;
    private GameObject ownerPlayer;

    private SphereCollider triggerCollider;

    private void Awake()
    {
        // Ensure we have a trigger collider used for the trigger radius
        triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<SphereCollider>();
        }

        triggerCollider.isTrigger = true;
        triggerCollider.radius = triggerRadius;
    }

    private void OnValidate()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();

        if (triggerCollider != null)
            triggerCollider.radius = triggerRadius;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            currentHealth = maxHealth;
            StartCoroutine(ArmAfterDelay());
            StartCoroutine(SelfDestructAfterLifetime());
        }
    }

    public void SetOwner(ulong clientId, GameObject player)
    {
        ownerClientId = clientId;
        ownerPlayer = player;
    }
    
    public void Server_ApplyMineDamage(int damageAmount)
    {
        if (!IsServer) return;
        if (hasExploded) return;
        if (damageAmount <= 0) return;

        currentHealth -= damageAmount;

        if (currentHealth <= 0)
        {
            Explode();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damageAmount)
    {
        Server_ApplyMineDamage(damageAmount);
    }

    private IEnumerator ArmAfterDelay()
    {
        yield return new WaitForSeconds(armDelay);
        isArmed = true;
    }

    private IEnumerator SelfDestructAfterLifetime()
    {
        yield return new WaitForSeconds(maxLifetime);

        if (!hasExploded && IsServer)
        {
            Explode();
        }
    }
    
    private bool TryTriggerFromVehicle(Collider other)
    {
        if (other == null)
            return false;

        // Ignore vehicle seat trigger colliders.
        // We only want the real car body collider / physical collider.
        if (other.isTrigger)
            return false;

        LanVehicleSeatManager vehicle = other.GetComponentInParent<LanVehicleSeatManager>();
        if (vehicle == null)
            return false;

        Explode();
        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!isArmed || hasExploded) return;

        // Vehicle body collider triggers the mine.
        // This must happen before the Player tag check because cars are not tagged Player.
        if (TryTriggerFromVehicle(other))
            return;

        if (!other.CompareTag("Player"))
            return;

        // Don't trigger from the owner walking on their own mine
        NetworkObject playerNetObj = other.GetComponent<NetworkObject>();
        if (playerNetObj != null && playerNetObj.OwnerClientId == ownerClientId)
            return;

        Explode();
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;
        if (!isArmed || hasExploded) return;

        // Backup for cases where the car enters the trigger before the mine finishes arming.
        if (TryTriggerFromVehicle(other))
            return;
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

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
        if (explosionPrefab != null)
        {
            // Same pattern as your LauncherProjectileLAN: local effect on each client
            Instantiate(explosionPrefab, position, Quaternion.identity);
        }
    }

    private void ApplyDamage()
    {
        if (!IsServer) return;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            blastRadius,
            ~0,
            QueryTriggerInteraction.Collide
        );

        HashSet<ClaymoreMineLAN> damagedMines = new HashSet<ClaymoreMineLAN>();

        foreach (Collider collider in hits)
        {
            // DAMAGE OTHER MINES
            ClaymoreMineLAN otherMine = collider.GetComponentInParent<ClaymoreMineLAN>();
            if (otherMine != null && otherMine != this && !damagedMines.Contains(otherMine))
            {
                damagedMines.Add(otherMine);
                otherMine.Server_ApplyMineDamage(damage);
                continue;
            }

            // DAMAGE PLAYERS
            if (!collider.CompareTag("Player"))
                continue;

            PlayerHealthLan targetHealth = collider.GetComponentInParent<PlayerHealthLan>();
            
            if (targetHealth == null)
                continue;

            NetworkObject netObj = targetHealth.GetComponent<NetworkObject>();
            
            if (netObj != null && netObj.OwnerClientId == ownerClientId)
                continue;

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

    [ClientRpc]
    private void NotifyHitClientRpc(ulong attackerClientId, int damageAmount)
    {
        // Same pattern as LauncherProjectileLAN: only attacker sees this feedback
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.LocalClientId == attackerClientId)
        {
            PlayerHitAndKillsManagerLAN hitManager = GetComponent<PlayerHitAndKillsManagerLAN>();
            if (hitManager != null)
            {
                hitManager.GetHit(damageAmount);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize trigger vs blast radius in the editor
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, blastRadius);
    }
}
