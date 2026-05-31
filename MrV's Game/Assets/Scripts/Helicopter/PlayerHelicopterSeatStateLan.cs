using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Attach to the player root.

[NetworkMode(NetworkMode.LAN)]
public class PlayerHelicopterSeatStateLan : NetworkBehaviour
{
    [Header("References")]
    public Movement movement;
    public MouseLook mouseLook;
    public Rigidbody playerRigidbody;
    public NetworkTransform playerNetworkTransform;

    [Tooltip("Optional. Auto-found if left empty. Used so car and helicopter seat systems do not fight.")]
    public PlayerVehicleSeatStateLan vehicleSeatState;
    
    [Header("Helicopter Mouse Steering")]
    [Tooltip("How much Mouse X turns the helicopter while piloting.")]
    public float helicopterMouseYawSensitivity = 3f;
    [Tooltip("Small dead zone so tiny mouse noise does not slowly turn the helicopter.")]
    public float helicopterMouseYawDeadZone = 0.001f;

    [Header("Weapon Roots")]
    public GameObject fpWeaponSwitcherRoot;
    public GameObject tpGunHolderRoot;

    [Header("Prompt UI")]
    public TMP_Text helicopterPromptText;

    [Header("Controls UI")]
    [Tooltip("Separate TMP text box for helicopter controls.")]
    public TMP_Text helicopterControlsText;

    private LanHelicopterSeatManager _nearbyHelicopter;
    private int _nearbySeatIndex = -1;
    private bool _nearbySeatIsPilot;

    private LanHelicopterSeatManager _currentHelicopter;
    private float _helicopterMouseYawInput;

    private bool _defaultUseGravity = true;
    private bool _defaultIsKinematic = false;
    private Collider[] _playerColliders;
    private bool _defaultNetworkTransformEnabled = true;
    
    private Coroutine _forceExitRoutine;
    
    // --- NetworkTransform axis sync defaults (cached on spawn) ---
    private bool _ntPosX, _ntPosY, _ntPosZ;
    private bool _ntRotX, _ntRotY, _ntRotZ;
    private bool _ntScaleX, _ntScaleY, _ntScaleZ;
    private bool _ntDefaultsCached;

    private readonly NetworkVariable<bool> isSeated =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isPilotSeat =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsSeated => isSeated.Value;
    public bool IsPilot => isPilotSeat.Value;

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
        
        if (vehicleSeatState == null)
            vehicleSeatState = GetComponent<PlayerVehicleSeatStateLan>();
        
        CacheNetworkTransformDefaults();
        SetNetworkTransformSeatMode(isSeated.Value);

        _playerColliders = GetComponentsInChildren<Collider>(true);

        isSeated.OnValueChanged += OnSeatedChanged;
        isPilotSeat.OnValueChanged += OnPilotChanged;

