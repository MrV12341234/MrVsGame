using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Unity.Netcode.Components;

//attached to the player root

[NetworkMode(NetworkMode.LAN)]
public class PlayerVehicleSeatStateLan : NetworkBehaviour
{
    [Header("References")]
    public Movement movement;
    public MouseLook mouseLook;
    public Rigidbody playerRigidbody;
    public NetworkTransform playerNetworkTransform;

    [Tooltip("Optional. Auto-found if left empty. Used so car and helicopter seat systems do not fight.")]
    public PlayerHelicopterSeatStateLan helicopterSeatState;

    [Header("Weapon Roots")]
    [Tooltip("Assign the FP weapon parent. Example: FP_Camera/SwayHolder/WeaponSwitcher")]
    public GameObject fpWeaponSwitcherRoot;

    [Tooltip("Assign the TP gun holder root. Example: ScaleFix/Player Model/TP_GunHolder")]
    public GameObject tpGunHolderRoot;

    [Header("Prompt UI")]
    [Tooltip("Assign a TMP_Text under the local player's Canvas.")]
    public TMP_Text vehiclePromptText;

    private LanVehicleSeatManager _nearbyVehicle;
    private int _nearbySeatIndex = -1;
    private bool _nearbySeatIsDriver;

    private LanVehicleSeatManager _currentVehicle;

    private bool _defaultUseGravity = true;
    private bool _defaultIsKinematic = false;
    private Collider[] _playerColliders;
    private Coroutine _forceVehicleExitRoutine;

// NetworkTransform axis sync defaults
    private bool _ntPosX, _ntPosY, _ntPosZ;
    private bool _ntRotX, _ntRotY, _ntRotZ;
    private bool _ntScaleX, _ntScaleY, _ntScaleZ;
    private bool _ntDefaultsCached;

    private readonly NetworkVariable<bool> isSeated =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isDriverSeat =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsSeated => isSeated.Value;
    public bool IsDriver => isDriverSeat.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();

        if (playerRigidbody != null)
        {
            _defaultUseGravity = playerRigidbody.useGravity;
            _defaultIsKinematic = playerRigidbody.isKinematic;
        }
        
        if (playerNetworkTransform == null)
            playerNetworkTransform = GetComponent<NetworkTransform>();
        
        if (helicopterSeatState == null)
            helicopterSeatState = GetComponent<PlayerHelicopterSeatStateLan>();

        CacheNetworkTransformDefaults();
        SetNetworkTransformSeatMode(isSeated.Value);
        
        _playerColliders = GetComponentsInChildren<Collider>(true);

        isSeated.OnValueChanged += OnSeatedChanged;
        isDriverSeat.OnValueChanged += OnDriverChanged;

