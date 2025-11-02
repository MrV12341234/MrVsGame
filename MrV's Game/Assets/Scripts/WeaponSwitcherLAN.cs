using UnityEngine;

[NetworkMode(NetworkMode.LAN)]

public class WeaponSwitcherLAN : MonoBehaviour
{
    public bool isArmoury = false;
    private int selectedWeapon;
    
    private float timeUntilAllowSelectNextWeapon;
    private int previouslySelectedWeapon = -1;

    public Grenade grenadeScriptReference;

    void Start()
    {
        InitializeWeapons();
    }

    private void InitializeWeapons()
    {
        // Start with all weapons disabled
        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject weapon = transform.GetChild(i).gameObject;
            weapon.SetActive(false);
            
            // Call SetWeaponActive on each weapon component
            LauncherPotatoLAN launcher = weapon.GetComponent<LauncherPotatoLAN>();
            if (launcher != null)
            {
                launcher.SetWeaponActive(false);
            }
        }

        // Enable the first weapon
        if (transform.childCount > 0)
        {
            selectedWeapon = 0;
            SelectWeapon();
        }
    }

    void Update()
    {
        if (PauseMenuManager.IsGamePaused) return;

        timeUntilAllowSelectNextWeapon = Mathf.Max(0, timeUntilAllowSelectNextWeapon - Time.deltaTime);
        
        // Number key weapon selection
        if (Input.GetKeyDown(KeyCode.Alpha1)) selectedWeapon = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) selectedWeapon = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) selectedWeapon = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) selectedWeapon = 3;
        if (Input.GetKeyDown(KeyCode.Alpha5)) selectedWeapon = 4;
        if (Input.GetKeyDown(KeyCode.Alpha6)) selectedWeapon = 5;
        if (Input.GetKeyDown(KeyCode.Alpha7)) selectedWeapon = 6;
        if (Input.GetKeyDown(KeyCode.Alpha8)) selectedWeapon = 7;
        if (Input.GetKeyDown(KeyCode.Alpha9)) selectedWeapon = 8;

        // Mouse scroll wheel
        if (Input.GetAxis("Mouse ScrollWheel") > 0 && timeUntilAllowSelectNextWeapon <= 0)
        {
            timeUntilAllowSelectNextWeapon = 0.1f;
            selectedWeapon = (selectedWeapon >= transform.childCount - 1) ? 0 : selectedWeapon + 1;
        }
        
        if (Input.GetAxis("Mouse ScrollWheel") < 0 && timeUntilAllowSelectNextWeapon <= 0)
        {
            timeUntilAllowSelectNextWeapon = 0.1f;
            selectedWeapon = (selectedWeapon <= 0) ? transform.childCount - 1 : selectedWeapon - 1;
        }
        
        SelectWeapon();
    }
    
    void SelectWeapon()
    {
        selectedWeapon = Mathf.Clamp(selectedWeapon, 0, transform.childCount - 1);
        
        // Only change if weapon actually changed
        if (selectedWeapon != previouslySelectedWeapon)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject weapon = transform.GetChild(i).gameObject;
                bool isActive = i == selectedWeapon;
                weapon.SetActive(isActive);
                
                // Update weapon active state
                LauncherPotatoLAN launcher = weapon.GetComponent<LauncherPotatoLAN>();
                if (launcher != null)
                {
                    launcher.SetWeaponActive(isActive);
                }
            }

            previouslySelectedWeapon = selectedWeapon;
            
            // Toggle charge bar visibility for grenade
            if (grenadeScriptReference != null)
            {
                bool isGrenadeSelected = transform.GetChild(selectedWeapon).gameObject == grenadeScriptReference.gameObject;
                grenadeScriptReference.SetChargeBarActive(isGrenadeSelected);
            }
        }
    }
}