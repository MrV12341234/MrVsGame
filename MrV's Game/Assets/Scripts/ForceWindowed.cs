using UnityEngine;

// add this script to menu manager on the Menu scene to force the game into full screen. 

public class ForceWindowed : MonoBehaviour
{
    void Awake()
    {
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.fullScreen = false;
        Screen.SetResolution(1024, 768, false);
    }
}