using Unity.Netcode;
using UnityEngine;

[NetworkMode(NetworkMode.LAN)]
public class CTFGameManagerLan : NetworkBehaviour
{
    public static CTFGameManagerLan Instance;

    [Header("CTF Scene Root (disable in non-CTF)")]
    [Tooltip("Parent object that contains BOTH flags + BOTH capture zones (and any other CTF-only objects).")]
    public GameObject ctfRoot; // where the flags and capture zone game objects live

    [Header("Flags")]
    public CTFFlagLan blueFlag;
    public CTFFlagLan redFlag;

    private void Awake()
    {
        Instance = this;

        // Use PlayerPrefs here because RoomManagerLan.gameMode is set in Start(),
        // and Awake runs earlier.
        ApplyCTFVisibilityFromPrefs();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Re-apply once network is up (safe for late join/scene reload edge cases)
        ApplyCTFVisibilityFromPrefs();
    }

    private void ApplyCTFVisibilityFromPrefs()
    {
        // 0 = FFA, 1 = Teams, 2 = CTF
        bool isCTF = PlayerPrefs.GetInt("LAN_GameMode", 0) == 2;

        if (ctfRoot != null)
            ctfRoot.SetActive(isCTF);

        // Extra safety: if someone forgot to parent flags under ctfRoot,
        // at least disable the scripts in non-CTF.
        if (!isCTF)
        {
            if (blueFlag != null) blueFlag.enabled = false;
            if (redFlag != null) redFlag.enabled = false;
            enabled = false; // disables this manager too (optional but nice)
        }
        else
        {
            if (blueFlag != null) blueFlag.enabled = true;
            if (redFlag != null) redFlag.enabled = true;
            enabled = true;
        }
    }

    public bool IsCTFActive =>
        RoomManagerLan.Instance != null && RoomManagerLan.Instance.IsCTFMode;

    [ServerRpc(RequireOwnership = false)]
    public void ReportCaptureServerRpc(ulong scorerClientId, RoomManagerLan.TeamId scoringTeam)
    {
        if (!IsServer) return;
        if (!IsCTFActive) return;

        TeamScoreManagerLan.Instance?.Server_AddCTFCapture(scoringTeam);

        if (blueFlag != null) blueFlag.Server_ReturnToBase();
        if (redFlag != null) redFlag.Server_ReturnToBase();

        AnnounceClientRpc($"{scoringTeam} captured the flag!");
    }

    [ClientRpc]
    public void AnnounceClientRpc(string msg)
    {
        CTFAnnouncementUILan.Instance?.Show(msg, 2f);
    }
}