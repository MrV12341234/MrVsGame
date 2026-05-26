using UnityEngine;

public class Movement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 15f;
    public float maxVelocityChange = 10f;

    [Header("Jumping")]
    public float jumpForce = 5f;
    public float extraGravity = 10f;

    [Header("Jump Input Default")]
    [Tooltip("Checked = jump uses GetKeyDown by default. Unchecked = uses GetKey by default.")]
    public bool useGetKeyDownForJump = false;

    private Vector2 input;
    private bool isSprinting;
    private bool isJumpHeld;
    private bool jumpQueued;
    private bool _movementLocked;
    private Rigidbody rb;
    private bool isGrounded;
    private AnimationSyncer _animationSyncer;

    // default values from prefab
    private float _defaultWalkSpeed;
    private float _defaultSprintSpeed;
    private float _defaultMaxVelocityChange;
    private float _defaultJumpForce;
    private float _defaultExtraGravity;
    private bool _defaultUseGetKeyDownForJump;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _animationSyncer = GetComponent<AnimationSyncer>();

        // save prefab defaults once
        _defaultWalkSpeed = walkSpeed;
        _defaultSprintSpeed = sprintSpeed;
        _defaultMaxVelocityChange = maxVelocityChange;
        _defaultJumpForce = jumpForce;
        _defaultExtraGravity = extraGravity;
        _defaultUseGetKeyDownForJump = useGetKeyDownForJump;
    }

    void Update()
    {
        if (PauseMenuManager.IsGamePaused)
            return;
        
        if (_movementLocked)
        {
            input = Vector2.zero;
            isSprinting = false;
            isJumpHeld = false;
            jumpQueued = false;

            if (_animationSyncer != null)
            {
                _animationSyncer.horizontal = 0f;
                _animationSyncer.vertical = 0f;
            }

            return;
        }

        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        if (_animationSyncer != null)
        {
            _animationSyncer.horizontal = input.x;
            _animationSyncer.vertical = input.y;
        }

        isSprinting = Input.GetKey(KeyCode.LeftShift);

        if (useGetKeyDownForJump)
        {
            // queue one jump press
            if (Input.GetKeyDown(KeyCode.Space))
                jumpQueued = true;

            isJumpHeld = false;
        }
        else
        {
            isJumpHeld = Input.GetKey(KeyCode.Space);
        }
    }

    void FixedUpdate()
    {
        if (_movementLocked)
            return;
        
        rb.AddForce(CalculateMovement(), ForceMode.VelocityChange);

        bool shouldJump = false;

        if (useGetKeyDownForJump)
        {
            shouldJump = jumpQueued && isGrounded;
            jumpQueued = false; // consume it
        }
        else
        {
            shouldJump = isJumpHeld && isGrounded;
        }

        if (shouldJump)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
        else
        {
            rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
        }

        isGrounded = false;
    }

    Vector3 CalculateMovement()
    {
        float speedToUse = isSprinting ? sprintSpeed : walkSpeed;

        Vector3 targetVelocity = transform.TransformDirection(new Vector3(input.x, 0f, input.y)) * speedToUse;
        Vector3 velocityChange = targetVelocity - rb.linearVelocity;

        velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange);
        velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);
        velocityChange.y = 0f;

        if (input.magnitude < 0.5f)
        {
            return new Vector3(-rb.linearVelocity.x, 0, -rb.linearVelocity.z);
        }
        else
        {
            return velocityChange;
        }
    }

    void OnCollisionStay(Collision collision)
    {
        isGrounded = true;
    }

    public void ApplyMovementOverrides(
        float newWalkSpeed,
        float newSprintSpeed,
        float newMaxVelocityChange,
        float newJumpForce,
        float newExtraGravity,
        bool newUseGetKeyDownForJump)
    {
        walkSpeed = newWalkSpeed;
        sprintSpeed = newSprintSpeed;
        maxVelocityChange = newMaxVelocityChange;
        jumpForce = newJumpForce;
        extraGravity = newExtraGravity;
        useGetKeyDownForJump = newUseGetKeyDownForJump;
    }

    public void ResetToDefaults()
    {
        walkSpeed = _defaultWalkSpeed;
        sprintSpeed = _defaultSprintSpeed;
        maxVelocityChange = _defaultMaxVelocityChange;
        jumpForce = _defaultJumpForce;
        extraGravity = _defaultExtraGravity;
        useGetKeyDownForJump = _defaultUseGetKeyDownForJump;
        jumpQueued = false;
    }
    
    //method for controlling interactions with jump pads
    public void LaunchUpward(float launchForce, bool clearVerticalVelocity = true)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null) return;

        // Optional: remove existing up/down velocity so launch feels consistent
        if (clearVerticalVelocity)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        rb.AddForce(Vector3.up * launchForce, ForceMode.Impulse);

        // Prevent weird grounded behavior right after launch
        isGrounded = false;
        jumpQueued = false;
    }
    
    //used in vehicle driving
    public void SetMovementLocked(bool locked)
    {
        _movementLocked = locked;

        if (locked)
        {
            input = Vector2.zero;
            isSprinting = false;
            isJumpHeld = false;
            jumpQueued = false;
        }
    }
}
