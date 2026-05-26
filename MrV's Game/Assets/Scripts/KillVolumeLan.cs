using UnityEngine;

// attach this script to an object (plane, cube, etc) and the player will die when they touch that object
public class KillVolumeLan : MonoBehaviour
{
    [Header("Kill Volume Settings")]
    [SerializeField] private int damageAmount = 999;

    private void OnTriggerEnter(Collider other)
    {
        TryKillPlayer(other);
    }

    // Optional backup:
    // If you ever forget to tick "Is Trigger", this can still work on collision.
    private void OnCollisionEnter(Collision collision)
    {
        TryKillPlayer(collision.collider);
    }

    private void TryKillPlayer(Collider other)
    {
        if (other == null) return;

        // Looks for PlayerHealthLan on the object we touched or one of its parents
        PlayerHealthLan playerHealth = other.GetComponentInParent<PlayerHealthLan>();
        if (playerHealth == null) return;

        // IMPORTANT:
        // Only the owning client should report damage for its own player.
        // This matches the pattern you already use in PlayerHealthLan.Update().
        if (!playerHealth.IsOwner) return;

        // This matches your current fall-death style:
        // pass the player's own client id as the attacker.
        playerHealth.TakeDamageServerRpc(damageAmount, playerHealth.OwnerClientId);

        // If later you want the killfeed to say "Environment" instead,
        // replace the line above with this:
        // playerHealth.TakeDamageServerRpc(damageAmount, ulong.MaxValue);
    }
}