using UnityEngine;
using Unity.Netcode;
using System.Collections;

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
            StartCoroutine(ArmAfterDelay());
            StartCoroutine(SelfDestructAfterLifetime());
        }
    }

    public void SetOwner(ulong clientId, GameObject player)
    {
        ownerClientId = clientId;
        ownerPlayer = player;
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

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!isArmed || hasExploded) return;

        if (!other.CompareTag("Player"))
            return;

        // Don't trigger from the owner walking on their own mine
        NetworkObject playerNetObj = other.GetComponent<NetworkObject>();
        if (playerNetObj != null && playerNetObj.OwnerClientId == ownerClientId)
            return;

        Explode();
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

        Collider[] hits = Physics.OverlapSphere(transform.position, blastRadius);

        foreach (Collider collider in hits)
        {
            if (!collider.CompareTag("Player"))
                continue;

            PlayerHealthLan targetHealth = collider.GetComponent<PlayerHealthLan>();
            if (targetHealth == null)
                continue;

            // Optional: don't damage owner. Remove this check if you want self-damage / friendly fire.
            NetworkObject netObj = collider.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == ownerClientId)
                continue;

            // Apply damage
            targetHealth.TakeDamageServerRpc(damage, ownerClientId);

            // Optional hit-marker logic, following your grenade pattern
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
