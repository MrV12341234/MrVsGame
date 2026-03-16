using Unity.Netcode;
using UnityEngine;

[NetworkMode(NetworkMode.LAN)]
public class CTFCaptureZoneLan : NetworkBehaviour
{
    public RoomManagerLan.TeamId zoneTeam;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (RoomManagerLan.Instance == null || !RoomManagerLan.Instance.IsCTFMode) return;
        if (!other.CompareTag("Player")) return;

        var setup = other.GetComponentInParent<PlayerSetupLan>();
        if (setup == null) return;

        ulong carrierId = setup.OwnerClientId;
        var carrierTeam = setup.GetTeam();

        // only score if player is on THIS zone’s team
        if (carrierTeam != zoneTeam) return;

        var gm = CTFGameManagerLan.Instance;
        if (gm == null || gm.redFlag == null || gm.blueFlag == null) return;

        // zoneTeam captures the ENEMY flag
        CTFFlagLan enemyFlag = (zoneTeam == RoomManagerLan.TeamId.Blue) ? gm.redFlag : gm.blueFlag;

        if (enemyFlag.IsHeldBy(carrierId))
        {
            gm.ReportCaptureServerRpc(carrierId, zoneTeam);
        }
    }
}