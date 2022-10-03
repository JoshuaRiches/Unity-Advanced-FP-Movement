using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Advanced_Movement_System : MonoBehaviour
{
    #region VARIABLES
    [Header("Controls")]
    private PlayerControls controls;

    [Header("Components")]
    public Transform orientation;
    public Rigidbody rigidBody;

    [Header("Movement")]
    public float moveSpeed;
    public float groundDrag;

    [Header("Ground Check")]
    public Transform groundCheck;
    public LayerMask groundMask;
    private bool isGrounded;

    [Header("Jumping")]
    private bool readyToJump = true;
    private bool doubleJump;
    public float jumpCooldown;
    public float jumpForce;

    [Header("Aerial Movement")]
    public float airMultiplier;

    [Header("Wall Running")]
    private bool wallRunning;

    [Header("Slope Movement")]
    private float maxSlopeAngle;
    private RaycastHit slopeHit;

    [Header("Slope Sliding")]
    private bool exitingSlope;

    #region InputVariables
    private Vector2 moveInput;

    #endregion

    #endregion

    #region INITIALISE
    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void Start()
    {
        // jump when the jump control is pressed
        controls.Player.Jump.performed += ctx => Jump();
    }

    private void OnEnable()
    {
        controls.Enable();
    }
    private void OnDisable()
    {
        controls.Disable();
    }
    #endregion

    #region STATE_SYSTEM
    public enum MOVEMENT_STATE
    {
        WALK,
        SPRINT,
        AIR,
        CROUCHING,
        SLOPE_SLIDING,
        SLIDING,
        WALL_RUNNING
    }

    public MOVEMENT_STATE state;

    #endregion

    #region UPDATE/FIXED UPDATE
    private void Update()
    {
        // Function that checks if player is on ground
        GroundCheck();
        // function that checks for any inputs
        Inputs();
    }

    private void FixedUpdate()
    {
        // Function that moves the player
        MovePlayer();

        // when player is on a slope, use the slope movement
        if (OnSlope() && !exitingSlope)
        {
            SlopeMovement();
        }
        // if the player isnt wallrunning or on a slope then gravity should be on
        if (!wallRunning) rigidBody.useGravity = !OnSlope();
    }
    #endregion

    #region INPUTS
    private void Inputs()
    {
        // Get the input for the movement axis (wasd, joystick, etc)
        moveInput = controls.Player.Move.ReadValue<Vector2>();
    }
    #endregion

    #region BASIC MOVEMENT

    private void MovePlayer()
    {
        // TODO: stop movement for forward when on slope

        // Calculate the direction in which to move
        Vector3 v3MoveDir = orientation.forward * moveInput.y + orientation.right * moveInput.x;

        if (isGrounded)
        {
            // Add force to the player rigidbody to move
            rigidBody.AddForce(v3MoveDir.normalized * moveSpeed * 10f, ForceMode.Force); // TODO: make it only possible when grounded
        }
        else if (!isGrounded)
        {
            // add force to the player to move, but reduce it as they are in air
            rigidBody.AddForce(v3MoveDir.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
        }

    }
    #endregion

    #region JUMP
    private void Jump()
    {
        if (!readyToJump) return; // early out

        // if player is grounded or has a double jump to use and is not wall running, jump
        if (isGrounded || doubleJump && !wallRunning)
        {
            readyToJump = false;
            doubleJump = !doubleJump;

            exitingSlope = true;

            // reset the player's y velocity
            rigidBody.velocity = new Vector3(rigidBody.velocity.x, 0f, rigidBody.velocity.z);
            // Apply the jump force to the player
            rigidBody.AddForce(transform.up * jumpForce, ForceMode.Impulse);

            Invoke(nameof(ResetJump), jumpCooldown);

            // TODO: endSlide();
        }
    }

    private void ResetJump()
    {
        exitingSlope = false;
        readyToJump = true;
    }
    #endregion

    #region SLOPE MOVEMENT
    private void SlopeMovement()
    {
        // Calculate the direction in which to move
        Vector3 v3MoveDir = orientation.forward * moveInput.y + orientation.right * moveInput.x;
        // add the slope movement force
        rigidBody.AddForce(GetSlopeMoveDirection(v3MoveDir) * moveSpeed * 10f, ForceMode.Force);

        if (rigidBody.velocity.y > 0f)
        {
            // add a downward force to keep the player on the slope
            rigidBody.AddForce(Vector3.down * 80f, ForceMode.Force);
        }
    }

    private bool OnSlope()
    {
        // check if the ground beneath is a slope by checking if it is at a certain angle
        if (Physics.Raycast(groundCheck.position, Vector3.down, out slopeHit, 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }

        return false;
    }
    #endregion

    #region GROUND
    private void GroundCheck()
    {
        // check if there is a ground object under the player
        isGrounded = Physics.CheckSphere(groundCheck.position, 0.4f, groundMask);

        // Apply drag to the player
        if (isGrounded)
        {
            rigidBody.drag = groundDrag;
        }
        else
        {
            rigidBody.drag = 0f;
        }
    }
    #endregion
}