        ApplySeatState(isSeated.Value, isDriverSeat.Value);
    }

    public override void OnNetworkDespawn()
    {
        isSeated.OnValueChanged -= OnSeatedChanged;
        isDriverSeat.OnValueChanged -= OnDriverChanged;
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (PauseMenuManager.IsGamePaused)
            return;
        
        // If the player is currently seated in a helicopter, this car script should not
// control prompts, input, movement lock, or nearby car state.
        if (helicopterSeatState != null && helicopterSeatState.IsSeated)
        {
            _nearbyVehicle = null;
            _nearbySeatIndex = -1;
            _nearbySeatIsDriver = false;

            HidePrompt();
            return;
        }

        if (IsSeated)
        {
            ShowPrompt("Press C to exit");

            if (Input.GetKeyDown(KeyCode.C) && _currentVehicle != null)
            {
                _currentVehicle.RequestExitSeatServerRpc();
                return;
            }

            // Only driver sends car movement input
            if (IsDriver && _currentVehicle != null)
            {
                float throttle = Input.GetAxisRaw("Vertical");
                float steer = Input.GetAxisRaw("Horizontal");

                _currentVehicle.SetDriverInputServerRpc(throttle, steer);
            }

            return;
        }

        if (_nearbyVehicle != null)
        {
            ShowPrompt(_nearbySeatIsDriver ? "Press X to drive" : "Press X to sit");

            if (Input.GetKeyDown(KeyCode.X))
            {
                _currentVehicle = _nearbyVehicle;
                _currentVehicle.RequestEnterSeatServerRpc(_nearbySeatIndex);
            }
        }
        else
        {
            HidePrompt();
        }
    }

    public void SetNearbySeat(LanVehicleSeatManager vehicle, int seatIndex, bool isDriver)
    {
        if (!IsOwner)
            return;

        if (IsSeated)
            return;

        if (helicopterSeatState == null)
            helicopterSeatState = GetComponent<PlayerHelicopterSeatStateLan>();

        if (helicopterSeatState != null && helicopterSeatState.IsSeated)
            return;

        _nearbyVehicle = vehicle;
        _nearbySeatIndex = seatIndex;
        _nearbySeatIsDriver = isDriver;
    }

    public void ClearNearbySeat(LanVehicleSeatManager vehicle, int seatIndex)
    {
        if (!IsOwner)
            return;

        if (_nearbyVehicle == vehicle && _nearbySeatIndex == seatIndex)
        {
            _nearbyVehicle = null;
            _nearbySeatIndex = -1;
            _nearbySeatIsDriver = false;

            if (!IsSeated)
                HidePrompt();
        }
    }

    public void SetCurrentVehicleReference(LanVehicleSeatManager vehicle)
    {
        if (_currentVehicle == vehicle)
            return;

        if (_currentVehicle != null)
            SetVehicleCollisionIgnore(_currentVehicle, false);

        _currentVehicle = vehicle;

        if (_currentVehicle != null && isSeated.Value)
            SetVehicleCollisionIgnore(_currentVehicle, true);
    }

    public void ClearCurrentVehicleReference(LanVehicleSeatManager vehicle)
    {
        if (_currentVehicle != vehicle)
            return;

        SetVehicleCollisionIgnore(_currentVehicle, false);
        _currentVehicle = null;
    }

    // Called by the vehicle script on the SERVER
    public void ServerSetVehicleState(bool seated, bool driverSeatFlag)
    {
        if (!IsServer)
            return;

        isSeated.Value = seated;
        isDriverSeat.Value = driverSeatFlag;
    }

    private void OnSeatedChanged(bool oldValue, bool newValue)
    {
        ApplySeatState(newValue, isDriverSeat.Value);

        if (!newValue)
        {
            HidePrompt();
        }
    }

    private void OnDriverChanged(bool oldValue, bool newValue)
    {
        ApplySeatState(isSeated.Value, newValue);
    }

    private void ApplySeatState(bool seated, bool driverSeatFlag)
    {
        // Movement lock only matters for owner
        if (IsOwner)
        {
            if (movement != null)
                movement.SetMovementLocked(seated);

            if (mouseLook != null)
                mouseLook.SetCharacterBodyRotationLocked(seated);
        }
        
        SetNetworkTransformSeatMode(seated);

        // Make seated player stable inside moving vehicle
        if (playerRigidbody != null)
        {
            if (seated)
            {
                if (!playerRigidbody.isKinematic)
                {
                    playerRigidbody.linearVelocity = Vector3.zero;
                    playerRigidbody.angularVelocity = Vector3.zero;
                }

                playerRigidbody.useGravity = false;
                playerRigidbody.isKinematic = true;
            }
            else
            {
                playerRigidbody.useGravity = _defaultUseGravity;
                playerRigidbody.isKinematic = _defaultIsKinematic;

                if (!playerRigidbody.isKinematic)
                {
                    playerRigidbody.linearVelocity = Vector3.zero;
                    playerRigidbody.angularVelocity = Vector3.zero;
                }
            }
        }
        
        if (_currentVehicle != null)
            SetVehicleCollisionIgnore(_currentVehicle, seated);

        bool flagCarrier = IsThisPlayerCTFFlagCarrier();

        if (IsOwner && fpWeaponSwitcherRoot != null)
        {
            bool showFPWeapons = true;

                if (seated && driverSeatFlag)
                showFPWeapons = false;

            if (flagCarrier)
                showFPWeapons = false;

            fpWeaponSwitcherRoot.SetActive(showFPWeapons);
        }

        if (tpGunHolderRoot != null)
        {
            bool showTPWeapons = true;

                if (seated && driverSeatFlag)
                showTPWeapons = false;

            if (flagCarrier)
                showTPWeapons = false;

            tpGunHolderRoot.SetActive(showTPWeapons);
        }
    }
    public void ForceVehicleExitStateFromVehicle(Vector3 worldExitPos, Quaternion worldExitRot)
    {
        if (_forceVehicleExitRoutine != null)
        {
            StopCoroutine(_forceVehicleExitRoutine);
            _forceVehicleExitRoutine = null;
        }

        if (playerNetworkTransform == null)
            playerNetworkTransform = GetComponent<NetworkTransform>();

        // Do not disable the component. Just restore normal sync axes.
        if (playerNetworkTransform != null)
            playerNetworkTransform.enabled = true;

        SetNetworkTransformSeatMode(false);

        _forceVehicleExitRoutine = StartCoroutine(ForceVehicleExitStateRoutine(worldExitPos, worldExitRot));
    }

    private IEnumerator ForceVehicleExitStateRoutine(Vector3 worldExitPos, Quaternion worldExitRot)
    {
        // Snap once only.
        ForceVehicleExitStateOnce(worldExitPos, worldExitRot, true);

        // Keep movement/mouse/NetworkTransform state clean for a few frames.
        // Do not keep snapping position because that can fight falling/movement.
        for (int i = 0; i < 20; i++)
        {
            if (playerNetworkTransform != null)
                playerNetworkTransform.enabled = true;

            SetNetworkTransformSeatMode(false);

            if (movement != null)
                movement.SetMovementLocked(false);

            if (mouseLook != null)
                mouseLook.SetCharacterBodyRotationLocked(false);

            yield return null;
        }

        _forceVehicleExitRoutine = null;
    }

    private void ForceVehicleExitStateOnce(Vector3 worldExitPos, Quaternion worldExitRot, bool snapTransform)
    {
        if (playerNetworkTransform == null)
            playerNetworkTransform = GetComponent<NetworkTransform>();

// Never disable the whole NetworkTransform component.
// Just restore normal sync axes when exiting.
        if (playerNetworkTransform != null)
            playerNetworkTransform.enabled = true;

        SetNetworkTransformSeatMode(false);

        if (snapTransform)
        {
            transform.SetPositionAndRotation(worldExitPos, worldExitRot);
        }

        if (IsOwner)
        {
            if (movement != null)
                movement.SetMovementLocked(false);

            if (mouseLook != null)
                mouseLook.SetCharacterBodyRotationLocked(false);

            HidePrompt();
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.useGravity = _defaultUseGravity;
            playerRigidbody.isKinematic = _defaultIsKinematic;

            if (!playerRigidbody.isKinematic)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }
        }

        if (_currentVehicle != null)
        {
            SetVehicleCollisionIgnore(_currentVehicle, false);
            _currentVehicle = null;
        }
    }
    
    private void CacheNetworkTransformDefaults()
{
    if (_ntDefaultsCached) return;
    if (playerNetworkTransform == null) return;

    _ntPosX = playerNetworkTransform.SyncPositionX;
    _ntPosY = playerNetworkTransform.SyncPositionY;
    _ntPosZ = playerNetworkTransform.SyncPositionZ;

    _ntRotX = playerNetworkTransform.SyncRotAngleX;
    _ntRotY = playerNetworkTransform.SyncRotAngleY;
    _ntRotZ = playerNetworkTransform.SyncRotAngleZ;

    _ntScaleX = playerNetworkTransform.SyncScaleX;
    _ntScaleY = playerNetworkTransform.SyncScaleY;
    _ntScaleZ = playerNetworkTransform.SyncScaleZ;

    _ntDefaultsCached = true;
}

