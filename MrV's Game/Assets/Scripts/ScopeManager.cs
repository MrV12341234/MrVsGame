using System;
using UnityEngine;

public class ScopeManager : MonoBehaviour
{
    public GameObject scopeOverlay;
    public GameObject crosshair;
    public float scopeFOV;
    private float defaultFOV;

    private Camera cam;
    
    private void Start()
    {
        float savedFOV = PlayerPrefs.GetFloat("savedFOV", 60);
        GetComponent<Camera>().fieldOfView = savedFOV;
        
        cam = GetComponent<Camera>();
        defaultFOV = savedFOV;
    }


    public void SetScopeState(bool _isScoped)
    {
        cam.fieldOfView = _isScoped ? scopeFOV : defaultFOV;
        scopeOverlay.SetActive(_isScoped);
        crosshair.SetActive(!_isScoped);
    }
    
    
}
