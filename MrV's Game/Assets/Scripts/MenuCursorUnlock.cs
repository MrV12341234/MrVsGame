using UnityEngine;

public class MenuCursorUnlock : MonoBehaviour
{

    // Update is called once per frame
    void Update()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
