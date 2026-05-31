using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

[NetworkMode(NetworkMode.LAN)]
public class ClaymoreWeaponLAN : MonoBehaviour
{
    [Header("Weapon Stats")]
    [Tooltip("How many claymores the player starts with.")]
    public int startingClaymores = 10;
    [Tooltip("How fast the player can place claymores (per second).")]
    public float placeRate = 2f;

    [Header("Placement Settings")]
    [Tooltip("How far in front of the player to place the claymore.")]
    public float forwardOffset = 1.5f;
    [Tooltip("How far down we raycast to find the ground.")]
    public float groundCheckDistance = 2f;
    [Tooltip("Slight height above the ground hit point.")]
    public float verticalOffsetFromGround = 0.05f;

    [Header("Animation Set Up")]
    public Animation anim;
    [Tooltip("Animation played when weapon is selected.")]
    public AnimationClip startClip;
    [Tooltip("Animation played when placing a claymore.")]
    public AnimationClip placeClip;
    [Tooltip("Delay before actually spawning the claymore (sync with place animation).")]
    public float placeDelay = 0.2f;

    [Header("Scope / ADS")]
    public ScopeManager scopeManager;

    [Header("Ammo UI")]
    public TextMeshProUGUI ammoText;
    public Image ammoIndicator;

    private int claymoresLeft;
    private float timeUntilNextPlace;
    private bool isWeaponActive = false;
    private NetworkObject localNetObj;

    void Start()
    {
        claymoresLeft = startingClaymores;

        if (GameMode.IsLAN)
            localNetObj = GetComponentInParent<NetworkObject>();

        SetScopeState(false);
    }

    public void SetWeaponActive(bool active)
    {
        isWeaponActive = active;

        if (active)
        {
            UpdateAmmoUI();

            if (anim != null && startClip != null)
            {
                anim.clip = startClip;
                anim.Stop();
                anim.Play();
            }
        }
    }

    private void SetScopeState(bool _isScoped)
    {
        // Claymores don't scope – always off
        if (scopeManager != null)
            scopeManager.SetScopeState(false);
    }

    private void UpdateAmmoUI()
    {
        if (ammoText != null)
            ammoText.text = $"<b>{claymoresLeft}/</b>{startingClaymores}";

        if (ammoIndicator != null)
        {
            if (startingClaymores > 0)
                ammoIndicator.fillAmount = (float)claymoresLeft / startingClaymores;
            else
                ammoIndicator.fillAmount = 0f;
        }
    }

    void Update()
    {
        if (!isWeaponActive)
            return;
        if (PauseMenuManager.IsGamePaused)
            return;

        timeUntilNextPlace = Mathf.Max(0, timeUntilNextPlace - Time.deltaTime);

        if (Input.GetButton("Fire1") && timeUntilNextPlace <= 0 && claymoresLeft > 0)
        {
            timeUntilNextPlace = 1f / placeRate;
            StartCoroutine(PlaceClaymoreRoutine());
        }
    }

    private IEnumerator PlaceClaymoreRoutine()
    {
        claymoresLeft--;
        UpdateAmmoUI();

        if (anim != null && placeClip != null)
        {
            anim.clip = placeClip;
            anim.Stop();
            anim.Play();
        }

        if (placeDelay > 0f)
            yield return new WaitForSeconds(placeDelay);

        if (localNetObj == null)
        {
            localNetObj = GetComponentInParent<NetworkObject>();
            if (localNetObj == null)
            {
                Debug.LogError("[ClaymoreWeaponLAN] No NetworkObject found in parents.");
                yield break;
            }
        }

        // Calculate position on the floor in front of the player
        Transform playerRoot = localNetObj.transform;
        Vector3 basePos = playerRoot.position + playerRoot.forward * forwardOffset;
        Vector3 rayOrigin = basePos + Vector3.up * (groundCheckDistance * 0.5f);

        Vector3 finalPos = basePos;

        // Snap to ground
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundCheckDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            finalPos = hit.point + hit.normal * verticalOffsetFromGround;
        }

        Quaternion rotation = Quaternion.LookRotation(playerRoot.forward, Vector3.up);

        // Ask your network shooter to spawn the claymore on the server
        PlayerGrenadeShooter shooter = GetComponentInParent<PlayerGrenadeShooter>();
        if (shooter != null)
        {
            shooter.PlantClaymoreServerRpc(finalPos, rotation);
        }
        else
        {
            Debug.LogError("[ClaymoreWeaponLAN] PlayerGrenadeShooter not found in parent!");
        }
    }

    /// <summary>
    /// Call this from your respawn logic if you want ammo back on respawn.
    /// </summary>
    public void ResetClaymores()
    {
        claymoresLeft = startingClaymores;
        UpdateAmmoUI();
    }
}
