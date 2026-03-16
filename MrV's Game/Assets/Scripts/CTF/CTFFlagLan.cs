using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Linq;

[NetworkMode(NetworkMode.LAN)]
public class CTFFlagLan : NetworkBehaviour
{
    public enum FlagState : byte { AtBase, Carried, Dropped }

    [Header("Settings")]
    public RoomManagerLan.TeamId flagTeam; // which base this belongs to
    public float autoReturnSeconds = 30f;
    public float dropIfAboveY = 100f;
    public float returnIfBelowY = -70f;
    
    [Header("Manual drop settings")]
    public float manualDropPickupDelay = 1f; // 1 second pickup lock after manual drop (Fire1 is pressed)

    // Server-only: block pickup for a short time after a manual drop
    private float _pickupLockedUntilServerTime = 0f;

    private NetworkVariable<FlagState> state =
        new NetworkVariable<FlagState>(FlagState.AtBase);

    private NetworkVariable<ulong> holderClientId =
        new NetworkVariable<ulong>(ulong.MaxValue);

    private Vector3 _basePos;
    private Quaternion _baseRot;

    private Rigidbody _rb;
    private Collider _col;
    private Collider[] _triggerColliders;

    private Coroutine _autoReturn;

    public override void OnNetworkSpawn()
    {
        _rb = GetComponent<Rigidbody>();
        
        // Grab ALL colliders on the flag prefab (solid + triggers)
        var cols = GetComponentsInChildren<Collider>(true);
        
        // Solid collider (non-trigger) for ground collisions
        _col = cols.FirstOrDefault(c => !c.isTrigger);   // <-- solid collider
        if (_col == null) _col = cols.FirstOrDefault();  // fallback
        
        // Trigger colliders (pickup zones etc)
        _triggerColliders = cols.Where(c => c.isTrigger).ToArray();
        
        // Apply correct physics on EVERY peer (host + clients) based on current state
        ApplyLocalPhysicsForState();

        // When the state changes on server, all clients will receive it and update locally too
        state.OnValueChanged += (_, __) => ApplyLocalPhysicsForState();


        if (IsServer)
        {
            _basePos = transform.position;
            _baseRot = transform.rotation;
            Server_ReturnToBase();
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        if (RoomManagerLan.Instance == null || !RoomManagerLan.Instance.IsCTFMode) return;

        // off-map safety
        if (transform.position.y < returnIfBelowY)
        {
            Server_ReturnToBase();
            CTFGameManagerLan.Instance?.AnnounceClientRpc($"{flagTeam} flag returned (fell off map).");
            return;
        }

        if (state.Value == FlagState.Carried)
        {
            // if carrier gone (death/despawn), drop at current position
            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(holderClientId.Value) ||
                NetworkManager.Singleton.ConnectedClients[holderClientId.Value].PlayerObject == null)
            {
                Server_Drop(transform.position);
                return;
            }

            var playerObj = NetworkManager.Singleton.ConnectedClients[holderClientId.Value].PlayerObject;
            var carryPoint = FindCarryPoint(playerObj.transform);

            // follow carrier
            if (carryPoint != null)
            {
                transform.position = carryPoint.position;
                transform.rotation = carryPoint.rotation;
            }
            else
            {
                transform.position = playerObj.transform.position + Vector3.up * 1.5f; // move flag, not player
                transform.rotation = playerObj.transform.rotation;
            }

            // anti-skyrocket rule
            if (carryPoint.position.y > dropIfAboveY)
            {
                Server_Drop(carryPoint.position);
                CTFGameManagerLan.Instance?.AnnounceClientRpc($"{flagTeam} flag dropped (too high).");
            }
        }
    }

    private Transform FindCarryPoint(Transform playerRoot)
    {
        // empty child called "FlagCarryPoint" on player prefab
        var t = playerRoot.Find("FlagCarryPoint"); // direct child search on the player prefab
        if (t != null) return t;

        // fallback: just use playerRoot (but DO NOT move it!)
        return playerRoot;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (RoomManagerLan.Instance == null || !RoomManagerLan.Instance.IsCTFMode) return;
        
        // Prevent immediate re-pickup right after a manual drop
        if (state.Value == FlagState.Dropped && Time.time < _pickupLockedUntilServerTime)
            return;
        
        if (state.Value == FlagState.Carried) return;

        if (!other.CompareTag("Player")) return;

        var setup = other.GetComponentInParent<PlayerSetupLan>();
        if (setup == null) return;

        var toucherTeam = setup.GetTeam();
        ulong toucherId = setup.OwnerClientId;

        // Defender touching their own dropped flag -> return instantly
        if (toucherTeam == flagTeam && state.Value == FlagState.Dropped)
        {
            Server_ReturnToBase();
            CTFGameManagerLan.Instance?.AnnounceClientRpc($"{flagTeam} flag returned!");
            return;
        }

        // Attacker touching enemy flag -> pick up
        if (toucherTeam != flagTeam)
        {
            Server_Pickup(toucherId);
            CTFGameManagerLan.Instance?.AnnounceClientRpc($"{toucherTeam} team has the {flagTeam} flag!");
        }
    }

