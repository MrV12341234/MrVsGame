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

    private NetworkVariable<byte> hatIndex = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>("Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log("[LAN] PlayerSetupLan.cs is running!");

        bool isLocal = IsOwner;

        // Camera & control visibility
        if (fpCamera) fpCamera.SetActive(isLocal);
        if (movement) movement.enabled = isLocal;
        if (tpPlayer) tpPlayer.SetActive(!isLocal);
        if (nameText) nameText.gameObject.SetActive(!isLocal);

        // --- AUDIO LISTENER RULE: exactly one per process; only local player keeps theirs ---
        // Disable all listeners under this player first…
        var listeners = GetComponentsInChildren<AudioListener>(true);
        foreach (var l in listeners) l.enabled = false;

        // …then, if this is the local player, enable the one on the FP camera (if present)
        if (isLocal && fpCamera)
        {
            var camListener = fpCamera.GetComponent<AudioListener>();
            if (camListener) camListener.enabled = true;
        }
        // ------------------------------------------------------------------------------

        if (isLocal)
        {
            byte savedHat = (byte)PlayerPrefs.GetInt("hatSelected", 0);
            hatIndex.Value = savedHat;

            string localName = PlayerPrefs.GetString("PlayerName", $"Player_{Random.Range(0, 999)}");
            playerName.Value = localName;

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
}
