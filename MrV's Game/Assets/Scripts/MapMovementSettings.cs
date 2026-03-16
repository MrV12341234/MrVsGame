using UnityEngine;

public class MapMovementSettings : MonoBehaviour
{
    [Header("Enable map-specific movement")]
    public bool useOverrides = true;

    [Header("Movement Overrides")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 10f;
    public float maxVelocityChange = 10f;

    [Header("Jump Overrides")]
    public float jumpForce = 2.5f;
    public float extraGravity = 25f;

    [Header("Jump Input Style")]
    [Tooltip("Checked = jump uses GetKeyDown (single press). Unchecked = jump uses GetKey (holding space continues trying to jump).")]
    public bool useGetKeyDownForJump = false;

    public static MapMovementSettings Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }
}