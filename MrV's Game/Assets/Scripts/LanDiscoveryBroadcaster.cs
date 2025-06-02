using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class LanDiscoveryBroadcaster : MonoBehaviour
{
    public int broadcastPort = 47777;
    private UdpClient udpClient;
    private float broadcastInterval = 1f;
    private float timer;
    private bool broadcastingStarted = false;

    void Start()
    {
        Debug.Log($"[LAN BROADCAST] IsHost: {NetworkManager.Singleton.IsHost}, IsListening: {NetworkManager.Singleton.IsListening}");
        StartCoroutine(WaitForHostAndStartBroadcasting());
    }

    IEnumerator WaitForHostAndStartBroadcasting()
    {
        Debug.Log("[LAN BROADCAST] Waiting to detect host...");

        // Wait until host starts
        while (!NetworkManager.Singleton.IsHost)
        {
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("[LAN BROADCAST] Host detected. Starting broadcaster...");

        udpClient = new UdpClient();
        udpClient.EnableBroadcast = true;

        broadcastingStarted = true;
    }
    void Update()
    {
        if (!broadcastingStarted) return;

        timer += Time.deltaTime;
        if (timer >= broadcastInterval)
        {
            timer = 0f;
            BroadcastRoomInfo();
        }
    }

    void BroadcastRoomInfo()
    {
        string roomName = PlayerPrefs.GetString("LAN_RoomName", "LAN Room");
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string message = $"{roomName}|{NetworkManager.Singleton.ConnectedClients.Count}|{GetLocalIPAddress()}|{currentScene}";


        byte[] data = Encoding.UTF8.GetBytes(message);
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, broadcastPort);
        udpClient.Send(data, data.Length, endPoint);

        Debug.Log($"[LAN BROADCAST] Sent room info: {roomName}");
    }

    string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        }
        return "127.0.0.1";
    }

    void OnDestroy()
    {
        if (udpClient != null)
        {
            udpClient.Close();
        }
    }
}
