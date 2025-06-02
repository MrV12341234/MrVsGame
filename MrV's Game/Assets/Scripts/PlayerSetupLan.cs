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
        Debug.Log("[LAN] PlayerSetupLan.cs is running!");

        bool isLocal = IsOwner;

        fpCamera.SetActive(isLocal);
        movement.enabled = isLocal;
        tpPlayer.SetActive(!isLocal);
        nameText.gameObject.SetActive(!isLocal);
        


        if (isLocal)
        {
            byte savedHat = (byte)PlayerPrefs.GetInt("hatSelected", 0);
            hatIndex.Value = savedHat;

            string localName = PlayerPrefs.GetString("PlayerName", $"Player_{Random.Range(0, 999)}");
            playerName.Value = localName;

            // Delay cursor lock to allow UI to finish transitions
            StartCoroutine(LockCursorDelayed());
        }

        UpdateHatVisual(hatIndex.Value);
        nameText.text = playerName.Value.ToString();

        hatIndex.OnValueChanged += (_, newValue) => UpdateHatVisual(newValue);
        playerName.OnValueChanged += (_, newName) => nameText.text = newName.ToString();
        
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
