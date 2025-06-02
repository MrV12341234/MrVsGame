using System.Collections;
using Photon.Pun;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [HideInInspector] public GameObject player;
    
    public float throwForce = 1000f;
    public float randomRotationForce = 100f;
    public float lifetime = 5f;
    public GameObject explosionPrefab;
    [Header("Damage Setup")] public int damage;
    public float damageRadius;
    
    private Rigidbody rb;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.AddForce(transform.forward * throwForce);
        rb.AddTorque(new Vector3(UnityEngine.Random.Range(-randomRotationForce, randomRotationForce),UnityEngine.Random.Range(-randomRotationForce, randomRotationForce),UnityEngine.Random.Range(-randomRotationForce, randomRotationForce)));
        
        StartCoroutine(DelayedExplode());
    }

    IEnumerator DelayedExplode()
    {
        yield return new WaitForSeconds(lifetime);
        
        Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        
        // Destroy the projectile
        if (GetComponent<PhotonView>().IsMine)
        {
            foreach (var collider in Physics.OverlapSphere(transform.position, damageRadius))
            {
                if (collider.gameObject.CompareTag("Player"))
                {
                    // Damage code
                    collider.transform.GetComponent<PhotonView>().RPC("RPC_TakeDamage", RpcTarget.AllBuffered, damage);

                    if (collider.transform.GetComponent<PlayerHealth>().health <= 0)
                    {
                        // kill
                        player.GetComponent<PlayerHitAndKillsManager>().GetKill(collider.transform.GetComponent<PhotonView>().Owner.NickName);
                    }
                    else
                    {
                        // damage
                        player.GetComponent<PlayerHitAndKillsManager>().GetHit(damage);
                    }
                }
            }
            
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
