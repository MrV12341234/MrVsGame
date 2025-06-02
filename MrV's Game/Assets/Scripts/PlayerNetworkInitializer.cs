using UnityEngine;
using System;
using System.Linq;

public class PlayerNetworkInitializer : MonoBehaviour
{
    [SerializeField] private bool isLanMode; // Set this from your game manager or UI

    void Awake()
    {
        NetworkMode desiredMode = GameMode.IsLAN ? NetworkMode.LAN : NetworkMode.PHOTON;


        MonoBehaviour[] allScripts = GetComponents<MonoBehaviour>();

        foreach (var script in allScripts)
        {
            Type type = script.GetType();
            var modeAttr = type.GetCustomAttributes(typeof(NetworkModeAttribute), false)
                .FirstOrDefault() as NetworkModeAttribute;

            if (modeAttr != null)
            {
                script.enabled = modeAttr.Mode == desiredMode;
            }
        }
    }
}