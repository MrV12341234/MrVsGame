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

    private bool _defaultUseGravity = true;
    private bool _defaultIsKinematic = false;
    private Collider[] _playerColliders;
    private bool _defaultNetworkTransformEnabled = true;

    private Coroutine _seatNetworkTransformRoutine;
    private Coroutine _forceExitRoutine;

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

        if (playerNetworkTransform != null)
            _defaultNetworkTransformEnabled = playerNetworkTransform.enabled;

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

        if (IsSeated)
        {
            ShowPrompt("Press C to exit");

            if (IsPilot)
            {
                ShowControls("Use W,A,S,D with left hand and arrow keys with right hand. Q and E fire weapons.");
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

            if (IsPilot && _currentHelicopter != null)
            {
                float forward = 0f;
                if (Input.GetKey(KeyCode.W)) forward += 1f;
                if (Input.GetKey(KeyCode.S)) forward -= 1f;

                float strafe = 0f;
                if (Input.GetKey(KeyCode.D)) strafe += 1f;
                if (Input.GetKey(KeyCode.A)) strafe -= 1f;

                float collective = 0f;
                if (Input.GetKey(KeyCode.UpArrow)) collective += 1f;
                if (Input.GetKey(KeyCode.DownArrow)) collective -= 1f;

                float yaw = 0f;
                if (Input.GetKey(KeyCode.RightArrow)) yaw += 1f;
                if (Input.GetKey(KeyCode.LeftArrow)) yaw -= 1f;

                _currentHelicopter.SetPilotInputServerRpc(forward, strafe, collective, yaw);

                if (Input.GetKeyDown(KeyCode.Q))
                    _currentHelicopter.RequestFireHelicopterGunServerRpc(0);

                if (Input.GetKeyDown(KeyCode.E))
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

    private void LateUpdate()
    {
        if (!isSeated.Value &&
            playerNetworkTransform != null &&
            !playerNetworkTransform.enabled &&
            transform.parent == null)
        {
            playerNetworkTransform.enabled = true;
        }
    }

    public void SetNearbyHelicopterSeat(LanHelicopterSeatManager helicopter, int seatIndex, bool isPilot)
    {
        if (!IsOwner)
            return;

        if (IsSeated)
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

        UpdateSeatNetworkTransformState(seated);

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
            return;

        _seatNetworkTransformRoutine = StartCoroutine(DisableNetworkTransformDelayed());
    }

    private IEnumerator DisableNetworkTransformDelayed()
    {
        yield return null;
        yield return null;
        yield return null;

        if (!isSeated.Value)
        {
            if (playerNetworkTransform != null)
                playerNetworkTransform.enabled = true;

            _seatNetworkTransformRoutine = null;
            yield break;
        }

        if (playerNetworkTransform != null)
            playerNetworkTransform.enabled = false;

        _seatNetworkTransformRoutine = null;
    }

    public void ForceHelicopterExitStateFromHelicopter(Vector3 worldExitPos, Quaternion worldExitRot)
    {
        if (_forceExitRoutine != null)
        {
            StopCoroutine(_forceExitRoutine);
            _forceExitRoutine = null;
        }

        _forceExitRoutine = StartCoroutine(ForceExitRoutine(worldExitPos, worldExitRot));
    }

    private IEnumerator ForceExitRoutine(Vector3 worldExitPos, Quaternion worldExitRot)
    {
        ForceExitStateOnce(worldExitPos, worldExitRot, true);
        yield return null;

        float timeout = 2f;
        float start = Time.time;

        while (transform.parent != null && Time.time - start < timeout)
        {
            yield return null;
        }

        if (playerNetworkTransform != null)
            playerNetworkTransform.enabled = true;

        for (int i = 0; i < 5; i++)
        {
            if (playerNetworkTransform != null && !playerNetworkTransform.enabled)
                playerNetworkTransform.enabled = true;

            yield return null;
        }

        _forceExitRoutine = null;
    }

    private void ForceExitStateOnce(Vector3 worldExitPos, Quaternion worldExitRot, bool snapTransform)
    {
        if (_seatNetworkTransformRoutine != null)
        {
            StopCoroutine(_seatNetworkTransformRoutine);
            _seatNetworkTransformRoutine = null;
        }

        if (playerNetworkTransform == null)
            playerNetworkTransform = GetComponent<NetworkTransform>();

        if (playerNetworkTransform != null && snapTransform)
        {
            playerNetworkTransform.enabled = false;
            transform.SetPositionAndRotation(worldExitPos, worldExitRot);
        }

        if (IsOwner)
        {
            if (movement != null)
                movement.SetMovementLocked(false);

            if (mouseLook != null)
                mouseLook.SetCharacterBodyRotationLocked(false);

            HidePrompt();
            HideControls();
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
        if (helicopterPromptText != null)
            helicopterPromptText.text = msg;
    }

    private void HidePrompt()
    {
        if (helicopterPromptText != null)
            helicopterPromptText.text = "";
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
}