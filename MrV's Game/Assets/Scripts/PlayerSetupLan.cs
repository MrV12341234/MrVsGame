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

    [Header("Team Visuals")]
    public Renderer playerModelRenderer;  // drag your visible model renderer here
    public Material ffaMaterial;
    public Material blueMaterial;
    public Material redMaterial;

    private NetworkVariable<byte> hatIndex =
        new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // Server writes this name
    private NetworkVariable<FixedString32Bytes> playerName =
        new NetworkVariable<FixedString32Bytes>("Player",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    // Server writes this team
    private NetworkVariable<RoomManagerLan.TeamId> team =
        new NetworkVariable<RoomManagerLan.TeamId>(
            RoomManagerLan.TeamId.Blue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public string GetPlayerNameString() => playerName.Value.ToString();
    public RoomManagerLan.TeamId GetTeam() => team.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        bool isLocal = IsOwner;

        // Camera & control visibility
        if (fpCamera) fpCamera.SetActive(isLocal);
        if (movement) movement.enabled = isLocal;
        if (tpPlayer) tpPlayer.SetActive(!isLocal);
        if (nameText) nameText.gameObject.SetActive(!isLocal);

        // AUDIO LISTENER: disable all, enable local camera's only
        var listeners = GetComponentsInChildren<AudioListener>(true);
        foreach (var l in listeners) l.enabled = false;
        if (isLocal && fpCamera)
        {
            var camListener = fpCamera.GetComponent<AudioListener>();
            if (camListener) camListener.enabled = true;
        }

        // Apply visuals once, for everyone
        ApplyHatVisualAndOwnerHide(hatIndex.Value);
        if (nameText) nameText.text = playerName.Value.ToString();
        ApplyTeamVisual(team.Value);

        // Subscribe to changes (for everyone)
        hatIndex.OnValueChanged += OnHatIndexChanged;
        playerName.OnValueChanged += (_, newName) =>
        {
            if (nameText) nameText.text = newName.ToString();
        };
        team.OnValueChanged += (_, newTeam) =>
        {
            ApplyTeamVisual(newTeam);
        };

        // Local-only: set hat + submit name
        if (isLocal)
        {
            byte savedHat = (byte)PlayerPrefs.GetInt("hatSelected", 0);
            hatIndex.Value = savedHat;

            //Name Submission on spawn/respawn.
            // var localName = PlayerPrefs.GetString("PlayerName", $"Player_{NetworkManager.Singleton.LocalClientId}");
            // SubmitInitialNameServerRpc(localName);
            
            // If match already started, make sure lobby UI is not visible
            var rm = RoomManagerLan.Instance;
            if (rm != null && rm.IsMatchStarted && rm.teamLobbyUI != null)
                rm.teamLobbyUI.HideLobby();
            
            ApplyMapMovementSettingsIfNeeded(); // apply specific map settings (any map needs to have an empty gameobject added with MapMovementSettings.cs)

            StartCoroutine(LockCursorDelayed());
        }
    }

    private void OnHatIndexChanged(byte _, byte newValue)
    {
        ApplyHatVisualAndOwnerHide(newValue);
    }

    private void ApplyHatVisualAndOwnerHide(byte index)
    {
        UpdateHatVisual(index);

        // Hide active hat locally for owner only
        if (IsOwner)
            HideActiveHatLocally();
    }

    private void UpdateHatVisual(byte index)
    {
        if (hatParent == null) return;

        for (int i = 0; i < hatParent.childCount; i++)
        {
            bool enable = (i == index);
            hatParent.GetChild(i).gameObject.SetActive(enable);
        }
    }

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

        var renderers = activeHat.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            r.forceRenderingOff = true;

        var colliders = activeHat.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
            c.enabled = false;
    }

    private System.Collections.IEnumerator LockCursorDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ApplyTeamVisual(RoomManagerLan.TeamId t)
    {
        if (playerModelRenderer == null) return;

        bool isTeamBasedMode = false;

        // Prefer RoomManager if available
        if (RoomManagerLan.Instance != null)
        {
            isTeamBasedMode = RoomManagerLan.Instance.IsTeamsMode; // true for Teams and CTF
        }
        else
        {
            // Fallback if RoomManager isn't ready yet
            int gm = PlayerPrefs.GetInt("LAN_GameMode", 0);
            isTeamBasedMode =
                gm == (int)RoomManagerLan.LanGameMode.Teams ||
                gm == (int)RoomManagerLan.LanGameMode.CTF;
        }

        if (!isTeamBasedMode)
        {
            if (ffaMaterial != null)
                playerModelRenderer.material = ffaMaterial;
            return;
        }

        if (t == RoomManagerLan.TeamId.Blue)
        {
            if (blueMaterial != null)
                playerModelRenderer.material = blueMaterial;
        }
        else
        {
            if (redMaterial != null)
                playerModelRenderer.material = redMaterial;
        }
    }


    // Server-only setter called by RoomManagerLan after spawn
    public void ServerSetTeam(RoomManagerLan.TeamId t)
    {
        if (!IsServer) return;
        team.Value = t;
    }

    // Server-only setter
    public void ServerSetName(string s)
    {
        if (!IsServer) return;

        var finalName = string.IsNullOrWhiteSpace(s) ? "Player" : s;
        playerName.Value = new FixedString32Bytes(finalName);
    }


    [ServerRpc]
    private void SubmitInitialNameServerRpc(string chosenName, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // The real client who sent this RPC
        ulong senderId = rpcParams.Receive.SenderClientId;

        // Set the name NetworkVariable on THIS player object
        ServerSetName(chosenName);

        // Store it in RoomManager using senderId (authoritative + correct)
        var finalName = string.IsNullOrWhiteSpace(chosenName) ? "Player" : chosenName;
        RoomManagerLan.Instance?.StorePlayerName(senderId, finalName);
    }
    
    //this method is for specific maps that have player movement overides (add more gravity etc). 
    private void ApplyMapMovementSettingsIfNeeded()
    {
        if (movement == null) return;

        var mapSettings = FindFirstObjectByType<MapMovementSettings>();

        if (mapSettings != null && mapSettings.useOverrides)
        {
            movement.ApplyMovementOverrides(
                    mapSettings.walkSpeed,
                    mapSettings.sprintSpeed,
                    mapSettings.maxVelocityChange,
                    mapSettings.jumpForce,
                    mapSettings.extraGravity,
                    mapSettings.useGetKeyDownForJump
            );

            Debug.Log($"[PlayerSetupLan] Applied map movement overrides: jump={mapSettings.jumpForce}, extraGravity={mapSettings.extraGravity}");
        }
        else
        {
            movement.ResetToDefaults();
            Debug.Log("[PlayerSetupLan] No map movement overrides found. Using default movement.");
        }
    }


}
