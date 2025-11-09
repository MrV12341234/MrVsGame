using UnityEngine;
using TMPro;
using Unity.Collections;
using Unity.Netcode;

[NetworkMode(NetworkMode.LAN)]
public class PlayerSetupLan : NetworkBehaviour
{
    public GameObject fpCamera;
    public Movement movement;
    public GameObject tpPlayer;
    public TextMeshProUGUI nameText;

    [Header("Hat Setup")]
    public Transform hatParent;

    private NetworkVariable<byte> hatIndex =
        new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // SERVER-WRITABLE: Only server can write this
    private NetworkVariable<FixedString32Bytes> playerName =
        new NetworkVariable<FixedString32Bytes>("Player",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    // allow other scripts (server-side killfeed) to read
    public string GetPlayerNameString() => playerName.Value.ToString();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"[LAN] PlayerSetupLan.OnNetworkSpawn — OwnerClientId={OwnerClientId} LocalClientId={NetworkManager.Singleton.LocalClientId} IsOwner={IsOwner}");

        bool isLocal = IsOwner;

        // Camera & control visibility
        if (fpCamera) fpCamera.SetActive(isLocal);
        if (movement) movement.enabled = isLocal;
        if (tpPlayer) tpPlayer.SetActive(!isLocal);
        if (nameText) nameText.gameObject.SetActive(!isLocal);

        // AUDIO LISTENER rule
        var listeners = GetComponentsInChildren<AudioListener>(true);
        foreach (var l in listeners) l.enabled = false;
        if (isLocal && fpCamera)
        {
            var camListener = fpCamera.GetComponent<AudioListener>();
            if (camListener) camListener.enabled = true;
        }

        if (isLocal)
        {
            // local cosmetics still owner-writable
            byte savedHat = (byte)PlayerPrefs.GetInt("hatSelected", 0);
            hatIndex.Value = savedHat;

            // Send our chosen name to the server once per spawn (server writes NV)
            var localName = PlayerPrefs.GetString("PlayerName", $"Player_{NetworkManager.Singleton.LocalClientId}");
            
            SubmitInitialNameServerRpc(localName);

            // Delay cursor lock to allow UI to finish transitions
            StartCoroutine(LockCursorDelayed());
        }

        // Apply current visuals and subscribe to changes
        UpdateHatVisual(hatIndex.Value);
        if (nameText) nameText.text = playerName.Value.ToString();

        hatIndex.OnValueChanged += (_, newValue) => UpdateHatVisual(newValue);
        playerName.OnValueChanged += (_, newName) =>
        {
            if (nameText) nameText.text = newName.ToString();
        };

        Debug.Log("[LAN] Player has spawned! IsOwner: " + IsOwner);
    }

    private void UpdateHatVisual(byte index)
    {
        for (int i = 0; i < hatParent.childCount; i++)
        {
            hatParent.GetChild(i).gameObject.SetActive(i == index);
        }
    }

    private System.Collections.IEnumerator LockCursorDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("[LAN] Cursor locked and hidden.");
    }
    
    // Called by server only to set NV safely
    public void ServerSetName(string s)
    {
        var finalName = string.IsNullOrWhiteSpace(s) ? "Player" : s;
        playerName.Value = new FixedString32Bytes(finalName);
        
        // also update server-side cache so respawns & any server logic see the correct name
        RoomManagerLan.Instance?.StorePlayerName(OwnerClientId, finalName);
        
        Debug.Log($"[LAN][Server] ServerSetName for OwnerClientId={OwnerClientId} -> '{finalName}'");
    }

    // Owner tells server their chosen name (one tiny RPC)
    [ServerRpc]
    private void SubmitInitialNameServerRpc(string chosenName)
    {
        ServerSetName(chosenName);
    }
}
