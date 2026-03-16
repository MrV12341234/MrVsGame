using Unity.Netcode;
using UnityEngine;

[NetworkMode(NetworkMode.LAN)]
public class CTFFlagCarrierWeaponsLockLan : NetworkBehaviour
{
    [Header("Weapon Objects To Disable (assign on prefab)")]
    [Tooltip("Your FP WeaponSwitcher object (the parent that contains WeaponSwitcherLAN).")]
    [SerializeField] private GameObject fpWeaponSwitcherRoot;

    [Tooltip("Your TP_GunHolder object (third-person guns).")]
    [SerializeField] private GameObject tpGunHolderRoot;

    [Tooltip("Optional: any extra gun-related roots (sway holder, etc) you want hidden while carrying.")]
    [SerializeField] private GameObject[] extraRootsToDisable;

    private WeaponSwitcherLAN _weaponSwitcher;
    private bool _isLocked;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only the owner should drive input + local locking decisions.
        if (!IsOwner) return;

        if (fpWeaponSwitcherRoot != null)
            _weaponSwitcher = fpWeaponSwitcherRoot.GetComponentInChildren<WeaponSwitcherLAN>(true);

        // Ensure normal state on spawn
        SetLocked(false);
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (RoomManagerLan.Instance == null || !RoomManagerLan.Instance.IsCTFMode) 
        {
            // Not CTF -> ensure unlocked
            if (_isLocked) SetLocked(false);
            return;
        }

        var gm = CTFGameManagerLan.Instance;
        if (gm == null || gm.blueFlag == null || gm.redFlag == null) return;

        ulong myId = NetworkManager.Singleton.LocalClientId;

        bool holdingBlue = gm.blueFlag.IsHeldBy(myId);
        bool holdingRed  = gm.redFlag.IsHeldBy(myId);
        bool isCarrier = holdingBlue || holdingRed;

        // Lock/unlock weapons when carrier state changes
        if (isCarrier != _isLocked)
            SetLocked(isCarrier);

        // If carrying and Fire1 pressed -> drop (instead of shooting)
        if (_isLocked && Input.GetButtonDown("Fire1"))
        {
            if (holdingBlue) gm.blueFlag.RequestDropByCarrierServerRpc();
            else if (holdingRed) gm.redFlag.RequestDropByCarrierServerRpc();
        }
    }

    private void SetLocked(bool locked)
    {
        _isLocked = locked;

        // Disable FP weapon switching + hide FP guns
        if (fpWeaponSwitcherRoot != null)
            fpWeaponSwitcherRoot.SetActive(!locked);

        if (_weaponSwitcher != null)
            _weaponSwitcher.enabled = !locked;

        // Hide TP guns so other players don’t see carrier armed
        if (tpGunHolderRoot != null)
            tpGunHolderRoot.SetActive(!locked);

        // Any other roots you want hidden
        if (extraRootsToDisable != null)
        {
            for (int i = 0; i < extraRootsToDisable.Length; i++)
            {
                if (extraRootsToDisable[i] != null)
                    extraRootsToDisable[i].SetActive(!locked);
            }
        }
    }
}