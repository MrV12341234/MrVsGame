using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LanNetworkManager : MonoBehaviour
{
    public static LanNetworkManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // persist between scenes
        }
        else
        {
            Destroy(gameObject);
        }
    }

    
    public void HostLanGame(string mapSceneName)
    {
        GameMode.IsLAN = true;

        // ONLY load the map scene, do NOT start host yet
        SceneManager.LoadScene(mapSceneName);
    }

    public void JoinLanGame(string ipAddress)
    {
        GameMode.IsLAN = true;

        var unityTransport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (unityTransport != null)
        {
            unityTransport.SetConnectionData(ipAddress, 7777); // default port
            NetworkManager.Singleton.StartClient();
        }
        else
        {
            Debug.LogError("Unity Transport not found!");
        }
    }

    public void Shutdown()
    {
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }
}