private void SetNetworkTransformSeatMode(bool seated)
{
    if (playerNetworkTransform == null)
        return;

    CacheNetworkTransformDefaults();

    // Keep the component enabled. We only change the sync checkboxes.
    playerNetworkTransform.enabled = true;

    if (seated)
    {
        // While parented to the vehicle, do not let this player's NetworkTransform
        // fight the vehicle parent sync.
        playerNetworkTransform.SyncPositionX = false;
        playerNetworkTransform.SyncPositionY = false;
        playerNetworkTransform.SyncPositionZ = false;

        playerNetworkTransform.SyncRotAngleX = false;
        playerNetworkTransform.SyncRotAngleY = false;
        playerNetworkTransform.SyncRotAngleZ = false;

        playerNetworkTransform.SyncScaleX = false;
        playerNetworkTransform.SyncScaleY = false;
        playerNetworkTransform.SyncScaleZ = false;
    }
    else
    {
        // Restore prefab defaults when unseated.
        playerNetworkTransform.SyncPositionX = _ntPosX;
        playerNetworkTransform.SyncPositionY = _ntPosY;
        playerNetworkTransform.SyncPositionZ = _ntPosZ;

        playerNetworkTransform.SyncRotAngleX = _ntRotX;
        playerNetworkTransform.SyncRotAngleY = _ntRotY;
        playerNetworkTransform.SyncRotAngleZ = _ntRotZ;

        playerNetworkTransform.SyncScaleX = _ntScaleX;
        playerNetworkTransform.SyncScaleY = _ntScaleY;
        playerNetworkTransform.SyncScaleZ = _ntScaleZ;
    }
}
    private void SetVehicleCollisionIgnore(LanVehicleSeatManager vehicle, bool ignore)
    {
        if (vehicle == null)
            return;

        if (_playerColliders == null || _playerColliders.Length == 0)
            _playerColliders = GetComponentsInChildren<Collider>(true);

        var vehicleColliders = vehicle.GetComponentsInChildren<Collider>(true);
        if (vehicleColliders == null || vehicleColliders.Length == 0)
            return;

        for (int i = 0; i < _playerColliders.Length; i++)
        {
            var playerCol = _playerColliders[i];
            if (playerCol == null) continue;

            for (int j = 0; j < vehicleColliders.Length; j++)
            {
                var vehicleCol = vehicleColliders[j];
                if (vehicleCol == null) continue;
                if (playerCol == vehicleCol) continue;

                Physics.IgnoreCollision(playerCol, vehicleCol, ignore);
            }
        }
    }

    private void ShowPrompt(string msg)
    {
        SeatPromptOwnerLan.Show(this, vehiclePromptText, msg);
    }

    private void HidePrompt()
    {
        SeatPromptOwnerLan.Hide(this, vehiclePromptText);
    }
    
    private bool IsThisPlayerCTFFlagCarrier()
    {
        if (RoomManagerLan.Instance == null || !RoomManagerLan.Instance.IsCTFMode)
            return false;

        var gm = CTFGameManagerLan.Instance;
        if (gm == null)
            return false;

        ulong clientId = OwnerClientId;

        bool holdingBlue = gm.blueFlag != null && gm.blueFlag.IsHeldBy(clientId);
        bool holdingRed = gm.redFlag != null && gm.redFlag.IsHeldBy(clientId);

        return holdingBlue || holdingRed;
    }
}