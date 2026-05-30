using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[NetworkMode(NetworkMode.LAN)]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class LanHelicopterSeatManager : NetworkBehaviour
{
    [Header("Seats")]
    public Transform[] seatPoints;
    public LanHelicopterSeatTrigger[] seatTriggers;
    public int pilotSeatIndex = 0;

    [Header("Exit")]
    [Tooltip("How high above the seat point the player appears when exiting.")]
    public float exitUpOffset = 2f;

    [Header("Flight Movement")]
    public Rigidbody helicopterRigidbody;
    public float maxForwardSpeed = 22f;
    public float maxStrafeSpeed = 16f;
    public float climbSpeed = 12f;
    public float acceleration = 18f;
    public float yawDegreesPerSecond = 80f;
    
    [Header("Flight Stability")]
    [Tooltip("When true, the helicopter Rigidbody root stays level while flying. Visual tilt is still handled by helicopterVisualRoot.")]
    public bool keepRootLevelWhileFlying = true;
    [Tooltip("How fast the Rigidbody root corrects back to level if physics/collisions tilt it.")]
    public float levelCorrectionSpeed = 12f;
    [Tooltip("When true, physics cannot roll/pitch the helicopter while a pilot is flying.")]
    public bool freezePitchRollWhileFlying = true;

    [Header("Ground / Parking")]
    public bool lockHelicopterWhenParked = true;
    public float groundedRayLength = 2.2f;
    public LayerMask groundMask = ~0;
    public float landingVerticalSpeedTolerance = 2.5f;

    [Header("Visual Tilt")]
    [Tooltip("Recommended: assign a visual/model child, not the network root. Leave empty for no visual tilt.")]
    public Transform helicopterVisualRoot;
    public float maxForwardTilt = 12f;
    public float maxSideTilt = 14f;
    public float tiltLerpSpeed = 6f;

    [Header("Rotors")]
    public Transform mainRotor;
    public Transform tailRotor;
    public float mainRotorSpinDegreesPerSecond = 1800f;
    public float tailRotorSpinDegreesPerSecond = 2200f;

    [Header("Helicopter Guns")]
    public NetworkObject potatoProjectilePrefab;
    public Transform leftGunMuzzle;
    public Transform rightGunMuzzle;
    public float gunFireRate = 10f;
    
    [Header("Helicopter Gun Projectile Tuning")]
    [Tooltip("Projectile force for helicopter potatoes. Same idea as LauncherProjectileLAN shootForce.")]
    public float helicopterGunShootForce = 1000f;
    [Tooltip("0 = straight from muzzle. 0.5 = upward arc like handheld launcher. Negative = downward arc.")]
    public float helicopterGunArcHeightMultiplier = 0f;
    [Tooltip("Extra local pitch angle for the projectile. Positive usually angles the shot downward in Unity.")]
    public float helicopterGunPitchOffsetDegrees = 0f;

    private NetworkList<ulong> seatOccupants;

    private readonly NetworkVariable<bool> syncedRotorsActive =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> syncedForwardInput =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> syncedStrafeInput =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float _serverForward;
    private float _serverStrafe;
    private float _serverCollective;
    private float _serverYaw;

    private bool _isParked = true;
    private bool _isFlying = false;

    private float _mainRotorAngle;
    private float _tailRotorAngle;

    private float _nextLeftFireTime;
    private float _nextRightFireTime;

    private void Awake()
    {
        seatOccupants = new NetworkList<ulong>();

        if (helicopterRigidbody == null)
            helicopterRigidbody = GetComponent<Rigidbody>();
    }

    private void OnValidate()
    {
        if (helicopterRigidbody == null)
            helicopterRigidbody = GetComponent<Rigidbody>();

        AutoWireTriggers();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        AutoWireTriggers();

        seatOccupants.OnListChanged += OnSeatOccupantsChanged;

        if (IsServer)
        {
            EnsureSeatListSize();

            if (NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback += OnAnyClientDisconnected;

            ParkHelicopter();
        }

        RefreshSeatTriggerStates();
    }

    public override void OnNetworkDespawn()
    {
        seatOccupants.OnListChanged -= OnSeatOccupantsChanged;

        if (IsServer && NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= OnAnyClientDisconnected;

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        UpdateRotorVisuals();
        UpdateTiltVisuals();
    }

    private void FixedUpdate()
    {
        if (!IsServer)
            return;

        CleanupInvalidOccupants();

        bool hasPilot = IsPilotSeatOccupied();
        bool grounded = IsGrounded();

        syncedRotorsActive.Value = hasPilot;

        if (!hasPilot)
        {
            _serverForward = 0f;
            _serverStrafe = 0f;
            _serverCollective = 0f;
            _serverYaw = 0f;

            syncedForwardInput.Value = 0f;
            syncedStrafeInput.Value = 0f;

            if (grounded)
            {
                ParkHelicopter();
            }
            else
            {
                LetHelicopterFall();
            }

            return;
        }

        if (_isParked)
        {
            if (_serverCollective > 0.05f)
            {
                UnparkForFlight();
            }
            else
            {
                ParkHelicopter();
                syncedForwardInput.Value = 0f;
                syncedStrafeInput.Value = 0f;
                return;
            }
        }

        RunFlightMovement(Time.fixedDeltaTime);

        if (grounded && _serverCollective < -0.1f)
        {
            float verticalSpeed = Mathf.Abs(helicopterRigidbody.linearVelocity.y);

            if (verticalSpeed <= landingVerticalSpeedTolerance)
            {
                ParkHelicopter();
            }
        }

        syncedForwardInput.Value = _serverForward;
        syncedStrafeInput.Value = _serverStrafe;
    }
    
    private Quaternion GetLevelYawRotation(Quaternion currentRotation)
    {
        Vector3 flatForward = currentRotation * Vector3.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = transform.forward;

        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;

        flatForward.Normalize();

        return Quaternion.LookRotation(flatForward, Vector3.up);
    }

    private void RunFlightMovement(float deltaTime)
{
    if (helicopterRigidbody == null)
        return;

    _isFlying = true;
    _isParked = false;

    helicopterRigidbody.isKinematic = false;
    helicopterRigidbody.useGravity = false;

    if (freezePitchRollWhileFlying)
    {
        // Allow yaw, but block physics from tipping the helicopter forward/back/sideways.
        helicopterRigidbody.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;
    }
    else
    {
        helicopterRigidbody.constraints = RigidbodyConstraints.None;
    }

    // Kill physics-added pitch/roll spin. We drive yaw manually.
    Vector3 angularVelocity = helicopterRigidbody.angularVelocity;
    helicopterRigidbody.angularVelocity = new Vector3(0f, angularVelocity.y, 0f);

    Quaternion levelYawRotation = keepRootLevelWhileFlying
        ? GetLevelYawRotation(helicopterRigidbody.rotation)
        : helicopterRigidbody.rotation;

    Quaternion yawRotation =
        levelYawRotation *
        Quaternion.Euler(0f, _serverYaw * yawDegreesPerSecond * deltaTime, 0f);

    Quaternion nextRotation = keepRootLevelWhileFlying
        ? Quaternion.Slerp(helicopterRigidbody.rotation, yawRotation, levelCorrectionSpeed * deltaTime)
        : yawRotation;

    // Important: force the final rotation to be level.
    if (keepRootLevelWhileFlying)
        nextRotation = GetLevelYawRotation(nextRotation);

    helicopterRigidbody.MoveRotation(nextRotation);

    // Use the level yaw rotation for movement, not the tilted visual model.
    Vector3 forward = nextRotation * Vector3.forward;
    Vector3 right = nextRotation * Vector3.right;

    Vector3 desiredVelocity =
        forward * (_serverForward * maxForwardSpeed) +
        right * (_serverStrafe * maxStrafeSpeed) +
        Vector3.up * (_serverCollective * climbSpeed);

    Vector3 nextVelocity = Vector3.MoveTowards(
        helicopterRigidbody.linearVelocity,
        desiredVelocity,
        acceleration * deltaTime
    );

    helicopterRigidbody.linearVelocity = nextVelocity;
}

    private bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.25f;

        return Physics.Raycast(
            origin,
            Vector3.down,
            groundedRayLength,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private void ParkHelicopter()
    {
        if (helicopterRigidbody == null)
            return;

        _isParked = true;
        _isFlying = false;

        helicopterRigidbody.isKinematic = false;
        helicopterRigidbody.useGravity = false;

        helicopterRigidbody.linearVelocity = Vector3.zero;
        helicopterRigidbody.angularVelocity = Vector3.zero;

        if (lockHelicopterWhenParked)
        {
            helicopterRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    private void UnparkForFlight()
    {
        if (helicopterRigidbody == null)
            return;

        _isParked = false;
        _isFlying = true;

        helicopterRigidbody.isKinematic = false;
        helicopterRigidbody.useGravity = false;

        if (freezePitchRollWhileFlying)
        {
            helicopterRigidbody.constraints =
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
        }
        else
        {
            helicopterRigidbody.constraints = RigidbodyConstraints.None;
        }

        // Immediately clean up any bad pitch/roll from sitting on the ground or bumping something.
        if (keepRootLevelWhileFlying)
        {
            Quaternion levelRotation = GetLevelYawRotation(helicopterRigidbody.rotation);
            helicopterRigidbody.MoveRotation(levelRotation);
        }

        Vector3 av = helicopterRigidbody.angularVelocity;
        helicopterRigidbody.angularVelocity = new Vector3(0f, av.y, 0f);
    }

    private void LetHelicopterFall()
    {
        if (helicopterRigidbody == null)
            return;

        _isParked = false;
        _isFlying = false;

        helicopterRigidbody.isKinematic = false;
        helicopterRigidbody.useGravity = true;
        helicopterRigidbody.constraints = RigidbodyConstraints.None;
    }

    private void UpdateRotorVisuals()
    {
        if (!syncedRotorsActive.Value)
            return;

        _mainRotorAngle += mainRotorSpinDegreesPerSecond * Time.deltaTime;
        _tailRotorAngle += tailRotorSpinDegreesPerSecond * Time.deltaTime;

        if (mainRotor != null)
            mainRotor.localRotation = Quaternion.Euler(0f, _mainRotorAngle, 0f);

        if (tailRotor != null)
            tailRotor.localRotation = Quaternion.Euler(_tailRotorAngle, 0f, 0f);
    }

    private void UpdateTiltVisuals()
    {
        if (helicopterVisualRoot == null)
            return;

        float pitch = -syncedForwardInput.Value * maxForwardTilt;
        float roll = -syncedStrafeInput.Value * maxSideTilt;

        Quaternion target = Quaternion.Euler(pitch, 0f, roll);

        helicopterVisualRoot.localRotation = Quaternion.Slerp(
            helicopterVisualRoot.localRotation,
            target,
            tiltLerpSpeed * Time.deltaTime
        );
    }

    private void AutoWireTriggers()
    {
        if (seatTriggers == null)
            return;

        for (int i = 0; i < seatTriggers.Length; i++)
        {
            if (seatTriggers[i] != null)
                seatTriggers[i].Configure(this, i, i == pilotSeatIndex);
        }
    }

    private void RefreshSeatTriggerStates()
    {
        if (seatTriggers == null)
            return;

        for (int i = 0; i < seatTriggers.Length; i++)
        {
            if (seatTriggers[i] == null)
                continue;

            seatTriggers[i].SetSeatInteractable(!IsSeatOccupied(i));
        }
    }

    private void EnsureSeatListSize()
    {
        if (seatPoints == null)
            return;

        while (seatOccupants.Count < seatPoints.Length)
            seatOccupants.Add(ulong.MaxValue);

        while (seatOccupants.Count > seatPoints.Length)
            seatOccupants.RemoveAt(seatOccupants.Count - 1);
    }

    private bool IsPilotSeatOccupied()
    {
        if (pilotSeatIndex < 0 || pilotSeatIndex >= seatOccupants.Count)
            return false;

        return seatOccupants[pilotSeatIndex] != ulong.MaxValue;
    }

    public bool IsSeatOccupied(int seatIndex)
    {
        if (seatOccupants == null)
            return false;

        if (seatIndex < 0 || seatIndex >= seatOccupants.Count)
            return false;

        return seatOccupants[seatIndex] != ulong.MaxValue;
    }

    private ulong GetPilotClientId()
    {
        if (!IsPilotSeatOccupied())
            return ulong.MaxValue;

        return seatOccupants[pilotSeatIndex];
    }

    private int FindSeatForClient(ulong clientId)
    {
        for (int i = 0; i < seatOccupants.Count; i++)
        {
            if (seatOccupants[i] == clientId)
                return i;
        }

        return -1;
    }

    private void OnSeatOccupantsChanged(NetworkListEvent<ulong> changeEvent)
    {
        RefreshSeatTriggerStates();
    }

    private void OnAnyClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        int seatIndex = FindSeatForClient(clientId);
        if (seatIndex >= 0)
            ClearSeatServer(seatIndex, false);
    }

    private void CleanupInvalidOccupants()
    {
        for (int i = 0; i < seatOccupants.Count; i++)
        {
            ulong occupantId = seatOccupants[i];
            if (occupantId == ulong.MaxValue)
                continue;

            if (NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(occupantId, out var client) ||
                client.PlayerObject == null)
            {
                ClearSeatServer(i, false);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEnterSeatServerRpc(int seatIndex, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        if (seatPoints == null || seatIndex < 0 || seatIndex >= seatPoints.Length)
            return;

        EnsureSeatListSize();

        if (seatOccupants[seatIndex] != ulong.MaxValue)
            return;

        ulong clientId = rpcParams.Receive.SenderClientId;

        if (FindSeatForClient(clientId) >= 0)
            return;

        if (NetworkManager == null ||
            !NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) ||
            client.PlayerObject == null)
        {
            return;
        }

        Transform seatTransform = seatPoints[seatIndex];
        if (seatTransform == null)
            return;

        if (seatTriggers != null && seatIndex < seatTriggers.Length && seatTriggers[seatIndex] != null)
        {
            float dist = Vector3.Distance(client.PlayerObject.transform.position, seatTriggers[seatIndex].transform.position);
            if (dist > 6f)
                return;
        }

        NetworkObject playerNO = client.PlayerObject;
        var seatState = playerNO.GetComponent<PlayerHelicopterSeatStateLan>();
        if (seatState == null)
            return;

        seatOccupants[seatIndex] = clientId;

        seatState.SetCurrentHelicopterReference(this);

        var rb = playerNO.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        playerNO.TrySetParent(NetworkObject, true);

        playerNO.transform.position = seatTransform.position;
        playerNO.transform.rotation = seatTransform.rotation;

        bool isPilot = seatIndex == pilotSeatIndex;
        seatState.ServerSetHelicopterSeatState(true, isPilot);

        TargetSetCurrentHelicopterClientRpc(NetworkObject, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        });

        SnapPlayerIntoSeatClientRpc(playerNO, seatIndex);

        if (isPilot)
        {
            _serverForward = 0f;
            _serverStrafe = 0f;
            _serverCollective = 0f;
            _serverYaw = 0f;
        }

        RefreshSeatTriggerStates();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestExitSeatServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        int seatIndex = FindSeatForClient(clientId);

        if (seatIndex < 0)
            return;

        ClearSeatServer(seatIndex, true);
    }

    private void ClearSeatServer(int seatIndex, bool placeAboveSeat)
    {
        if (!IsServer)
            return;

        if (seatIndex < 0 || seatIndex >= seatOccupants.Count)
            return;

        ulong clientId = seatOccupants[seatIndex];
        if (clientId == ulong.MaxValue)
            return;

        if (NetworkManager != null &&
            NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) &&
            client.PlayerObject != null)
        {
            NetworkObject playerNO = client.PlayerObject;
            var seatState = playerNO.GetComponent<PlayerHelicopterSeatStateLan>();

            Transform seatTransform = null;
            if (seatPoints != null && seatIndex >= 0 && seatIndex < seatPoints.Length)
                seatTransform = seatPoints[seatIndex];

            Vector3 finalExitPos = playerNO.transform.position;
            Quaternion finalExitRot = Quaternion.LookRotation(transform.forward, Vector3.up);

            if (placeAboveSeat)
            {
                Vector3 baseExitPos = seatTransform != null
                    ? seatTransform.position
                    : playerNO.transform.position;

                finalExitPos = baseExitPos + transform.up * exitUpOffset;
            }

            playerNO.TryRemoveParent(true);
            playerNO.transform.SetPositionAndRotation(finalExitPos, finalExitRot);

            var rb = playerNO.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (seatState != null)
            {
                seatState.ServerSetHelicopterSeatState(false, false);
                seatState.ClearCurrentHelicopterReference(this);

                seatState.ForceHelicopterExitStateFromHelicopter(finalExitPos, finalExitRot);

                ForcePlayerHelicopterExitStateClientRpc(playerNO, finalExitPos, finalExitRot);

                TargetClearCurrentHelicopterClientRpc(NetworkObject, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { clientId }
                    }
                });
            }
        }

        seatOccupants[seatIndex] = ulong.MaxValue;

        if (seatIndex == pilotSeatIndex)
        {
            _serverForward = 0f;
            _serverStrafe = 0f;
            _serverCollective = 0f;
            _serverYaw = 0f;
        }

        RefreshSeatTriggerStates();

        if (!IsPilotSeatOccupied())
        {
            if (IsGrounded())
                ParkHelicopter();
            else
                LetHelicopterFall();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetPilotInputServerRpc(float forward, float strafe, float collective, float yaw, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        if (pilotSeatIndex < 0 || pilotSeatIndex >= seatOccupants.Count)
            return;

        if (seatOccupants[pilotSeatIndex] != senderId)
            return;

        _serverForward = Mathf.Clamp(forward, -1f, 1f);
        _serverStrafe = Mathf.Clamp(strafe, -1f, 1f);
        _serverCollective = Mathf.Clamp(collective, -1f, 1f);
        _serverYaw = Mathf.Clamp(yaw, -1f, 1f);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestFireHelicopterGunServerRpc(int gunIndex, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        if (GetPilotClientId() != senderId)
            return;

        if (potatoProjectilePrefab == null)
            return;

        Transform muzzle = gunIndex == 0 ? leftGunMuzzle : rightGunMuzzle;
        if (muzzle == null)
            return;

        float nextAllowed = gunIndex == 0 ? _nextLeftFireTime : _nextRightFireTime;
        if (Time.time < nextAllowed)
            return;

        float cooldown = gunFireRate > 0.01f ? 1f / gunFireRate : 0.1f;

        if (gunIndex == 0)
            _nextLeftFireTime = Time.time + cooldown;
        else
            _nextRightFireTime = Time.time + cooldown;

        Quaternion projectileRotation =
            muzzle.rotation * Quaternion.Euler(helicopterGunPitchOffsetDegrees, 0f, 0f);

        NetworkObject projectileNO = Instantiate(
            potatoProjectilePrefab,
            muzzle.position,
            projectileRotation
        );

        GameObject ownerPlayer = null;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var client) && client.PlayerObject != null)
            ownerPlayer = client.PlayerObject.gameObject;

        var projectile = projectileNO.GetComponent<LauncherProjectileLAN>();
        if (projectile != null)
        {
            projectile.shootForce = helicopterGunShootForce;
            projectile.arcHeightMultiplier = helicopterGunArcHeightMultiplier;
            projectile.SetOwner(senderId, ownerPlayer);
        }

        projectileNO.Spawn(true);
    }

    [ClientRpc]
    private void TargetSetCurrentHelicopterClientRpc(NetworkObjectReference helicopterRef, ClientRpcParams clientRpcParams = default)
    {
        if (!NetworkManager || !NetworkManager.LocalClient?.PlayerObject)
            return;

        var mySeatState = NetworkManager.LocalClient.PlayerObject.GetComponent<PlayerHelicopterSeatStateLan>();
        if (mySeatState == null)
            return;

        if (helicopterRef.TryGet(out NetworkObject no))
        {
            var helicopter = no.GetComponent<LanHelicopterSeatManager>();
            if (helicopter != null)
                mySeatState.SetCurrentHelicopterReference(helicopter);
        }
    }

    [ClientRpc]
    private void TargetClearCurrentHelicopterClientRpc(NetworkObjectReference helicopterRef, ClientRpcParams clientRpcParams = default)
    {
        if (!NetworkManager || !NetworkManager.LocalClient?.PlayerObject)
            return;

        var mySeatState = NetworkManager.LocalClient.PlayerObject.GetComponent<PlayerHelicopterSeatStateLan>();
        if (mySeatState == null)
            return;

        if (helicopterRef.TryGet(out NetworkObject no))
        {
            var helicopter = no.GetComponent<LanHelicopterSeatManager>();
            if (helicopter != null)
                mySeatState.ClearCurrentHelicopterReference(helicopter);
        }
    }

    [ClientRpc]
    private void SnapPlayerIntoSeatClientRpc(NetworkObjectReference playerRef, int seatIndex)
    {
        StartCoroutine(SnapPlayerIntoSeatRoutine(playerRef, seatIndex));
    }

    private IEnumerator SnapPlayerIntoSeatRoutine(NetworkObjectReference playerRef, int seatIndex)
    {
        if (!playerRef.TryGet(out NetworkObject playerNO))
            yield break;

        if (seatPoints == null || seatIndex < 0 || seatIndex >= seatPoints.Length)
            yield break;

        Transform seatTransform = seatPoints[seatIndex];
        if (seatTransform == null)
            yield break;

        int waitFrames = 0;
        while (playerNO.transform.parent != transform && waitFrames < 15)
        {
            waitFrames++;
            yield return null;
        }

        for (int i = 0; i < 20; i++)
        {
            if (playerNO == null || seatTransform == null)
                yield break;

            if (playerNO.transform.parent == transform)
            {
                playerNO.transform.localPosition = seatTransform.localPosition;
                playerNO.transform.localRotation = seatTransform.localRotation;
            }
            else
            {
                playerNO.transform.position = seatTransform.position;
                playerNO.transform.rotation = seatTransform.rotation;
            }

            var rb = playerNO.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            yield return null;
        }
    }

    [ClientRpc]
    private void ForcePlayerHelicopterExitStateClientRpc(NetworkObjectReference playerRef, Vector3 worldExitPos, Quaternion worldExitRot)
    {
        if (IsServer)
            return;

        if (!playerRef.TryGet(out NetworkObject playerNO))
            return;

        var seatState = playerNO.GetComponent<PlayerHelicopterSeatStateLan>();
        if (seatState == null)
            return;

        seatState.ForceHelicopterExitStateFromHelicopter(worldExitPos, worldExitRot);
    }
}