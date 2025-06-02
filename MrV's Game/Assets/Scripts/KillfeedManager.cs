using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Photon.Pun;
public class KillfeedManager : MonoBehaviourPun
{
    public static KillfeedManager Instance;
    
    [Header("UI")] public GameObject killfeedItemPrefab;

    public Transform killfeedItemParent;

    private void Awake()
    {
        Instance = this;
    }

    [PunRPC]
    public void RPC_GetKill(string _killer, string _victim)
    {
        GameObject item = Instantiate(killfeedItemPrefab, killfeedItemParent);
        item.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = _killer + "  loves  " + _victim;
        StartCoroutine(DelayedEnableKillfeedItem(item.transform.GetChild(0).gameObject));
        
        Destroy(item, 6f);
    }

    IEnumerator DelayedEnableKillfeedItem(GameObject itemText)
    {
        itemText.gameObject.SetActive(false);
        
        yield return null;
        
        itemText.gameObject.SetActive(true);
    }
}
