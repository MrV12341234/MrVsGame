using UnityEngine;
using Unity.Netcode;

[NetworkMode(NetworkMode.LAN)]

public class LocalPlayerKDManagerLAN : NetworkBehaviour
{
    public static LocalPlayerKDManagerLAN Instance;

    private NetworkVariable<int> kills = new NetworkVariable<int>();
    private NetworkVariable<int> deaths = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Instance = this;
        }
    }

    public void GetKill()
    {
        if (IsServer)
        {
            kills.Value++;
        }
        else
        {
            AddKillServerRpc();
        }
    }

    public void OnDied()
    {
        if (IsServer)
        {
            deaths.Value++;
        }
        else
        {
            AddDeathServerRpc();
        }
    }

    [ServerRpc]
    private void AddKillServerRpc()
    {
        kills.Value++;
    }

    [ServerRpc]
    private void AddDeathServerRpc()
    {
        deaths.Value++;
    }

    // Public getters for UI
    public int GetKills() => kills.Value;
    public int GetDeaths() => deaths.Value;
}