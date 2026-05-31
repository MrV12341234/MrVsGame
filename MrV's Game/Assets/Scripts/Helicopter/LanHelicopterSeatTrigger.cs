using UnityEngine;

// Attach to each helicopter Seat Trigger object, not the seat location transform.

public class LanHelicopterSeatTrigger : MonoBehaviour
{
    [HideInInspector] public LanHelicopterSeatManager helicopter;
    [HideInInspector] public int seatIndex;
    [HideInInspector] public bool isPilotSeat;

    private Collider[] _triggerColliders;
    private PlayerHelicopterSeatStateLan _localSeatStateInside;

    private void Awake()
    {
        _triggerColliders = GetComponents<Collider>();

        for (int i = 0; i < _triggerColliders.Length; i++)
        {
            if (_triggerColliders[i] != null)
                _triggerColliders[i].isTrigger = true;
        }
    }

    public void Configure(LanHelicopterSeatManager owningHelicopter, int index, bool pilot)
    {
        helicopter = owningHelicopter;
        seatIndex = index;
        isPilotSeat = pilot;
    }

    public void SetSeatInteractable(bool canInteract)
    {
        if (_triggerColliders == null || _triggerColliders.Length == 0)
            _triggerColliders = GetComponents<Collider>();

        if (!canInteract && _localSeatStateInside != null)
        {
            _localSeatStateInside.ClearNearbyHelicopterSeat(helicopter, seatIndex);
            _localSeatStateInside = null;
        }

        for (int i = 0; i < _triggerColliders.Length; i++)
        {
            if (_triggerColliders[i] != null)
                _triggerColliders[i].enabled = canInteract;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TrySetNearbySeat(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TrySetNearbySeat(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (helicopter == null)
            return;

        var seatState = other.GetComponentInParent<PlayerHelicopterSeatStateLan>();
        if (seatState == null || !seatState.IsOwner)
            return;

        if (_localSeatStateInside == seatState)
            _localSeatStateInside = null;

        seatState.ClearNearbyHelicopterSeat(helicopter, seatIndex);
    }

    private void OnDisable()
    {
        if (_localSeatStateInside != null)
        {
            _localSeatStateInside.ClearNearbyHelicopterSeat(helicopter, seatIndex);
            _localSeatStateInside = null;
        }
    }

    private void TrySetNearbySeat(Collider other)
    {
        if (helicopter == null)
            return;

        var seatState = other.GetComponentInParent<PlayerHelicopterSeatStateLan>();
        if (seatState == null || !seatState.IsOwner)
            return;
        
        var vehicleSeatState = other.GetComponentInParent<PlayerVehicleSeatStateLan>();
        if (vehicleSeatState != null && vehicleSeatState.IsSeated)
            return;

        if (helicopter.IsSeatOccupied(seatIndex))
        {
            if (_localSeatStateInside == seatState)
            {
                seatState.ClearNearbyHelicopterSeat(helicopter, seatIndex);
                _localSeatStateInside = null;
            }

            return;
        }

        _localSeatStateInside = seatState;
        seatState.SetNearbyHelicopterSeat(helicopter, seatIndex, isPilotSeat);
    }
}