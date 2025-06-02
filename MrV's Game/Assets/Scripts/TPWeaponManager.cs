using Photon.Pun;
using UnityEngine;

public class TPWeaponManager : MonoBehaviour
{
    public Transform tpWeaponParent;
    
    [PunRPC]
    public void RPC_SetTPWeapon(byte _index)
    {
        for (int i = 0; i < tpWeaponParent.childCount; i++)
        {
            tpWeaponParent.GetChild(i).gameObject.SetActive(i == _index);
        }
    }
}
