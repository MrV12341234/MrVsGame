using UnityEngine;
using Unity.Netcode;

[NetworkMode(NetworkMode.LAN)]
public class RegisterNetworkPrefabs : MonoBehaviour
{
    public GameObject[] prefabsToRegister;

    void Awake()
    {
        foreach (var prefab in prefabsToRegister)
        {
            if (prefab != null)
            {
                NetworkManager.Singleton.AddNetworkPrefab(prefab);
                Debug.Log($"[LAN] Registered network prefab: {prefab.name}");
            }
        }
    }
}