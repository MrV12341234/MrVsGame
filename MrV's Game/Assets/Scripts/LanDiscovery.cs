using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Unity.Netcode; // Needed for NetworkManager

public class LanDiscovery : MonoBehaviour
{
    public int listenPort = 47777;
    private UdpClient udpClient;
    private IPEndPoint endPoint;

    public List<LanRoomInfo> discoveredRooms = new List<LanRoomInfo>();

    void Start()
    {
        // Disable this script if we're the host
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            Debug.Log("[LAN DISCOVERY] This is host, disabling discovery script.");
            this.enabled = false;
            return;
        }
        Debug.Log("[LAN DISCOVERY] Client listening for LAN broadcasts...");
        udpClient = new UdpClient(listenPort);
        udpClient.EnableBroadcast = true;
        endPoint = new IPEndPoint(IPAddress.Any, listenPort);
        udpClient.BeginReceive(OnReceive, null);
    }
    void OnReceive(System.IAsyncResult result)
    {
        byte[] data = udpClient.EndReceive(result, ref endPoint);
        udpClient.BeginReceive(OnReceive, null);

        string message = Encoding.UTF8.GetString(data);
        Debug.Log($"[LAN DISCOVERY] Received broadcast: {message}");
        string[] parts = message.Split('|');

        if (parts.Length == 4)
        {
            LanRoomInfo info = new LanRoomInfo
            {
                roomName = parts[0],
                playerCount = parts[1],
                ipAddress = parts[2],
                sceneName = parts[3]
            };


            // Avoid duplicates
            bool alreadyExists = discoveredRooms.Exists(r => 
                r.ipAddress == info.ipAddress && r.roomName == info.roomName);
            {
                discoveredRooms.Add(info);
                Debug.Log($"[LAN DISCOVERY] Room added: {info.roomName} ({info.ipAddress})");
            }
        }
    }

    void OnDestroy()
    {
        if (udpClient != null)
        {
            udpClient.Close();
        }
    }
}

[System.Serializable]
public class LanRoomInfo
{
    public string roomName;
    public string playerCount;
    public string ipAddress;
    public string sceneName;
}