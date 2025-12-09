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
            // local cosmetics still owner-writable. Load saved hat choice and publish to the network
            byte savedHat = (byte)PlayerPrefs.GetInt("hatSelected", 0); // use -1 in prefs if you support "no hat"
            hatIndex.Value = savedHat;

            // Send our chosen name to the server once per spawn (server writes NV)
            var localName = PlayerPrefs.GetString("PlayerName", $"Player_{NetworkManager.Singleton.LocalClientId}");
            
            SubmitInitialNameServerRpc(localName);

            // Delay cursor lock to allow UI to finish transitions
            StartCoroutine(LockCursorDelayed());
        }

        // Apply current visuals and subscribe to changes
        ApplyHatVisualAndOwnerHide(hatIndex.Value);               // <<< important: apply once on spawn
        if (nameText) nameText.text = playerName.Value.ToString();

        hatIndex.OnValueChanged += OnHatIndexChanged;
        playerName.OnValueChanged += (_, newName) =>
        {
            if (nameText) nameText.text = newName.ToString();
        };

        Debug.Log("[LAN] Player has spawned! IsOwner: " + IsOwner);
    }
    
    private void OnHatIndexChanged(byte _, byte newValue)
    {
        ApplyHatVisualAndOwnerHide(newValue);
    }

    /// <summary>
    /// Enables only the selected hat for everyone.
    /// If this is the owner, also hides rendering locally so FP camera doesn't see it.
    /// </summary>
    private void ApplyHatVisualAndOwnerHide(byte index)
    {
        UpdateHatVisual(index);

        // Hide the active hat from the local owner's FP view (others still see it)
        if (IsOwner)
            HideActiveHatLocally();
    }

    private void UpdateHatVisual(byte index)
    {
        if (hatParent == null) return;

        for (int i = 0; i < hatParent.childCount; i++)
        {
            // If you support "no hat" with index 255 or -1, change this condition accordingly
            bool enable = (i == index);
            hatParent.GetChild(i).gameObject.SetActive(enable);
        }
    }
    
    /// <summary>
    /// Locally hides the currently active hat (renderers off, colliders optional off) for the owner only.
    /// This is not networked; others still render your hat.
    /// </summary>
    private void HideActiveHatLocally()
    {
        if (hatParent == null) return;

        Transform activeHat = null;
        for (int i = 0; i < hatParent.childCount; i++)
        {
            var child = hatParent.GetChild(i);
            if (child.gameObject.activeInHierarchy)
            {
                activeHat = child;
                break;
            }
        }
        if (activeHat == null) return;

        // Safest local-only hide: forceRenderingOff (keeps state consistent but invisible locally)
        var renderers = activeHat.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            r.forceRenderingOff = true;

        // Optional: avoid raycast / interaction issues if hats have colliders
        var colliders = activeHat.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
            c.enabled = false;

        // --- Alternative (layer culling):
        // If you prefer layers, put the activeHat on a "HiddenFromFPCam" layer for the owner only
        // and ensure the FP camera culls that layer in its Culling Mask.
        // int hiddenLayer = LayerMask.NameToLayer("HiddenFromFPCam");
        // if (hiddenLayer >= 0)
        // {
        //     foreach (var t in activeHat.GetComponentsInChildren<Transform>(true))
        //         t.gameObject.layer = hiddenLayer;
        // }
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
