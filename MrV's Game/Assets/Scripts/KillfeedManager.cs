using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Photon.Pun;

[NetworkMode(NetworkMode.PHOTON)]
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
        //instantiate the killfeed prefab ui
        GameObject item = Instantiate(killfeedItemPrefab, killfeedItemParent);
        item.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = _killer + "  loves  " + _victim;
        StartCoroutine(DelayedEnableKillfeedItem(item.transform.GetChild(0).gameObject));
        
        //destroy the killfeed prefab ui after 6 seconds(6f)
        Destroy(item, 6f);
    }

    IEnumerator DelayedEnableKillfeedItem(GameObject itemText)
    {
        itemText.gameObject.SetActive(false);
        
        yield return null;
        
        itemText.gameObject.SetActive(true);
    }
}