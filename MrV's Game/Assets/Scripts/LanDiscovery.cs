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

        // UPDATED: expect 5 parts now
        if (parts.Length == 5)
        {
            int parsedMode = 0;
            int.TryParse(parts[4], out parsedMode);

            LanRoomInfo info = new LanRoomInfo
            {
                roomName = parts[0],
                playerCount = parts[1],
                ipAddress = parts[2],
                sceneName = parts[3],
                gameMode = parsedMode
            };

            // duplicate check
            LanRoomInfo existing = discoveredRooms.Find(r =>
                r.ipAddress == info.ipAddress &&
                r.roomName == info.roomName);

            if (existing != null)
            {
                // update the existing entry
                existing.playerCount = info.playerCount;
                existing.sceneName = info.sceneName;
                existing.gameMode = info.gameMode;
            }
            else
            {
                // add new entry
                discoveredRooms.Add(info);
            }
        }
        else
        {
            Debug.LogWarning($"[LAN DISCOVERY] Broadcast ignored. Expected 5 parts, got {parts.Length}");
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
    public int gameMode; // 0=FFA, 1 = Teams, 2 = CTF
}