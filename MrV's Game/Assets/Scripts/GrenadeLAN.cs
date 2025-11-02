using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

[NetworkMode(NetworkMode.LAN)]

public class GrenadeLAN : MonoBehaviour
{
    [Header("Weapon Stats")]
    public float fireRate = 1f;

    [Header("Grenade Prefab")]
    public GameObject grenadePrefab;

    [Header("Animation Set Up")]
    public Animation anim;
    public AnimationClip shootClip;
    public AnimationClip startClip;
    [Space] 
    public float grenadeSpawningDelay = 0.35f;

    [Header("Scope Reference")]
    public ScopeManager scopeManager;

    [Header("Ammo Set Up")]
    private int grenadesLeft = 3;
    public int startingGrenades = 3;
    [Space]
    public TextMeshProUGUI ammoText;
    public Image ammoIndicator;

    [Header("Charge Throw Settings")]
    public float minThrowForce = 2f; // Reduced from 300f
    public float maxThrowForce = 50f; // changing inspector doesnt seem to work. Maybe b/c there is no official prefab?
    public float maxChargeTime = 1.1f;
    
    [Header("UI - Charge Bar")] 
    public GameObject chargeBarContainer;
    public Image chargeBarFill;
    
    [Header("Camera Reference")]
    public Transform cameraTransform;

    private float timeUntilAllowNextThrow;
    private float currentChargeTime = 0f;
    private bool isCharging = false;
    private bool isWeaponActive = false;
    private PlayerGrenadeShooter playerGrenadeShooter;

    void Start()
    {
        grenadesLeft = startingGrenades;
        UpdateAmmoUI();
        SetScopeState(false);

        // Get reference to player's grenade shooter component
        playerGrenadeShooter = GetComponentInParent<PlayerGrenadeShooter>();
        
        if (anim != null)
        {
            anim.clip = startClip;
            anim.Stop();
            anim.Play();
        }
    }

    public void SetWeaponActive(bool active)
    {
        isWeaponActive = active;
        if (active)
        {
            UpdateAmmoUI();
            if (anim != null)
            {
                anim.clip = startClip;
                anim.Stop();
                anim.Play();
            }
        }
        else
        {
            // Reset charging if weapon is switched away
            if (isCharging)
            {
                isCharging = false;
                currentChargeTime = 0f;
                if (chargeBarFill != null)
                    chargeBarFill.fillAmount = 0f;
            }
        }
    }

    public void SetChargeBarActive(bool isActive)
    {
        if (chargeBarContainer != null)
            chargeBarContainer.SetActive(isActive);
    }

    private void SetScopeState(bool _isScoped)
    {
        scopeManager?.SetScopeState(_isScoped);
    }

    private void UpdateAmmoUI()
    {
        if (ammoText != null)
            ammoText.text = $"<b>{grenadesLeft}/</b>{startingGrenades}";
        if (ammoIndicator != null)
            ammoIndicator.fillAmount = (float)grenadesLeft / startingGrenades;
    }

    private void Update()
    {
        if (!isWeaponActive) return;
        if (PauseMenuManager.IsGamePaused) return;

        timeUntilAllowNextThrow = Mathf.Max(0, timeUntilAllowNextThrow - Time.deltaTime);

        // Start charging when Fire1 is pressed
        if (Input.GetButtonDown("Fire1") && timeUntilAllowNextThrow <= 0 && grenadesLeft > 0)
        {
            isCharging = true;
            currentChargeTime = 0f;
        }

        // Continue charging while button is held
        if (isCharging)
        {
            currentChargeTime += Time.deltaTime;
            currentChargeTime = Mathf.Min(currentChargeTime, maxChargeTime);
            if (chargeBarFill != null)
                chargeBarFill.fillAmount = currentChargeTime / maxChargeTime;
        }

        // Throw when button is released
        if (Input.GetButtonUp("Fire1") && isCharging)
        {
            isCharging = false;
            timeUntilAllowNextThrow = 1 / fireRate;
            StartCoroutine(ThrowGrenade(currentChargeTime));
        }
    }

    IEnumerator ThrowGrenade(float chargeTime)
    {
        grenadesLeft--;
        UpdateAmmoUI();

        if (anim != null)
        {
            anim.clip = shootClip;
            anim.Stop();
            anim.Play();
        }
        
        if (chargeBarFill != null)
            chargeBarFill.fillAmount = 0f;

        yield return new WaitForSeconds(grenadeSpawningDelay);

        // Calculate force based on charge time
        float t = chargeTime / maxChargeTime;
        float throwForce = Mathf.Lerp(minThrowForce, maxThrowForce, t);
        
        // DEBUG: Log the calculated force
        Debug.Log($"Charge time: {chargeTime}, Calculated throw force: {throwForce}");

        // Calculate spawn position (.9f in front of player camera and -.1f down)
        Vector3 spawnPosition = cameraTransform.position + cameraTransform.forward * 0.9f + cameraTransform.up * -0.1f;

        // Use player's grenade shooter to spawn on network
        if (playerGrenadeShooter != null)
        {
            // REMOVED: grenadeLifetime parameter
            playerGrenadeShooter.ThrowGrenadeServerRpc(spawnPosition, cameraTransform.rotation, throwForce);
        }
        else
        {
            Debug.LogError("PlayerGrenadeShooter not found!");
        }
    }
}