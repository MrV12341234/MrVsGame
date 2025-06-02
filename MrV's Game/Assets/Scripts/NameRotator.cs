using UnityEngine;

public class NameRotator : MonoBehaviour
{

    void Update()
    {
        if (Camera.main != null)
        {
            transform.LookAt(Camera.main.transform);
        }
    }
}
