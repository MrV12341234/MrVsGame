using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class Grenade : MonoBehaviourPun
{
    [Header("Weapon Stats")]
    public float fireRate = 10f;

    [Header("Grenade Prefab")]
    public GameObject grenadePrefab;

    [Header("Animation Set Up")]
    public Animation anim;
    public AnimationClip shootClip;
    public AnimationClip startClip;
    [Space] public float grenadeSpawningDelay = 0.35f;

    [Header("Scope Reference")]
    public ScopeManager scopeManager;

    [Header("Ammo Set Up")]
    private int grenadesLeft = 3;
    public int startingGrenades = 3;
    [Space]
    public TextMeshProUGUI ammoText;
    public Image ammoIndicator;

    [Header("Shoot SFX")]
    public PhotonPlayerSoundsManager photonPlayerSoundsManager;
    [Space]
    public byte shootSoundIndex = 0;

    [Header("Camera Reference")]
    public Transform cameraTransform;

    [Header("Charge Throw Settings")]
    public float minThrowForce = 300f; 
    public float maxThrowForce = 1800f; // throw force after maxchargtime is reached
    public float maxChargeTime = 1.1f;
    
    [Header("UI - Charge Bar")] 
    public GameObject chargeBarContainer;
    public Image chargeBarFill;
    

    private float timeUntilAllowNextThrow;
    private float currentChargeTime = 0f;
    private bool isCharging = false;

    void Start()
    {
        grenadesLeft = startingGrenades;

        UpdateAmmoUI();
        SetScopeState(false);

        anim.clip = startClip;
        anim.Stop();
        anim.Play();
    }

    public void SetChargeBarActive(bool isActive)
    {
        chargeBarContainer.SetActive(isActive);
    }

    private void SetScopeState(bool _isScoped)
    {
        scopeManager.SetScopeState(_isScoped);
    }

    private void UpdateAmmoUI()
    {
        ammoText.text = $"<b>{grenadesLeft}/</b>{startingGrenades}";
        ammoIndicator.fillAmount = (float)grenadesLeft / startingGrenades;
    }

    private void Update()
    {
        if (PauseMenuManager.IsGamePaused)
            return;

        timeUntilAllowNextThrow = Mathf.Max(0, timeUntilAllowNextThrow - Time.deltaTime);

        if (Input.GetButtonDown("Fire1") && timeUntilAllowNextThrow <= 0 && grenadesLeft > 0)
        {
            // Start charging
            isCharging = true;
            currentChargeTime = 0f;
        }

        if (isCharging)
        {
            currentChargeTime += Time.deltaTime;
            currentChargeTime = Mathf.Min(currentChargeTime, maxChargeTime); // clamp
            chargeBarFill.fillAmount = currentChargeTime / maxChargeTime;

        }

        if (Input.GetButtonUp("Fire1") && isCharging)
        {
            // Release and throw
            isCharging = false;
            timeUntilAllowNextThrow = 1 / fireRate;
            StartCoroutine(ThrowGrenade(currentChargeTime));
        }
    }

    IEnumerator ThrowGrenade(float chargeTime)
    {
        grenadesLeft--;

        UpdateAmmoUI();

        anim.clip = shootClip;
        anim.Stop();
        anim.Play();
        
        chargeBarFill.fillAmount = 0f;

        photonPlayerSoundsManager.photonView.RPC("RPC_PlayShootSound", RpcTarget.All, shootSoundIndex);

        yield return new WaitForSeconds(grenadeSpawningDelay);

        // Calculate force based on charge time
        float t = chargeTime / maxChargeTime;
        float throwForce = Mathf.Lerp(minThrowForce, maxThrowForce, t);

        // Calculate spawn position (.9f in front of player camera and -.1f down, change if needed)
        Vector3 spawnPosition = cameraTransform.position + cameraTransform.forward * 0.9f + cameraTransform.up * -0.1f;

        // Instantiate and apply force
        GameObject _grenade = PhotonNetwork.Instantiate(grenadePrefab.name, spawnPosition, cameraTransform.rotation);
        Projectile projectile = _grenade.GetComponent<Projectile>();
        projectile.player = transform.root.gameObject;
        projectile.throwForce = throwForce;
    }
}

