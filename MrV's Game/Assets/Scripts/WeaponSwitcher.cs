using UnityEngine;
using Photon.Pun;

public class WeaponSwitcher : MonoBehaviour
{
    public bool isArmoury = false;
    private int selectedWeapon;
    public PhotonView tpWeaponManager;

    private float timeUntilAllowSelectNextWeapon;

    private int previouslySelectedWeapon = -1;

    public Grenade grenadeScriptReference; // reference to gernade script
    

    void Update()
    {
        timeUntilAllowSelectNextWeapon = Mathf.Max(0, timeUntilAllowSelectNextWeapon - Time.deltaTime);
        
        if (Input.GetKeyDown(KeyCode.Alpha1)) selectedWeapon = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) selectedWeapon = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) selectedWeapon = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) selectedWeapon = 3;
        if (Input.GetKeyDown(KeyCode.Alpha5)) selectedWeapon = 4;
        if (Input.GetKeyDown(KeyCode.Alpha6)) selectedWeapon = 5;
        if (Input.GetKeyDown(KeyCode.Alpha7)) selectedWeapon = 6;
        if (Input.GetKeyDown(KeyCode.Alpha8)) selectedWeapon = 7;
        if (Input.GetKeyDown(KeyCode.Alpha9)) selectedWeapon = 8;

        if (Input.GetAxis("Mouse ScrollWheel") > 0 && timeUntilAllowSelectNextWeapon <= 0)
        {
            timeUntilAllowSelectNextWeapon = 0.1f;
            
            if (selectedWeapon >= transform.childCount - 1)
            {
                selectedWeapon = 0;
            }
            else
            {
                selectedWeapon += 1;
            }
        }
        
        if (Input.GetAxis("Mouse ScrollWheel") < 0 && timeUntilAllowSelectNextWeapon <= 0)
        {
            timeUntilAllowSelectNextWeapon = 0.1f;
            
            if (selectedWeapon <= 0)
            {
                selectedWeapon = transform.childCount - 1;
            }
            else
            {
                selectedWeapon -= 1;
            }
        }
        
        SelectWeapon();
    }
    
    void SelectWeapon()
    {
        selectedWeapon = Mathf.Clamp(selectedWeapon, 0, transform.childCount - 1);
        
        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(i == selectedWeapon);
        }

        if (!isArmoury) if (selectedWeapon != previouslySelectedWeapon) tpWeaponManager.RPC("RPC_SetTPWeapon",RpcTarget.OthersBuffered, (byte)selectedWeapon);

        previouslySelectedWeapon = selectedWeapon;
        
        // Toggle charge bar visibility based on selected weapon
        if (grenadeScriptReference != null)
        {
            bool isGrenadeSelected = transform.GetChild(selectedWeapon).gameObject == grenadeScriptReference.gameObject;
            grenadeScriptReference.SetChargeBarActive(isGrenadeSelected);
        }
    }
}