        ApplySeatState(isSeated.Value, isPilotSeat.Value);
    }

    public override void OnNetworkDespawn()
    {
        isSeated.OnValueChanged -= OnSeatedChanged;
        isPilotSeat.OnValueChanged -= OnPilotChanged;

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (PauseMenuManager.IsGamePaused)
            return;
        
        // If the player is currently seated in a car/vehicle, this helicopter script should not
        // control prompts, input, movement lock, or nearby helicopter state.
        if (vehicleSeatState != null && vehicleSeatState.IsSeated)
        {
            _nearbyHelicopter = null;
            _nearbySeatIndex = -1;
            _nearbySeatIsPilot = false;

            HidePrompt();
            HideControls();
            return;
        }

        if (IsSeated)
        {
            ShowPrompt("Press C to exit");

            if (IsPilot)
            {
                ShowControls("Space=Up, Shift=Down. Use W/A/S/D to fly. Move mouse left/right to turn. Q/E fire weapons.");
            }
            else
            {
                HideControls();
            }

            UpdateWeaponVisibility(IsSeated, IsPilot);

            if (Input.GetKeyDown(KeyCode.C) && _currentHelicopter != null)
            {

                _currentHelicopter.RequestExitSeatServerRpc();
                return;
            }

            // Only pilot sends helicopter movement input
            if (IsPilot && _currentHelicopter != null)
            {
                float forward = Input.GetAxisRaw("Vertical");     // W/S
                float strafe = Input.GetAxisRaw("Horizontal");    // A/D

                float collective = 0f;
                if (Input.GetKey(KeyCode.Space))
                    collective = 1f;
                else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    collective = -1f;

                float mouseX = 0f;

// Use the same mouse value that MouseLook is already receiving.
// This avoids a case where this script reads Mouse X differently than the camera script.
                if (mouseLook != null)
                {
                    mouseX = mouseLook.LastRawMouseX;
                }
                else
                {
                    mouseX = Input.GetAxisRaw("Mouse X");
                }

                if (Mathf.Abs(mouseX) > helicopterMouseYawDeadZone)
                {
                    _helicopterMouseYawInput = Mathf.Clamp(
                        mouseX * helicopterMouseYawSensitivity,
                        -1f,
                        1f
                    );
                }
                else
                {
                    _helicopterMouseYawInput = 0f;
                }

                float yaw = _helicopterMouseYawInput;

                _currentHelicopter.SetPilotInputServerRpc(forward, strafe, collective, yaw);

                if (Input.GetKey(KeyCode.Q))
                    _currentHelicopter.RequestFireHelicopterGunServerRpc(0);

                if (Input.GetKey(KeyCode.E))
                    _currentHelicopter.RequestFireHelicopterGunServerRpc(1);
            }

            return;
        }

        HideControls();

        if (_nearbyHelicopter != null)
        {
            ShowPrompt(_nearbySeatIsPilot ? "Press X to fly" : "Press X to sit");

            if (Input.GetKeyDown(KeyCode.X))
            {
                _currentHelicopter = _nearbyHelicopter;
                _currentHelicopter.RequestEnterSeatServerRpc(_nearbySeatIndex);
            }
        }
        else
        {
            HidePrompt();
        }
    }

    public void SetNearbyHelicopterSeat(LanHelicopterSeatManager helicopter, int seatIndex, bool isPilot)
    {
        if (!IsOwner)
            return;

        if (IsSeated)
            return;

        if (vehicleSeatState == null)
            vehicleSeatState = GetComponent<PlayerVehicleSeatStateLan>();

        if (vehicleSeatState != null && vehicleSeatState.IsSeated)
            return;

        _nearbyHelicopter = helicopter;
        _nearbySeatIndex = seatIndex;
        _nearbySeatIsPilot = isPilot;
    }

    public void ClearNearbyHelicopterSeat(LanHelicopterSeatManager helicopter, int seatIndex)
    {
        if (!IsOwner)
            return;

        if (_nearbyHelicopter == helicopter && _nearbySeatIndex == seatIndex)
        {
            _nearbyHelicopter = null;
            _nearbySeatIndex = -1;
            _nearbySeatIsPilot = false;

            if (!IsSeated)
                HidePrompt();
        }
    }

    public void SetCurrentHelicopterReference(LanHelicopterSeatManager helicopter)
    {
        if (_currentHelicopter == helicopter)
            return;

        if (_currentHelicopter != null)
            SetHelicopterCollisionIgnore(_currentHelicopter, false);

        _currentHelicopter = helicopter;

        if (_currentHelicopter != null && isSeated.Value)
            SetHelicopterCollisionIgnore(_currentHelicopter, true);
    }

    public void ClearCurrentHelicopterReference(LanHelicopterSeatManager helicopter)
    {
        if (_currentHelicopter != helicopter)
            return;

        SetHelicopterCollisionIgnore(_currentHelicopter, false);
        _currentHelicopter = null;
    }

    public void ServerSetHelicopterSeatState(bool seated, bool pilotSeatFlag)
    {
        if (!IsServer)
            return;

        isSeated.Value = seated;
        isPilotSeat.Value = pilotSeatFlag;
    }

    private void OnSeatedChanged(bool oldValue, bool newValue)
    {
        ApplySeatState(newValue, isPilotSeat.Value);

        if (!newValue)
        {
            HidePrompt();
            HideControls();
        }
    }

    private void OnPilotChanged(bool oldValue, bool newValue)
    {
        ApplySeatState(isSeated.Value, newValue);
    }

    private void ApplySeatState(bool seated, bool pilotSeatFlag)
    {
        if (IsOwner)
        {
            if (movement != null)
                movement.SetMovementLocked(seated);

            if (mouseLook != null)
                mouseLook.SetCharacterBodyRotationLocked(seated);
        }

        SetNetworkTransformSeatMode(seated);

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

        if (_currentHelicopter != null)
            SetHelicopterCollisionIgnore(_currentHelicopter, seated);

        UpdateWeaponVisibility(seated, pilotSeatFlag);
    }

    private void UpdateWeaponVisibility(bool seated, bool pilotSeatFlag)
    {
        bool flagCarrier = IsThisPlayerCTFFlagCarrier();

        if (IsOwner && fpWeaponSwitcherRoot != null)
        {
            bool showFPWeapons = true;

            if (seated && pilotSeatFlag)
                showFPWeapons = false;

            if (flagCarrier)
                showFPWeapons = false;

            fpWeaponSwitcherRoot.SetActive(showFPWeapons);
        }

        if (tpGunHolderRoot != null)
        {
            bool showTPWeapons = true;

            if (seated && pilotSeatFlag)
                showTPWeapons = false;

            if (flagCarrier)
                showTPWeapons = false;

            tpGunHolderRoot.SetActive(showTPWeapons);
        }
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

    public void ForceHelicopterExitStateFromHelicopter(Vector3 worldExitPos, Quaternion worldExitRot)
    {
        if (_forceExitRoutine != null)
        {
            StopCoroutine(_forceExitRoutine);
            _forceExitRoutine = null;
        }

        if (playerNetworkTransform == null)
            playerNetworkTransform = GetComponent<NetworkTransform>();

        _forceExitRoutine = StartCoroutine(ForceExitRoutine(worldExitPos, worldExitRot));
        
    }

    private IEnumerator ForceExitRoutine(Vector3 worldExitPos, Quaternion worldExitRot)
    {
        // Snap once only.
        ForceExitStateOnce(worldExitPos, worldExitRot, true);

        // After the first snap, do NOT keep calling ForceExitStateOnce.
        // That method resets Rigidbody velocity, which can fight gravity/movement.
        for (int i = 0; i < 20; i++)
        {

            if (movement != null)
                movement.SetMovementLocked(false);

            if (mouseLook != null)
                mouseLook.SetCharacterBodyRotationLocked(false);

            yield return null;
        }

        _forceExitRoutine = null;
    }

    private void ForceExitStateOnce(Vector3 worldExitPos, Quaternion worldExitRot, bool snapTransform)
    {
        if (playerNetworkTransform == null)
            playerNetworkTransform = GetComponent<NetworkTransform>();

        if (snapTransform)
        {
            transform.SetPositionAndRotation(worldExitPos, worldExitRot);
        }

        if (IsOwner)
        {
            _helicopterMouseYawInput = 0f;

            if (movement != null)
                movement.SetMovementLocked(false);

            if (mouseLook != null)
                mouseLook.SetCharacterBodyRotationLocked(false);

            HidePrompt();
            HideControls();
        }

        // Only the owner should reset their Rigidbody.
// Non-owner/server observer copies must not keep killing velocity.
        if (playerRigidbody != null)
        {
            playerRigidbody.useGravity = _defaultUseGravity;
            playerRigidbody.isKinematic = _defaultIsKinematic;

            // Only clear velocity on the first snap frame.
            // After that, gravity/falling/movement must be allowed to continue.
            if (snapTransform && !playerRigidbody.isKinematic)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }
        }

        if (_currentHelicopter != null)
        {
            SetHelicopterCollisionIgnore(_currentHelicopter, false);
            _currentHelicopter = null;
        }

        UpdateWeaponVisibility(false, false);
    }
    private void SetHelicopterCollisionIgnore(LanHelicopterSeatManager helicopter, bool ignore)
    {
        if (helicopter == null)
            return;

        if (_playerColliders == null || _playerColliders.Length == 0)
            _playerColliders = GetComponentsInChildren<Collider>(true);

        var helicopterColliders = helicopter.GetComponentsInChildren<Collider>(true);
        if (helicopterColliders == null || helicopterColliders.Length == 0)
            return;

        for (int i = 0; i < _playerColliders.Length; i++)
        {
            var playerCol = _playerColliders[i];
            if (playerCol == null) continue;

            for (int j = 0; j < helicopterColliders.Length; j++)
            {
                var heliCol = helicopterColliders[j];
                if (heliCol == null) continue;
                if (playerCol == heliCol) continue;

                Physics.IgnoreCollision(playerCol, heliCol, ignore);
            }
        }
    }

    private void ShowPrompt(string msg)
    {
        SeatPromptOwnerLan.Show(this, helicopterPromptText, msg);
    }

    private void HidePrompt()
    {
        SeatPromptOwnerLan.Hide(this, helicopterPromptText);
    }

    private void ShowControls(string msg)
    {
        if (helicopterControlsText != null)
            helicopterControlsText.text = msg;
    }

    private void HideControls()
    {
        if (helicopterControlsText != null)
            helicopterControlsText.text = "";
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
    if (playerNetworkTransform == null) return;

    CacheNetworkTransformDefaults();

    if (seated)
    {
        // While parented to helicopter, we do NOT want the player's NT fighting the parent.
        playerNetworkTransform.SyncPositionX = false;
        playerNetworkTransform.SyncPositionY = false;
        playerNetworkTransform.SyncPositionZ = false;

        playerNetworkTransform.SyncRotAngleX = false;
        playerNetworkTransform.SyncRotAngleY = false;
        playerNetworkTransform.SyncRotAngleZ = false;

        // scale usually irrelevant, but safest off while seated
        playerNetworkTransform.SyncScaleX = false;
        playerNetworkTransform.SyncScaleY = false;
        playerNetworkTransform.SyncScaleZ = false;
    }
    else
    {
        // Restore prefab defaults when unseated
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

}