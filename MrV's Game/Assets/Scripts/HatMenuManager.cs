using UnityEngine;

public class HatMenuManager : MonoBehaviour
{
    [Header("Hat Holders")] public Transform hatParent;

    [Header("UI")] public GameObject equippedButton;
    public GameObject equipButton;

    private int hatSelected;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        hatSelected = PlayerPrefs.GetInt("hatSelected");
    }

    public void EquipCurrentlyShownHat()
    {
        for (int i = 0; i < hatParent.childCount; i++)
        {
            if (hatParent.GetChild(i).gameObject.activeInHierarchy)
            {
                hatSelected = i;
                PlayerPrefs.SetInt("hatSelected", i);
            }
        }
        UpdateUI();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        bool isSelectedHatEquipped = false;

        for (int i = 0; i < hatParent.childCount; i++)
        {
            if (hatParent.GetChild(i).gameObject.activeInHierarchy && i == hatSelected)
                isSelectedHatEquipped = true;
        }
        
        equipButton.SetActive(!isSelectedHatEquipped);
        equippedButton.SetActive(isSelectedHatEquipped);
    }
}