    public bool IsHeldBy(ulong clientId) =>
        state.Value == FlagState.Carried && holderClientId.Value == clientId;

    public RoomManagerLan.TeamId GetFlagTeam() => flagTeam;

    public void Server_Pickup(ulong carrierId)
    {
        if (!IsServer) return;

        state.Value = FlagState.Carried;
        holderClientId.Value = carrierId;

        if (_autoReturn != null) StopCoroutine(_autoReturn);
        _autoReturn = null;

        SetPhysicsCarried();
    }

    public void Server_Drop(Vector3 pos)
    {
        if (!IsServer) return;

        state.Value = FlagState.Dropped;
        holderClientId.Value = ulong.MaxValue;

        transform.position = pos + Vector3.up * 0.25f;

        SetPhysicsDropped();

        if (_autoReturn != null) StopCoroutine(_autoReturn);
        _autoReturn = StartCoroutine(AutoReturnRoutine());
        CTFGameManagerLan.Instance?.AnnounceClientRpc($"{flagTeam} flag dropped!");
    }

    public void Server_ReturnToBase()
    {
        if (!IsServer) return;

        state.Value = FlagState.AtBase;
        holderClientId.Value = ulong.MaxValue;

        if (_autoReturn != null) StopCoroutine(_autoReturn);
        _autoReturn = null;

        transform.position = _basePos;
        transform.rotation = _baseRot;

        SetPhysicsCarried(); // keep stable at base (kinematic)
    }

    private IEnumerator AutoReturnRoutine()
    {
        yield return new WaitForSeconds(autoReturnSeconds);

        if (state.Value == FlagState.Dropped)
        {
            Server_ReturnToBase();
            CTFGameManagerLan.Instance?.AnnounceClientRpc($"{flagTeam} flag returned (timer).");
        }
        _autoReturn = null;
    }

    private void SetPhysicsCarried()
    {
        ApplyLocalPhysicsForState();
    }

    private void SetPhysicsDropped()
    {
        ApplyLocalPhysicsForState();
        
        // Also force an upright rotation on drop
        transform.rotation = _baseRot;
    }
    
    // RPC that allows the flag carrier to drop the flag if they press Fire1 while holding the flag
    [ServerRpc(RequireOwnership = false)]
    public void RequestDropByCarrierServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (RoomManagerLan.Instance == null || !RoomManagerLan.Instance.IsCTFMode) return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        // Only the actual carrier can request a drop
        if (state.Value != FlagState.Carried) return;
        if (holderClientId.Value != senderId) return;

        // Use the carrier's current position (server authoritative)
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var nc) || nc.PlayerObject == null)
        {
            // Carrier object missing -> just drop where flag currently is
            Server_Drop(transform.position);
            _pickupLockedUntilServerTime = Time.time + manualDropPickupDelay;
            return;
        }

        Vector3 dropPos = nc.PlayerObject.transform.position;

        // Lock pickup for a second so carrier can move away
                _pickupLockedUntilServerTime = Time.time + manualDropPickupDelay;
        
        // Drop slightly above ground so it doesn't clip
        Server_Drop(dropPos);
    }
    
    private void ApplyLocalPhysicsForState()
    {
        bool isDropped = (state.Value == FlagState.Dropped);
        bool isCarried = (state.Value == FlagState.Carried);

        // SOLID collider:
        // only ON when dropped (so it rests on ground)
        if (_col != null)
            _col.enabled = isDropped;

        // TRIGGER colliders:
        // OFF while carried (so they never block bullets / cause weird interactions)
        // ON when AtBase or Dropped (so pickup/return works)
        if (_triggerColliders != null)
        {
            for (int i = 0; i < _triggerColliders.Length; i++)
            {
                if (_triggerColliders[i] != null)
                    _triggerColliders[i].enabled = !isCarried;
            }
        }

        // Rigidbody state
        if (_rb != null)
        {
            if (isDropped)
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
                _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            else
            {
                // AtBase OR Carried: stable, no physics pushing
                _rb.isKinematic = true;
                _rb.useGravity = false;
                _rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }


}