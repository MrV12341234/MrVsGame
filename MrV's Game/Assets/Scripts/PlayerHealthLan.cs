using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[NetworkMode(NetworkMode.LAN)]
public class PlayerHealthLan : NetworkBehaviour
{
    [Header("Health Set Up")]
    public int maxHealth = 100;

    [Header("UI Set Up")]
    public TextMeshProUGUI healthText;
    public Image healthFillImage;
    
    private ulong _lastAttackerClientId = ulong.MaxValue;

    private NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        value: 100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Start()
    {
        if (IsOwner)
        {
            SetInitialHealthServerRpc(maxHealth);
        }
        currentHealth.OnValueChanged += (oldVal, newVal) => UpdateUI(newVal);
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Fall off map damage
        if (transform.position.y < -70)
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

    [ServerRpc]
    private void SetInitialHealthServerRpc(int _max)
    {
        maxHealth = _max;
        currentHealth.Value = maxHealth;
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int amount, ulong attackerClientId)
    {
        if (currentHealth.Value <= 0) return;
        
        // track latest attacker for killfeed attribution
        _lastAttackerClientId = attackerClientId;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);

        if (currentHealth.Value <= 0)
        {
            // ---- KILLFEED (server) ----
            string victimName = ResolveNameForClient(OwnerClientId);
            string killerName = ResolveNameForClient(_lastAttackerClientId);

            // If you prefer to skip environmental/self kills:
            // if (_lastAttackerClientId == ulong.MaxValue || _lastAttackerClientId == OwnerClientId) { /* skip or set "Environment" */ }

            if (KillfeedManagerLAN.Instance != null)
            {
                KillfeedManagerLAN.Instance.ReportKill(killerName, victimName);
            }
            SubmitDeathClientRpc();
        }
    }

    [ClientRpc]
    private void SubmitDeathClientRpc()
    {
        if (IsOwner)
        {
            LocalPlayerKDManager.Instance?.OnDied();

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
        // Environment / unknown
        if (clientId == ulong.MaxValue) return "Environment";

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var nc) &&
            nc.PlayerObject != null)
        {
            var ps = nc.PlayerObject.GetComponent<PlayerSetupLan>();
            if (ps != null) return ps.GetPlayerNameString();
        }
        return $"Player_{clientId}";
    }
    
}
