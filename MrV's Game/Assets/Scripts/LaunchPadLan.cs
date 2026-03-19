using System.Collections.Generic;
using UnityEngine;

public class LaunchPadLan : MonoBehaviour
{
    [Header("Launch Settings")]
    [SerializeField] private float launchForce = 12f;

    [Tooltip("If true, removes current vertical speed first so every launch feels more consistent.")]
    [SerializeField] private bool clearVerticalVelocity = true;

    [Tooltip("Small delay so the same player does not trigger the pad multiple times instantly.")]
    [SerializeField] private float launchCooldown = 0.25f;

    private readonly Dictionary<ulong, float> _lastLaunchTimeByClient = new Dictionary<ulong, float>();

    private void OnTriggerEnter(Collider other)
    {
        var setup = other.GetComponentInParent<PlayerSetupLan>();
        if (setup == null) return;

        // Only launch the local owner's player on this machine.
        // This avoids host/client physics fighting each other.
        if (!setup.IsOwner) return;

        ulong clientId = setup.OwnerClientId;

        if (_lastLaunchTimeByClient.TryGetValue(clientId, out float lastTime))
        {
            if (Time.time - lastTime < launchCooldown)
                return;
        }

        _lastLaunchTimeByClient[clientId] = Time.time;

        if (setup.movement != null)
        {
            setup.movement.LaunchUpward(launchForce, clearVerticalVelocity);
        }
    }
}