using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// attach to each vehicle root object

[NetworkMode(NetworkMode.LAN)]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class LanVehicleSeatManager : NetworkBehaviour
{
    public enum Axis
    {
        X,
        Y,
        Z
    }

    [Header("Seats")]
    [Tooltip("Size of this array = number of seats in this vehicle.")]
    public Transform[] seatPoints;

    [Tooltip("Assign the matching trigger objects in the SAME ORDER as seatPoints.")]
    public LanVehicleSeatTrigger[] seatTriggers;

    [Tooltip("Which seat index is the driver seat. Usually 0.")]
    public int driverSeatIndex = 0;

    [Header("Exit")]
    [Tooltip("How high above the seat point the player appears when exiting.")]
    public float exitUpOffset = 2f;

    [Header("Driving")]
    public Rigidbody vehicleRigidbody;
    public float maxSpeedNormal = 18f;
    public float reverseSpeed = 8f;
    public float acceleration = 20f;
    public float deceleration = 14f;
    
    [Header("Vehicle Impact Damage")]
    public bool canRunOverPlayers = true;
    public int runOverDamage = 100;
    public float runOverMinSpeed = 8f;
    public float runOverHitCooldown = 0.5f;
    
    [Header("Extra Gravity")]
    public float extraGravity = 25f;
    public bool applyExtraGravityOnlyWhenDriven = true;
    
    [Header("Idle Lock")]
    [Tooltip("When true, the vehicle cannot be pushed around unless a driver is seated.")]
    public bool lockVehicleWhenNoDriver = true;

    [Tooltip("Extra damping while parked so small bumps die instantly.")]
    public float parkedDrag = 8f;

    [Tooltip("Extra angular damping while parked.")]
    public float parkedAngularDrag = 10f;

    private float _defaultDrag;
    private float _defaultAngularDrag;

    [Tooltip("X = speed normalized, Y = steering multiplier")]
    public AnimationCurve steeringCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.7f);

    public float maxSteerStrength = 35f;
    public float steerAcceleration = 8f;
    public float steerDeceleration = 12f;
    
    [Header("Wall Handling")]
    public float wallContactMemory = 0.08f;
    [Range(0f, 1f)] public float wallMaxUpDot = 0.55f;
    public float wallSlowdownPerSecond = 6f;
    [Range(0f, 20f)] public float wallSlideProjectionStrength = 8f;
    public float wallAssistMinSpeed = 4f;
    public float wallYawAssist = 8f;

    [Header("Wheels")]
    public Transform tireFL;
    public Transform tireFR;
    public Transform tireBL;
    public Transform tireBR;
    public Transform tireYawFL;
    public Transform tireYawFR;
    public Axis tireYawAxis = Axis.Y;
    public Axis wheelSpinAxis = Axis.X;
    public float wheelSpinDegreesPerSecondAtMaxSpeed = 900f;

    private NetworkList<ulong> seatOccupants;

    private readonly NetworkVariable<float> syncedSteer =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> syncedSpeed =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float _serverThrottle;
    private float _serverSteer;
    private float _appliedSpeed;
    private float _appliedSteer;
    private float _wheelSpinAngle;
    private Vector3 _lastWallNormal = Vector3.zero;
    private float _lastWallContactTime = -999f;
    
    private readonly Dictionary<ulong, float> _runOverCooldowns = new Dictionary<ulong, float>();

    private void Awake()
    {
        seatOccupants = new NetworkList<ulong>();

        if (vehicleRigidbody == null)
            vehicleRigidbody = GetComponent<Rigidbody>();
        
        if (vehicleRigidbody != null)
        {
            _defaultDrag = vehicleRigidbody.linearDamping;
            _defaultAngularDrag = vehicleRigidbody.angularDamping;
        }
    }

    private void OnValidate()
    {
        if (vehicleRigidbody == null)
            vehicleRigidbody = GetComponent<Rigidbody>();

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
        }
        
        RefreshParkLockState();
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
        UpdateWheelVisuals();
    }

    private void FixedUpdate()
    {
        if (!IsServer)
            return;

        CleanupInvalidOccupants();

        if ((Time.fixedTime - _lastWallContactTime) > wallContactMemory)
            _lastWallNormal = Vector3.zero;

        // IMPORTANT:
        // We steer manually with MoveRotation, so do not let physics keep any spin.
        if (vehicleRigidbody != null)
        {
            Vector3 av = vehicleRigidbody.angularVelocity;
            vehicleRigidbody.angularVelocity = new Vector3(av.x, 0f, av.z);
        }

        RunVehicleMovement(Time.fixedDeltaTime);

        if (vehicleRigidbody != null)
        {
            if (!applyExtraGravityOnlyWhenDriven || IsDriverSeatOccupied())
            {
                vehicleRigidbody.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
            }
        }

        syncedSteer.Value = _appliedSteer;
        syncedSpeed.Value = _appliedSpeed;

        // Kill any collision-added yaw spin again after movement.
        if (vehicleRigidbody != null)
        {
            Vector3 av = vehicleRigidbody.angularVelocity;
            vehicleRigidbody.angularVelocity = new Vector3(av.x, 0f, av.z);
        }

        syncedSteer.Value = _appliedSteer;
        syncedSpeed.Value = _appliedSpeed;
    }

    private void AutoWireTriggers()
    {
        if (seatTriggers == null)
            return;

        for (int i = 0; i < seatTriggers.Length; i++)
        {
            if (seatTriggers[i] != null)
                seatTriggers[i].Configure(this, i, i == driverSeatIndex);
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

            bool occupied = IsSeatOccupied(i);

            // Available seats have active trigger colliders.
            // Occupied seats have disabled trigger colliders.
            seatTriggers[i].SetSeatInteractable(!occupied);
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

    private void RunVehicleMovement(float deltaTime)
    {
        if (vehicleRigidbody == null)
            return;

        bool hasDriver = IsDriverSeatOccupied();

        float throttle = hasDriver ? _serverThrottle : 0f;
        float steerInput = hasDriver ? _serverSteer : 0f;

        float targetSpeed;
        if (throttle > 0f)
            targetSpeed = maxSpeedNormal * throttle;
        else if (throttle < 0f)
            targetSpeed = reverseSpeed * throttle;
        else
            targetSpeed = 0f;

        float speedRate = Mathf.Abs(targetSpeed) > Mathf.Abs(_appliedSpeed) ? acceleration : deceleration;
        _appliedSpeed = Mathf.MoveTowards(_appliedSpeed, targetSpeed, speedRate * deltaTime);

        float speed01 = Mathf.InverseLerp(0f, maxSpeedNormal, Mathf.Abs(_appliedSpeed));
        float steerMultiplier = steeringCurve != null ? steeringCurve.Evaluate(speed01) : 1f;

        bool hasRecentWallContact =
    _lastWallNormal != Vector3.zero &&
    (Time.fixedTime - _lastWallContactTime) <= wallContactMemory;

        float targetSteer = steerInput * maxSteerStrength * steerMultiplier;
        float steerRate = Mathf.Abs(steerInput) > 0.01f ? steerAcceleration : steerDeceleration;
        _appliedSteer = Mathf.MoveTowards(_appliedSteer, targetSteer, steerRate * maxSteerStrength * deltaTime);

        Quaternion nextRotation = vehicleRigidbody.rotation;

        if (Mathf.Abs(_appliedSpeed) > 0.05f)
        {
            float steerDirection = (_appliedSpeed >= 0f) ? 1f : -1f;
            float yawThisFrame = _appliedSteer * steerDirection * deltaTime;

    // While touching a wall, do NOT allow steering that turns the nose more into it
    if (hasRecentWallContact && Mathf.Abs(_appliedSpeed) >= wallAssistMinSpeed)
    {
        float currentIntoWall = Vector3.Dot(transform.forward, -_lastWallNormal);

        Vector3 predictedForward =
            Quaternion.Euler(0f, yawThisFrame, 0f) * transform.forward;

        float predictedIntoWall = Vector3.Dot(predictedForward, -_lastWallNormal);

        if (predictedIntoWall > currentIntoWall && predictedIntoWall > 0.05f)
        {
            yawThisFrame = 0f;
        }
    }

    nextRotation = vehicleRigidbody.rotation * Quaternion.Euler(0f, yawThisFrame, 0f);

    // Gently align the car parallel to the wall so it slides instead of nose-jamming
    if (hasRecentWallContact && Mathf.Abs(_appliedSpeed) >= wallAssistMinSpeed)
    {
        Vector3 slideForward = Vector3.ProjectOnPlane(nextRotation * Vector3.forward, _lastWallNormal);
        slideForward.y = 0f;

        if (slideForward.sqrMagnitude > 0.001f)
        {
            slideForward.Normalize();

            // Keep the forward direction sensible
            if (Vector3.Dot(slideForward, transform.forward) < 0f)
                slideForward = -slideForward;

            Quaternion wallAlignedRotation = Quaternion.LookRotation(slideForward, Vector3.up);
            nextRotation = Quaternion.Slerp(nextRotation, wallAlignedRotation, wallYawAssist * deltaTime);
        }
    }

    vehicleRigidbody.MoveRotation(nextRotation);
}

Vector3 currentVel = vehicleRigidbody.linearVelocity;
Vector3 desiredHorizontalVelocity = (nextRotation * Vector3.forward) * _appliedSpeed;

        if (hasRecentWallContact && desiredHorizontalVelocity.magnitude >= wallAssistMinSpeed)
        {
            float intoWall = Vector3.Dot(desiredHorizontalVelocity, _lastWallNormal);

            // If the vehicle is trying to drive into the wall,
            // remove that into-wall component so it slides along the wall instead.
            if (intoWall < 0f)
            {
                desiredHorizontalVelocity = Vector3.ProjectOnPlane(desiredHorizontalVelocity, _lastWallNormal);
            }
        }

        vehicleRigidbody.linearVelocity = new Vector3(
            desiredHorizontalVelocity.x,
            currentVel.y,
            desiredHorizontalVelocity.z
        );
    }

    private void UpdateWheelVisuals()
    {
        float speedAbs = Mathf.Abs(syncedSpeed.Value);
        float speedRatio = maxSpeedNormal > 0.01f ? speedAbs / maxSpeedNormal : 0f;
        float spinDelta = wheelSpinDegreesPerSecondAtMaxSpeed * speedRatio * Time.deltaTime;

        if (syncedSpeed.Value < 0f)
            spinDelta *= -1f;

        _wheelSpinAngle += spinDelta;

        ApplyWheelSpin(tireFL, _wheelSpinAngle);
        ApplyWheelSpin(tireFR, _wheelSpinAngle);
        ApplyWheelSpin(tireBL, _wheelSpinAngle);
        ApplyWheelSpin(tireBR, _wheelSpinAngle);

        ApplyAxisRotation(tireYawFL, tireYawAxis, syncedSteer.Value);
        ApplyAxisRotation(tireYawFR, tireYawAxis, syncedSteer.Value);
    }

    private void ApplyWheelSpin(Transform t, float angle)
    {
        if (t == null)
            return;

        ApplyAxisRotation(t, wheelSpinAxis, angle);
    }

    private void ApplyAxisRotation(Transform t, Axis axis, float angle)
    {
        if (t == null)
            return;

        Vector3 euler = Vector3.zero;

        switch (axis)
        {
            case Axis.X: euler.x = angle; break;
            case Axis.Y: euler.y = angle; break;
            case Axis.Z: euler.z = angle; break;
        }

        t.localRotation = Quaternion.Euler(euler);
    }

    private bool IsDriverSeatOccupied()
    {
        if (driverSeatIndex < 0 || driverSeatIndex >= seatOccupants.Count)
            return false;

        return seatOccupants[driverSeatIndex] != ulong.MaxValue;
    }
    
    public bool IsSeatOccupied(int seatIndex)
    {
        if (seatOccupants == null)
            return false;

        if (seatIndex < 0 || seatIndex >= seatOccupants.Count)
            return false;

        return seatOccupants[seatIndex] != ulong.MaxValue;
    }
    
    private ulong GetDriverClientId()
    {
        if (!IsDriverSeatOccupied())
            return ulong.MaxValue;

        return seatOccupants[driverSeatIndex];
    }

    private float GetHorizontalVehicleSpeed()
    {
        if (vehicleRigidbody == null)
            return 0f;

        Vector3 v = vehicleRigidbody.linearVelocity;
        v.y = 0f;
        return v.magnitude;
    }
    
    private void RefreshParkLockState()
    {
        if (vehicleRigidbody == null || !lockVehicleWhenNoDriver)
            return;

        bool hasDriver = IsDriverSeatOccupied();

        if (hasDriver)
        {
            // Fully unlock while driving so the car can tilt on ramps/slopes
            vehicleRigidbody.constraints = RigidbodyConstraints.None;

            vehicleRigidbody.linearDamping = _defaultDrag;
            vehicleRigidbody.angularDamping = _defaultAngularDrag;
        }
        else
        {
            // Parked: cannot be pushed around by players
            vehicleRigidbody.linearVelocity = Vector3.zero;
            vehicleRigidbody.angularVelocity = Vector3.zero;

            vehicleRigidbody.constraints =
                RigidbodyConstraints.FreezePositionX |
                RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;

            vehicleRigidbody.linearDamping = parkedDrag;
            vehicleRigidbody.angularDamping = parkedAngularDrag;
        }
    }

    private void OnSeatOccupantsChanged(NetworkListEvent<ulong> changeEvent)
    {
        RefreshParkLockState();
        RefreshSeatTriggerStates();
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
    
    private bool TryGetWallCollisionData(Collision collision, out Vector3 averagedWallNormal)
{
    averagedWallNormal = Vector3.zero;

    if (collision == null || collision.contactCount == 0)
        return false;

    Vector3 summedWallNormal = Vector3.zero;
    int wallContacts = 0;

    for (int i = 0; i < collision.contactCount; i++)
    {
        ContactPoint contact = collision.GetContact(i);
        float upDot = Vector3.Dot(contact.normal, Vector3.up);

        // Ignore floor / mostly-upward surfaces. Only treat side surfaces as walls.
        if (upDot < wallMaxUpDot)
        {
            summedWallNormal += contact.normal;
            wallContacts++;
        }
    }

    if (wallContacts <= 0)
        return false;

    averagedWallNormal = (summedWallNormal / wallContacts).normalized;
    return true;
}

private void OnCollisionStay(Collision collision)
{
    if (!IsServer)
        return;

    if (!IsDriverSeatOccupied())
        return;

    if (!TryGetWallCollisionData(collision, out Vector3 averagedWallNormal))
        return;

    Vector3 horizontalVelocity = vehicleRigidbody.linearVelocity;
    horizontalVelocity.y = 0f;

    if (horizontalVelocity.magnitude < wallAssistMinSpeed)
    {
        _lastWallNormal = Vector3.zero;
        _lastWallContactTime = -999f;
        return;
    }

    _lastWallNormal = averagedWallNormal;
    _lastWallContactTime = Time.fixedTime;

    ApplyWallContactSlowdown(averagedWallNormal);
}

private void OnCollisionEnter(Collision collision)
{
    if (!IsServer)
        return;

    if (!canRunOverPlayers)
        return;

    if (!IsDriverSeatOccupied())
        return;

    if (GetHorizontalVehicleSpeed() < runOverMinSpeed)
        return;

    PlayerHealthLan targetHealth = collision.collider.GetComponentInParent<PlayerHealthLan>();
    if (targetHealth == null)
        return;

    NetworkObject targetNetObj = targetHealth.GetComponent<NetworkObject>();
    if (targetNetObj == null)
        return;

    ulong victimClientId = targetNetObj.OwnerClientId;
    ulong driverClientId = GetDriverClientId();

    if (driverClientId == ulong.MaxValue)
        return;

    // Don't kill the driver with their own vehicle
    if (victimClientId == driverClientId)
        return;

    // Don't hit passengers inside THIS same vehicle
    if (FindSeatForClient(victimClientId) >= 0)
        return;

    // Prevent repeat hits from one impact/contact burst
    if (_runOverCooldowns.TryGetValue(victimClientId, out float nextAllowedTime))
    {
        if (Time.time < nextAllowedTime)
            return;
    }

    _runOverCooldowns[victimClientId] = Time.time + runOverHitCooldown;

    // Use your existing damage system so killfeed / leaderboard / hitmarker all still work
    targetHealth.TakeDamageServerRpc(runOverDamage, driverClientId);
}

private void ApplyWallContactSlowdown(Vector3 wallNormal)
{
    _appliedSpeed = Mathf.MoveTowards(_appliedSpeed, 0f, wallSlowdownPerSecond * Time.fixedDeltaTime);

    Vector3 velocity = vehicleRigidbody.linearVelocity;
    Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);

    Vector3 projectedAlongWall = Vector3.ProjectOnPlane(horizontalVelocity, wallNormal);

    horizontalVelocity = Vector3.Lerp(
        horizontalVelocity,
        projectedAlongWall,
        wallSlideProjectionStrength * Time.fixedDeltaTime);

    vehicleRigidbody.linearVelocity = new Vector3(horizontalVelocity.x, velocity.y, horizontalVelocity.z);

    // Also kill impact spin while sliding on the wall
    Vector3 av = vehicleRigidbody.angularVelocity;
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

        if (NetworkManager == null || !NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null)
            return;

        Transform seatTransform = seatPoints[seatIndex];
        if (seatTransform == null)
            return;

        // simple distance validation
        if (seatTriggers != null && seatIndex < seatTriggers.Length && seatTriggers[seatIndex] != null)
        {
            float dist = Vector3.Distance(client.PlayerObject.transform.position, seatTriggers[seatIndex].transform.position);
            if (dist > 5f)
                return;
        }

        NetworkObject playerNO = client.PlayerObject;
        var seatState = playerNO.GetComponent<PlayerVehicleSeatStateLan>();
        if (seatState == null)
            return;

        seatOccupants[seatIndex] = clientId;

        // Set current vehicle first so collision-ignore logic has the vehicle reference
        seatState.SetCurrentVehicleReference(this);

        // Zero any existing motion before seating
        var rb = playerNO.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Parent to vehicle root, but KEEP current world position first
        playerNO.TrySetParent(NetworkObject, true);

        // Then snap using WORLD seat position/rotation
        playerNO.transform.position = seatTransform.position;
        playerNO.transform.rotation = seatTransform.rotation;

        bool isDriver = seatIndex == driverSeatIndex;
        seatState.ServerSetVehicleState(true, isDriver);

        RefreshParkLockState();

        TargetSetCurrentVehicleClientRpc(NetworkObject, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        });

// Snap THIS player into THIS seat on every client
        SnapPlayerIntoSeatClientRpc(playerNO, seatIndex);

        if (isDriver)
        {
            _serverThrottle = 0f;
            _serverSteer = 0f;
        }
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

    private void ClearSeatServer(int seatIndex, bool placeOutsideVehicle)
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
            var seatState = playerNO.GetComponent<PlayerVehicleSeatStateLan>();

            // Exit above the seat instead of to the side.
            // This avoids pushing the player through nearby walls/objects.
            Transform seatTransform = null;

            if (seatPoints != null && seatIndex >= 0 && seatIndex < seatPoints.Length)
            {
                seatTransform = seatPoints[seatIndex];
            }

            Vector3 finalExitPos = playerNO.transform.position;
            Quaternion finalExitRot = Quaternion.LookRotation(transform.forward, Vector3.up);

            if (placeOutsideVehicle)
            {
                Vector3 baseExitPos = seatTransform != null
                    ? seatTransform.position
                    : playerNO.transform.position;

                // Use vehicle up so it works better on slopes/ramps.
                finalExitPos = baseExitPos + transform.up * exitUpOffset;
            }

            // Remove player from vehicle parent first, then place them above the car.
            playerNO.TryRemoveParent(true);
            playerNO.transform.SetPositionAndRotation(finalExitPos, finalExitRot);

            var rb = playerNO.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (seatState != null)
            {
                seatState.ServerSetVehicleState(false, false);
                seatState.ClearCurrentVehicleReference(this);

                // Force-reset the server/host copy immediately.
                seatState.ForceVehicleExitStateFromVehicle(finalExitPos, finalExitRot);
                
                // REMOVED the hacky nt.enabled = false/true block here. 
                // It was fighting the Coroutine and breaking the NetworkTransform buffer.
                

                // Force-reset every client copy too.
                ForcePlayerVehicleExitStateClientRpc(playerNO, finalExitPos, finalExitRot);

                TargetClearCurrentVehicleClientRpc(NetworkObject, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { clientId }
                    }
                });
            }
        }

        seatOccupants[seatIndex] = ulong.MaxValue;
        
        RefreshParkLockState();
        RefreshSeatTriggerStates();

        if (seatIndex == driverSeatIndex)
        {
            _serverThrottle = 0f;
            _serverSteer = 0f;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetDriverInputServerRpc(float throttle, float steer, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        if (driverSeatIndex < 0 || driverSeatIndex >= seatOccupants.Count)
            return;

        if (seatOccupants[driverSeatIndex] != senderId)
            return;

        _serverThrottle = Mathf.Clamp(throttle, -1f, 1f);
        _serverSteer = Mathf.Clamp(steer, -1f, 1f);
    }

    [ClientRpc]
    private void TargetSetCurrentVehicleClientRpc(NetworkObjectReference vehicleRef, ClientRpcParams clientRpcParams = default)
    {
        if (!NetworkManager || !NetworkManager.LocalClient?.PlayerObject)
            return;

        var mySeatState = NetworkManager.LocalClient.PlayerObject.GetComponent<PlayerVehicleSeatStateLan>();
        if (mySeatState == null)
            return;

        if (vehicleRef.TryGet(out NetworkObject no))
        {
            var vehicle = no.GetComponent<LanVehicleSeatManager>();
            if (vehicle != null)
                mySeatState.SetCurrentVehicleReference(vehicle);
        }
    }

    [ClientRpc]
    private void TargetClearCurrentVehicleClientRpc(NetworkObjectReference vehicleRef, ClientRpcParams clientRpcParams = default)
    {
        if (!NetworkManager || !NetworkManager.LocalClient?.PlayerObject)
            return;

        var mySeatState = NetworkManager.LocalClient.PlayerObject.GetComponent<PlayerVehicleSeatStateLan>();
        if (mySeatState == null)
            return;

        if (vehicleRef.TryGet(out NetworkObject no))
        {
            var vehicle = no.GetComponent<LanVehicleSeatManager>();
            if (vehicle != null)
                mySeatState.ClearCurrentVehicleReference(vehicle);
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

        // Wait a bit for parent sync to arrive on this client
        int waitFrames = 0;
        while (playerNO.transform.parent != transform && waitFrames < 15)
        {
            waitFrames++;
            yield return null;
        }
        // Force the correct seat placement for multiple frames
        // so late transform updates do not leave the player outside the car
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
                // fallback if parent is still late on this client
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
    private void ForcePlayerVehicleExitStateClientRpc(NetworkObjectReference playerRef, Vector3 worldExitPos, Quaternion worldExitRot)
    {
        // ADD THIS: The Server already executed the exit state locally inside ClearSeatServer. 
        // Running it again via the broadcast RPC corrupts the NetworkTransform state!
        if (IsServer)
            return;
        if (!playerRef.TryGet(out NetworkObject playerNO))
            return;

        var seatState = playerNO.GetComponent<PlayerVehicleSeatStateLan>();
        if (seatState == null)
            return;

        seatState.ForceVehicleExitStateFromVehicle(worldExitPos, worldExitRot);
    }
}