using UnityEngine;
using Photon.Pun;

public class AnimationSyncer : MonoBehaviour, IPunObservable
{
   

    [Range(-2, 2)] public float horizontal;
    [Range(-2, 2)] public float vertical;


    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(horizontal);
            stream.SendNext(vertical);
        }
        else
        {
            horizontal = (float)stream.ReceiveNext();
            vertical = (float)stream.ReceiveNext();
        }
    }

    void Update()
    {
        
        
    }
}
