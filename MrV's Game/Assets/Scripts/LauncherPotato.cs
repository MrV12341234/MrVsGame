using UnityEngine;
using System;
using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;


public class LauncherPotato : MonoBehaviour
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
    
    // below is reference to crosshair but i didnt have this when creating this script
    // [Header("Crosshair") public DynamicCrosshair crosshair;
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

    private float timeUntilAllowNextShot;
    
    void Start()
    {
        grenadesLeft = startingGrenades;
        
        UpdateAmmoUI();
        
        SetScopeState(false);

        anim.clip = startClip;
        anim.Stop();
        anim.Play();
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

        timeUntilAllowNextShot = Mathf.Max(0, timeUntilAllowNextShot - Time.deltaTime);
        
        if (Input.GetButton("Fire1") && timeUntilAllowNextShot <= 0 && grenadesLeft > 0)
        {
            // throw the grenade
            timeUntilAllowNextShot = 1 / fireRate;
            StartCoroutine(ShootGrenade());
        }
    }

    IEnumerator ShootGrenade()
    {
        grenadesLeft--;
        
        UpdateAmmoUI();
        
        anim.clip = shootClip;
        anim.Stop();
        anim.Play();

        photonPlayerSoundsManager.photonView.RPC("RPC_PlayShootSound", RpcTarget.All, shootSoundIndex);
        
        yield return new WaitForSeconds(grenadeSpawningDelay);
        
        // Instantiate the grenade at the MuzzlePoint gameobject
        GameObject _grenade = PhotonNetwork.Instantiate(grenadePrefab.name, muzzlePoint.position, muzzlePoint.rotation);

        _grenade.GetComponent<LauncherProjectile>().player = transform.root.gameObject;

    }
}



