using UnityEngine;
using System;

public class UIEnableLogger : MonoBehaviour
{
    private void OnEnable()
    {
        Debug.Log($"CONNECTING UI ENABLED: {gameObject.name}\n{Environment.StackTrace}");
    }
}