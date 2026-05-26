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
    private bool _defaultNetworkTransformEnabled = true;
    private Coroutine _seatNetworkTransformRoutine;

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

        if (playerNetworkTransform != null)
            _defaultNetworkTransformEnabled = playerNetworkTransform.enabled;
        
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
        
        UpdateSeatNetworkTransformState(seated);

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

        // Driver: hide FP weapons locally so they cannot shoot
        if (IsOwner && fpWeaponSwitcherRoot != null)
        {
            fpWeaponSwitcherRoot.SetActive(!(seated && driverSeatFlag));
        }

        // Driver: hide TP guns for everyone
        if (tpGunHolderRoot != null)
        {
            tpGunHolderRoot.SetActive(!driverSeatFlag);
        }
    }
    
    private void UpdateSeatNetworkTransformState(bool seated)
    {
        if (playerNetworkTransform == null)
            return;

        if (_seatNetworkTransformRoutine != null)
        {
            StopCoroutine(_seatNetworkTransformRoutine);
            _seatNetworkTransformRoutine = null;
        }

        if (!seated)
        {
            playerNetworkTransform.enabled = _defaultNetworkTransformEnabled;
            return;
        }

        _seatNetworkTransformRoutine = StartCoroutine(DisableNetworkTransformDelayed());
    }

    private IEnumerator DisableNetworkTransformDelayed()
    {
        // Let parent + seat snap land first
        yield return null;
        yield return null;
        yield return null;

        if (playerNetworkTransform != null)
            playerNetworkTransform.enabled = false;

        _seatNetworkTransformRoutine = null;
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
        if (vehiclePromptText != null)
            vehiclePromptText.text = msg;
    }

    private void HidePrompt()
    {
        if (vehiclePromptText != null)
            vehiclePromptText.text = "";
    }
}