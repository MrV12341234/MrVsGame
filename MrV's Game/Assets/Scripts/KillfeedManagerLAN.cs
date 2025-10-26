using System.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

[NetworkMode(NetworkMode.LAN)]
public class KillfeedManagerLAN : NetworkBehaviour
{
    public static KillfeedManagerLAN Instance;

    [Header("UI")]
    [Tooltip("Prefab of a single killfeed line item (disabled in prefab by default).")]
    public GameObject killfeedItemPrefab;

    [Tooltip("Parent transform under your Killfeed Canvas → Killfeed Item Holder.")]
    public Transform killfeedItemParent;

    private void Awake()
    {
        // Standard singleton (scene-placed)
        Instance = this;
    }

    // ========== PUBLIC API ==========
    /// <summary>
    /// Call this from server (or client; client will forward) with killer/victim display names.
    /// </summary>
    public void ReportKill(string killer, string victim)
    {
        if (string.IsNullOrWhiteSpace(killer) || string.IsNullOrWhiteSpace(victim))
            return;

        if (IsServer)
        {
            // Host/server: broadcast to everyone
            ShowKillClientRpc(killer, victim);
        }
        else
        {
            // Client: ask server to broadcast
            SubmitKillServerRpc(killer, victim);
        }
    }

    // ========== NETWORK PATHS ==========

    [ServerRpc(RequireOwnership = false)]
    private void SubmitKillServerRpc(string killer, string victim, ServerRpcParams rpcParams = default)
    {
        // server validates/sanitizes if desired, then forwards to all
        ShowKillClientRpc(killer, victim);
    }

    [ClientRpc]
    private void ShowKillClientRpc(string killer, string victim, ClientRpcParams clientRpcParams = default)
    {
        // Local UI only
        if (!killfeedItemPrefab || !killfeedItemParent) return;

        GameObject item = Instantiate(killfeedItemPrefab, killfeedItemParent);
        // Assumes child[0] is your TMP text, matching your Photon script
        var tmp = item.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        if (tmp) tmp.text = $"{killer}  loves  {victim}";

        // Make sure the text is enabled next frame (so layout/anim refresh is clean)
        StartCoroutine(DelayedEnableKillfeedItem(item.transform.GetChild(0).gameObject));

        // Auto-destroy after 6 seconds like Photon path
        Destroy(item, 6f);
    }

    private IEnumerator DelayedEnableKillfeedItem(GameObject itemText)
    {
        if (!itemText) yield break;
        itemText.SetActive(false);
        yield return null; // wait one frame
        itemText.SetActive(true);
    }
}
