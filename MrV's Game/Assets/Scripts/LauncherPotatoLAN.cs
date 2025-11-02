using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

[NetworkMode(NetworkMode.LAN)]

public class LauncherPotatoLAN : MonoBehaviour
{
    [Header("Weapon Stats")] 
    public float fireRate = 10f;
    
    [Header("Grenade Prefab")] 
    public GameObject grenadePrefab;
    [Header("Grenade Spawn Point")]
    public Transform muzzlePoint;
    
    [Header("Animation Set Up")]
    public Animation anim;
    public AnimationClip shootClip;
    public AnimationClip startClip;
    [Space]
    public float grenadeSpawningDelay = 0.35f;
    
    public ScopeManager scopeManager;

    [Header("Ammo Set Up")] 
    private int grenadesLeft = 3;
    public int startingGrenades = 3;
    [Space] 
    public TextMeshProUGUI ammoText;
    public Image ammoIndicator;
    
    private float timeUntilAllowNextShot;
    private bool isWeaponActive = false;
    
    void Start()
    {
        grenadesLeft = startingGrenades;
        SetScopeState(false);
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
        if (!isWeaponActive) 
        {
            return;
        }
        if (PauseMenuManager.IsGamePaused) 
        {
            return;
        }

        timeUntilAllowNextShot = Mathf.Max(0, timeUntilAllowNextShot - Time.deltaTime);
        
        if (Input.GetButton("Fire1") && timeUntilAllowNextShot <= 0 && grenadesLeft > 0)
        {
            timeUntilAllowNextShot = 1 / fireRate;
            StartCoroutine(ShootGrenade());
        }
    }

    IEnumerator ShootGrenade()
    {
        grenadesLeft--;
        
        UpdateAmmoUI();
        
        if (anim != null)
        {
            anim.clip = shootClip;
            anim.Stop();
            anim.Play();
        }

        yield return new WaitForSeconds(grenadeSpawningDelay);
        
        // Spawn grenade directly through the player's network component
        PlayerGrenadeShooter shooter = GetComponentInParent<PlayerGrenadeShooter>();
        if (shooter != null)
        {
            shooter.ShootGrenadeServerRpc(muzzlePoint.position, muzzlePoint.rotation);
        }
        else
        {
            Debug.LogError("PlayerGrenadeShooter not found in parent!");
        }
    }
}