using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public static MouseLook instance;

    [Header("Settings")]
    public Vector2 clampInDegrees = new Vector2(360, 180);
    public bool lockCursor = true;
    [Space]
    
    [Header("Mouse Settings")]
    [Tooltip("X = Horizontal sensitivity, Y = Vertical sensitivity")]
    public Vector2 sensitivity = new Vector2(2, 2);
    public Vector2 smoothing = new Vector2(3, 3);

    [Header("First Person")]
    public GameObject characterBody;
    
    [Header("Vehicle / Seat Lock")]
    [Tooltip("When true, mouse look will NOT rotate the player body. Used while seated in vehicles.")]
    public bool lockCharacterBodyRotation = false;

    private Vector2 targetDirection;
    private Vector2 targetCharacterDirection;

    private Vector2 _mouseAbsolute;
    private Vector2 _smoothMouse;

    private Vector2 mouseDelta;
    
    private float _lockedYaw;

    [HideInInspector]
    public bool scoped;

    void Start()
    {
        instance = this;

        // Set target direction to the camera's initial orientation.
        targetDirection = transform.localRotation.eulerAngles;
        
        // get sensitivity setting
        float savedSens = PlayerPrefs.GetFloat("savedSens", 1);
        sensitivity *= savedSens;

        // Set target direction for the character body to its inital state.
        if (characterBody)
            targetCharacterDirection = characterBody.transform.localRotation.eulerAngles;
        
        if (lockCursor)
            LockCursor();

    }

    public void LockCursor()
    {
        // make the cursor hidden and locked
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    public void SetCharacterBodyRotationLocked(bool locked)
    {
        if (lockCharacterBodyRotation == locked)
            return;

        lockCharacterBodyRotation = locked;

        // Reset look smoothing so there is no snap or spin when changing states
        _smoothMouse = Vector2.zero;
        _mouseAbsolute.x = 0f;
        _lockedYaw = 0f;

        if (characterBody != null)
            targetCharacterDirection = characterBody.transform.localRotation.eulerAngles;
    }

    void Update()
    {
        if (PauseMenuManager.IsGamePaused)
            return;

        // Allow the script to clamp based on a desired target value.
        var targetOrientation = Quaternion.Euler(targetDirection);
        var targetCharacterOrientation = Quaternion.Euler(targetCharacterDirection);

        // Get raw mouse input for a cleaner reading on more sensitive mice.
        mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

        // Scale input against the sensitivity setting and multiply that against the smoothing value.
        mouseDelta = Vector2.Scale(mouseDelta, new Vector2(sensitivity.x * smoothing.x, sensitivity.y * smoothing.y));

        // Interpolate mouse movement over time to apply smoothing delta.
        _smoothMouse.x = Mathf.Lerp(_smoothMouse.x, mouseDelta.x, 1f / smoothing.x);
        _smoothMouse.y = Mathf.Lerp(_smoothMouse.y, mouseDelta.y, 1f / smoothing.y);

        // Vertical look always accumulates normally
        _mouseAbsolute.y += _smoothMouse.y;

        // Clamp vertical look
        if (clampInDegrees.y < 360)
            _mouseAbsolute.y = Mathf.Clamp(_mouseAbsolute.y, -clampInDegrees.y * 0.5f, clampInDegrees.y * 0.5f);

        // Normal FPS mode = rotate the player body with yaw
        if (characterBody && !lockCharacterBodyRotation)
        {
            _mouseAbsolute.x += _smoothMouse.x;

            if (clampInDegrees.x < 360)
                _mouseAbsolute.x = Mathf.Clamp(_mouseAbsolute.x, -clampInDegrees.x * 0.5f, clampInDegrees.x * 0.5f);

            transform.localRotation =
                Quaternion.AngleAxis(-_mouseAbsolute.y, targetOrientation * Vector3.right) * targetOrientation;

            var yRotation = Quaternion.AngleAxis(_mouseAbsolute.x, Vector3.up);
            characterBody.transform.localRotation = yRotation * targetCharacterOrientation;
        }
        else
        {
            // Seated mode = camera can still look around, but player body does NOT rotate
            _lockedYaw += _smoothMouse.x;

            if (clampInDegrees.x < 360)
                _lockedYaw = Mathf.Clamp(_lockedYaw, -clampInDegrees.x * 0.5f, clampInDegrees.x * 0.5f);

            transform.localRotation = Quaternion.Euler(-_mouseAbsolute.y, _lockedYaw, 0f);
        }
    }
}
