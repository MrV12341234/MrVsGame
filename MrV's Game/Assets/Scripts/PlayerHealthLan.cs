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

        // Fall damage
        if (transform.position.y < -70)
        {
            TakeDamageServerRpc(999);
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
    public void TakeDamageServerRpc(int amount)
    {
        if (currentHealth.Value <= 0) return;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);

        if (currentHealth.Value <= 0)
        {
            Debug.Log("[LAN] Player died");
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
}
