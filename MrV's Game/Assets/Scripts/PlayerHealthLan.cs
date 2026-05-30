using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[NetworkMode(NetworkMode.LAN)]
public class PlayerHealthLan : NetworkBehaviour
{
    [Header("Health Set Up")]
    public int maxHealth = 100;

    [Header("UI Set Up")]
    public TextMeshProUGUI healthText;
    public Image healthFillImage;
    
    [Header("Damage Flash UI (Local Only)")]
    [SerializeField] private Image damageFlashImage;   // assign Red hit Damage Flash Image here
    [SerializeField] private float damageFlashDuration = 0.25f;
    [SerializeField, Range(0f, 1f)] private float damageFlashAlpha = 0.6f;
    
    private Coroutine _damageFlashRoutine;
    
    private ulong _lastAttackerClientId = ulong.MaxValue;

    private NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        value: 100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    
    [Header("Spawn Protection")]
    [SerializeField] private float spawnProtectionSeconds = 3f;

    private float _spawnProtectedUntil = -1f;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            _spawnProtectedUntil = Time.time + spawnProtectionSeconds;
        }
    }

    private void Start()
    {
        if (IsOwner)
        {
            SetInitialHealthServerRpc(maxHealth);
        }
        
        // IMPORTANT: hook once
        currentHealth.OnValueChanged += OnHealthChanged;

        // Ensure flash starts hidden
        if (damageFlashImage != null)
            SetDamageFlashVisible(false);
    }
    
    private void OnDestroy()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
    }
    
    private void OnHealthChanged(int oldVal, int newVal)
    {
        UpdateUI(newVal);

        // Only the owning client should see the red flash when they're hit with a bullet
        if (!IsOwner) return;

        // Only flash on damage (health decreased)
        if (newVal < oldVal) // remove newVal > 0 if you also want flash on death shot
        {
            TriggerDamageFlash();
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Fall off map damage. (treat as environment/self; no points)
        if (transform.position.y < -70 || transform.position.y > 500)
        {
            // kill player for falling and pass our own id to killfeed as attacker for attribution
            TakeDamageServerRpc(999, NetworkManager.Singleton.LocalClientId);
        }
    }

    private void UpdateUI(int newHealth)
    {
        if (healthText != null)
            healthText.text = $"<b>{newHealth}/</b>{maxHealth}";

        if (healthFillImage != null)
            healthFillImage.fillAmount = (float)newHealth / maxHealth;
    }
    
    private void TriggerDamageFlash()
    {
        if (damageFlashImage == null) return;

        if (_damageFlashRoutine != null)
            StopCoroutine(_damageFlashRoutine);

        _damageFlashRoutine = StartCoroutine(DamageFlashRoutine());
    }
    
    private IEnumerator DamageFlashRoutine()
    {
        SetDamageFlashVisible(true);
        yield return new WaitForSecondsRealtime(damageFlashDuration);
        SetDamageFlashVisible(false);
        _damageFlashRoutine = null;
    }

    private void SetDamageFlashVisible(bool visible)
    {
        if (damageFlashImage == null) return;

        var c = damageFlashImage.color;
        c.a = visible ? damageFlashAlpha : 0f;
        damageFlashImage.color = c;
    }

    [ServerRpc]
    private void SetInitialHealthServerRpc(int _max)
    {
        maxHealth = _max;
        currentHealth.Value = maxHealth;
    }

    private bool HasSpawnProtectionFrom(ulong attackerClientId)
    {
        if (!IsServer) return false;
        if (spawnProtectionSeconds <= 0f) return false;
        if (Time.time >= _spawnProtectedUntil) return false;

        // Allow self-damage and environment damage to still work
        if (attackerClientId == OwnerClientId) return false;
        if (attackerClientId == ulong.MaxValue) return false;

        return true;
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int amount, ulong attackerClientId)
    {
        if (currentHealth.Value <= 0) return;

        if (HasSpawnProtectionFrom(attackerClientId))
            return;
        
        // block friendly fire in Teams mode (server authoritative)
        if (IsFriendlyFire(attackerClientId))
            return;
        
        // track latest attacker for killfeed attribution
        _lastAttackerClientId = attackerClientId;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);

        // ADD: award +2 (pointsPerHit) to valid attacker when victim is still alive
        bool validAttacker =
            attackerClientId != ulong.MaxValue &&                  // not environment
            attackerClientId != OwnerClientId &&                   // not self-hit
            NetworkManager.Singleton.ConnectedClients.ContainsKey(attackerClientId);

        if (validAttacker && currentHealth.Value > 0)
        {
            // Server-side score for a hit (+2 by default – set in LeaderboardManagerLAN)
            LeaderboardManagerLAN.Instance?.Server_AwardHit(attackerClientId);

            // ping attacker with hitmarker locally
            NotifyHitClientRpc(attackerClientId, amount);  // NEW call
        }

        if (currentHealth.Value <= 0)
        {
            // ---- KILLFEED (server) ----
            string victimName = ResolveNameForClient(OwnerClientId);
            string killerName = ResolveNameForClient(_lastAttackerClientId);

            if (LeaderboardManagerLAN.Instance != null)
            {
                // increment victim death
                LeaderboardManagerLAN.Instance.Server_RegisterDeath(OwnerClientId);

                // only award kill if attacker is valid and not environment/self (tweak to your liking)
                if (_lastAttackerClientId != ulong.MaxValue && _lastAttackerClientId != OwnerClientId)
                {
                    LeaderboardManagerLAN.Instance.Server_AwardKill(_lastAttackerClientId);
                }
            }

            if (KillfeedManagerLAN.Instance != null)
            {
                KillfeedManagerLAN.Instance.ReportKill(killerName, victimName);
            }

            // Tell the killer client to play kill FX locally
            NotifyKillerClientRpc(_lastAttackerClientId, victimName);

            SubmitDeathClientRpc();
        }
    }
    
    // attacker-only hitmarker ping
    [ClientRpc]
    private void NotifyHitClientRpc(ulong attackerClientId, int amount)
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.LocalClientId != attackerClientId) return;

        var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (localPlayer == null) return;

        var hk = localPlayer.GetComponent<PlayerHitAndKillsManagerLAN>();
        if (hk != null)
        {
            hk.GetHit(amount); // plays the hit UI/SFX locally for the shooter
        }
    }

    // death hitmaker ping
    [ClientRpc]
    private void SubmitDeathClientRpc()
    {
        if (IsOwner)
        {
            //LAN version (not Photon)
            LocalPlayerKDManagerLAN.Instance?.OnDied();

            if (RoomManagerLan.Instance != null)
            {
                RoomManagerLan.Instance.ShowQuiz();
            }
        }

        if (IsServer)
        {
            NetworkObject netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true); // Destroy across network
            }
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    
    private string ResolveNameForClient(ulong clientId)
    {
        // Environment / map / unknown damage source
        if (clientId == ulong.MaxValue)
            return "Environment";

        // Use RoomManager's dictionary so we don't depend on PlayerObject being alive
        return RoomManagerLan.ResolvePlayerName(clientId);
    }
    
    [ClientRpc]
    private void NotifyKillerClientRpc(ulong killerClientId, string victimName)
    {
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.LocalClientId == killerClientId)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
            var hk = localPlayer ? localPlayer.GetComponent<PlayerHitAndKillsManagerLAN>() : null;
            if (hk != null)
                hk.GetKill(victimName);
        }
    }
    private bool IsFriendlyFire(ulong attackerClientId)
    {
        // Only block friendly fire in Teams mode
        if (RoomManagerLan.Instance == null || !RoomManagerLan.Instance.IsTeamsMode)
            return false;

        // Environment damage can still hurt
        if (attackerClientId == ulong.MaxValue)
            return false;

        // Self-damage rules stay as you already handle elsewhere
        if (attackerClientId == OwnerClientId)
            return false;

        // Victim team (this object)
        var victimSetup = GetComponent<PlayerSetupLan>();
        if (victimSetup == null)
            return false;

        var victimTeam = victimSetup.GetTeam();

        // Attacker team (attacker's PlayerObject)
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(attackerClientId, out var attackerClient) ||
            attackerClient.PlayerObject == null)
            return false;

        var attackerSetup = attackerClient.PlayerObject.GetComponent<PlayerSetupLan>();
        if (attackerSetup == null)
            return false;

        var attackerTeam = attackerSetup.GetTeam();

        return attackerTeam == victimTeam;
    }
}