using System.Collections;
using Photon.Pun;
using UnityEngine;

public class LauncherProjectile : MonoBehaviour
{
    [HideInInspector] public GameObject player;

    [Header("Projectile Settings")]
    public float shootForce = 1000f;
    public float arcHeightMultiplier = 0.5f; // Controls arc shape
    public float maxLifetime = 20f;
    public float spinStrength = 5f; // Controls how much spin to add

    [Header("Explosion Settings")]
    public GameObject explosionPrefab;
    public int damage;
    public float damageRadius;

    private Rigidbody rb;
    private bool hasExploded = false;

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

        if (!hasExploded)
        {
            Explode();
        }
    }

    void Explode()
    {
        hasExploded = true;

        // Visual explosion
        Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        if (GetComponent<PhotonView>().IsMine)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius);

            foreach (Collider collider in hits)
            {
                if (collider.CompareTag("Player"))
                {
                    PhotonView targetPV = collider.GetComponent<PhotonView>();
                    PlayerHealth targetHealth = collider.GetComponent<PlayerHealth>();

                    if (targetPV != null && targetHealth != null)
                    {
                        targetPV.RPC("RPC_TakeDamage", RpcTarget.AllBuffered, damage);

                        if (targetHealth.health <= 0)
                        {
                            player.GetComponent<PlayerHitAndKillsManager>()
                                .GetKill(targetPV.Owner.NickName);
                        }
                        else
                        {
                            player.GetComponent<PlayerHitAndKillsManager>()
                                .GetHit(damage);
                        }
                    }
                }
            }

            PhotonNetwork.Destroy(gameObject);
        }
    }
}
