using System.Collections;
using Photon.Pun;
using UnityEngine;

public class TimedObjectDestructor : MonoBehaviour
{
    public float lifetime = 3f;
    
    
    void Start()
    {
        if (GetComponent<PhotonView>())
        {
            if (GetComponent<PhotonView>().IsMine)
            { 
                StartCoroutine(DelayedDestroyPhoton());
            }
        }
        else
        {
            StartCoroutine(DelayedRegularDestroy());
        }
        
        
    }

    IEnumerator DelayedDestroyPhoton()
    {
        yield return new WaitForSeconds(lifetime);
        
        PhotonNetwork.Destroy(gameObject);
    }

    IEnumerator DelayedRegularDestroy()
    {
        yield return new WaitForSeconds(lifetime);
        
        Destroy(gameObject);
    }

}
