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

    private Vector2 input;
    private bool isSprinting;
    private bool isJumping;
    private Rigidbody rb;
    private bool isGrounded;
    private AnimationSyncer _animationSyncer;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        _animationSyncer = GetComponent<AnimationSyncer>();
    }

    void Update()
    {
        if (PauseMenuManager.IsGamePaused)
            return;

        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        _animationSyncer.horizontal = input.x;
        _animationSyncer.vertical = input.y;
        
        isSprinting = Input.GetKey(KeyCode.LeftShift);

        isJumping = Input.GetKey(KeyCode.Space);
    }

    void FixedUpdate()
    {
        rb.AddForce(CalculateMovement(), ForceMode.VelocityChange);
        
        if (isJumping && isGrounded)
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
        float _speedToUse = isSprinting ? sprintSpeed : walkSpeed;
        
        Vector3 targetVelocity = transform.TransformDirection(new Vector3(input.x, 0f, input.y)) * _speedToUse;
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
}