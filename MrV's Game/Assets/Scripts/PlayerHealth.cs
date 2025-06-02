using System;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[NetworkMode(NetworkMode.PHOTON)]
public class PlayerHealth : MonoBehaviourPun
{
    [Header("Health Set Up")]
    public int maxHealth;

    [Header("UI Set Up")] 
    public TextMeshProUGUI healthText;
    public Image healthFillImage;
    
    
    [HideInInspector]public int health;

    private void Start()
    {
        health = maxHealth;
        UpdateUI();
    }

    private void UpdateUI()
    {
        healthText.text = $"<b>{health}/</b>{maxHealth}";
        healthFillImage.fillAmount = (float)health / maxHealth;
    }

    [PunRPC]
    public void RPC_TakeDamage(int _damage)
    {
        health = Mathf.Max(0, health -= _damage);

        if (photonView.IsMine)
        {
            UpdateUI();

            
            if (health <= 0)
            {
                LocalPlayerKDManager.Instance.OnDied();
                
                // Death code!
                RoomManager.Instance.ShowQuiz();
                
                PhotonNetwork.Destroy(gameObject);
            }
        }
        else
        {
            if (health <= 0)
            {
                gameObject.SetActive(false);
            }
        }
        
    }

    public void Update()
    {
        if (transform.position.y < -70 && GetComponent<PhotonView>().IsMine)
        {
            GetComponent<PhotonView>().RPC("RPC_TakeDamage", RpcTarget.All, 999);
        }
    }
}