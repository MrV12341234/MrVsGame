using System.Collections;
using Photon.Pun;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class Weapon : MonoBehaviour
{
    [Header("Weapon Stats")]
    public float fireRate = 10f;
    public int damagePerShot = 25;
    public float hitscanDistance = 500f;
    [Space] public float spread = 0.05f;
    public int pelletsCount = 1;

    [Header("Animation Set Up")]
    public Animation anim;
    public AnimationClip reloadClip;
    public AnimationClip[] shootClips;
    public AnimationClip startClip;

    [Header("Scoping Setting")] public ScopeManager scopeManager;
    public bool isScopedWeapon = false;
    public SkinnedMeshRenderer[] MeshRenderers;
    public float spreadWhileScoped = 0f;

    [Header("Hit and Kills Manager")]
    public PlayerHitAndKillsManager playerHitAndKillsManager;

    [Header("Hit Particle Set Up")]
    public GameObject concreteHitParticle;
    public GameObject playerHitParticle;

    [Header("Shoot SFX")]
    public PhotonPlayerSoundsManager photonPlayerSoundsManager;
    [Space] public byte shootSoundIndex = 0;

    [Header("Ammo Set Up")]
    public int magSize = 30;
    public int currentAmmoInMag = 30;
    [Space] public TextMeshProUGUI ammoText;
    public Image ammoIndicator;

    [Header("Muzzle Flash Set Up")]
    public Transform muzzleFlashSpawnPoint;
    public GameObject muzzleFlashPrefab;

    [Header("Camera Reference")]
    public Transform cameraTransform;

    [Header("Recoil Settings")]
    [Range(0, 2)] public float recoverpercent = 0.7f;
    public float recoilUp = 1f;
    public float recoilBack = 0f;

    private float timeUntilAllowNextShot;
    private bool isScoped = false;

    private Vector3 originalPosition;
    private Vector3 recoilVelocity = Vector3.zero;

    private float recoilLength;
    private float recoverLength;

    private bool recoiling;
    private bool recovering;

    private NetworkObject localNetworkObject; // LAN self-hit prevention

    void Start()
    {
        UpdateAmmoUI();

        if (isScopedWeapon && scopeManager != null)
            SetScopeState(false);

        anim.clip = startClip;
        anim.Stop();
        anim.Play();

        originalPosition = transform.localPosition;
        recoilLength = 0;
        recoverLength = 1 / fireRate + recoverpercent;

        if (GameMode.IsLAN)
        {
            localNetworkObject = GetComponentInParent<NetworkObject>();
        }
    }
    void Update()
    {
        if (PauseMenuManager.IsGamePaused)
            return;

        timeUntilAllowNextShot = Mathf.Max(0, timeUntilAllowNextShot - Time.deltaTime);

        if (Input.GetButton("Fire1") && timeUntilAllowNextShot <= 0 && currentAmmoInMag > 0 && !IsPlayingReloadClip())
        {
            HitscanShoot();
            timeUntilAllowNextShot = 1 / fireRate;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Reload();
        }

        if (recoiling) Recoil();
        if (recovering) Recovering();

        if (isScopedWeapon && scopeManager != null)
            SetScopeState(Input.GetButton("Fire2"));
    }

    private void SetScopeState(bool _isScoped)
    {
        scopeManager.SetScopeState(_isScoped);
        isScoped = _isScoped;

        foreach (var _renderer in MeshRenderers)
        {
            _renderer.enabled = !_isScoped;
        }
    }

    private bool IsPlayingReloadClip() => anim.isPlaying && anim.clip == reloadClip;

    private void Reload()
    {
        anim.clip = reloadClip;
        anim.Stop();
        anim.Play();

        currentAmmoInMag = magSize;
        UpdateAmmoUI();
    }

    private void UpdateAmmoUI()
    {
        if (currentAmmoInMag > 150)
        {
            ammoText.text = "\u221e";
            ammoIndicator.fillAmount = 1f;
        }
        else
        {
            ammoText.text = $"<b>{currentAmmoInMag}/</b>{magSize}";
            ammoIndicator.fillAmount = (float)currentAmmoInMag / magSize;
        }
    }

    void HitscanShoot()
    {
        currentAmmoInMag--;
        UpdateAmmoUI();

        anim.clip = shootClips[Random.Range(0, shootClips.Length)];
        anim.Stop();
        anim.Play();

        photonPlayerSoundsManager.photonView.RPC("RPC_PlayShootSound", Photon.Pun.RpcTarget.All, shootSoundIndex);

        if (muzzleFlashPrefab && muzzleFlashSpawnPoint)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, muzzleFlashSpawnPoint.position, muzzleFlashSpawnPoint.rotation);
            flash.transform.parent = muzzleFlashSpawnPoint;
            Destroy(flash, 1f);
        }

        
        for (int i = 0; i < pelletsCount; i++)
        {
            Vector3 dir = cameraTransform.forward;
            float spreadUsed = isScoped ? spreadWhileScoped : spread;
            dir += cameraTransform.right * Random.Range(-spreadUsed, spreadUsed);
            dir += cameraTransform.up * Random.Range(-spreadUsed, spreadUsed);
            dir.Normalize();

            recoiling = true;
            recovering = false;

            if (Physics.Raycast(cameraTransform.position, dir, out RaycastHit hit, hitscanDistance))
            {
                Quaternion rotation = Quaternion.LookRotation(hit.normal);

                if (hit.transform.CompareTag("Player"))
                {
                    if (GameMode.IsLAN)
                    {
                        var hitNetObj = hit.transform.GetComponent<NetworkObject>();
                        if (hitNetObj != null && localNetworkObject != null && hitNetObj.NetworkObjectId == localNetworkObject.NetworkObjectId)
                        {
                            Debug.Log("[LAN] Skipping self-hit");
                            continue;
                        }
                        // LAN damage
                        var health = hit.transform.GetComponent<PlayerHealthLan>();
                        if (health != null)
                        {
                            health.TakeDamageServerRpc(damagePerShot);
                        }
                        Instantiate(playerHitParticle, hit.point, rotation);
                    }
                    else // Photon
                    {
                        PhotonView target = hit.transform.GetComponent<PhotonView>();
                        PhotonView shooter = GetComponentInParent<PhotonView>();

                        if (target != null && shooter != null && target.ViewID != shooter.ViewID)
                        {
                            target.RPC("RPC_TakeDamage", Photon.Pun.RpcTarget.All, damagePerShot);
                            GameObject particle = PhotonNetwork.Instantiate(playerHitParticle.name, hit.point, rotation);
                            StartCoroutine(DestroyParticleAfterTime(particle, 2f));

                            if (hit.transform.GetComponent<PlayerHealth>().health <= 0)
                                playerHitAndKillsManager.GetKill(target.Owner.NickName);
                            else
                                playerHitAndKillsManager.GetHit(damagePerShot);
                        }
                    }
                }
                else
                {
                    if (GameMode.IsLAN)
                        Instantiate(concreteHitParticle, hit.point, rotation);
                    else
                    {
                        GameObject particle = PhotonNetwork.Instantiate(concreteHitParticle.name, hit.point, rotation);
                        StartCoroutine(DestroyParticleAfterTime(particle, 2f));
                    }
                }
            }
        }
    }

    private IEnumerator DestroyParticleAfterTime(GameObject particle, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (GameMode.IsLAN)
        {
            Destroy(particle);
        }
        else if (particle && particle.GetPhotonView()?.IsMine == true)
        {
            PhotonNetwork.Destroy(particle);
        }
    }

    void Recoil()
    {
        Vector3 target = new Vector3(originalPosition.x, originalPosition.y + recoilUp, originalPosition.z - recoilBack);
        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, target, ref recoilVelocity, recoilLength);

        if (transform.localPosition == target)
        {
            recoiling = false;
            recovering = true;
        }
    }

    void Recovering()
    {
        Vector3 target = originalPosition;
        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, target, ref recoilVelocity, recoverLength);

        if (transform.localPosition == target)
        {
            recoiling = false;
            recovering = false;
        }
    }
}
