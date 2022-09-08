using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Movement : MonoBehaviour
{

    private float moveSpeed;
    [Header("Movement")]
    public float walkSpeed;
    public float sprintSpeed;

    public float groundDrag;

    [SerializeField] private Transform orientation;

    private float horizontalInput;
    private float forwardlInput;

    private Vector3 moveDirection;

    [SerializeField] private Rigidbody rb;


    [Header("Jumping")]
    public float jumpForce;
    public float jumpCooldown;
    public float airMultiplier;
    private bool readyToJump = true;

    [Header("Crouching")]
    public float crouchSpeed;
    public float crouchYScale;
    private float startYScale;
    [SerializeField] private bool canStand = true;
    private bool crouching = false;
    private bool tryingToStand = false;
    [SerializeField] private Transform crouchCheck;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    public LayerMask groundMask;
    bool isGrounded;

    [Header("Slope Handling")]
    public float maxSlopeAngle;
    private RaycastHit slopeHit;
    private bool exitingSlope;

    [Header("STATE")]
    public MOVEMENT_STATE state;

    public enum MOVEMENT_STATE
    {
        WALK,
        SPRINT,
        AIR,
        CROUCHING
    }

    private void Start()
    {
        readyToJump = true;
        startYScale = transform.localScale.y;
    }

    // Update is called once per frame
    void Update()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, 0.4f, groundMask);

        if (crouching)
        {
            canStand = !Physics.CheckSphere(crouchCheck.position, 1f, groundMask);

            if (tryingToStand && canStand)
            {
                Stand();
                tryingToStand = false;
            }
        }

        MoveInput();
        SpeedControl();
        StateHandler();

        if (isGrounded)
        {
            rb.drag = groundDrag;
        }
        else
        {
            rb.drag = 0;
        }
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void MoveInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        forwardlInput = Input.GetAxisRaw("Vertical");

        if (Input.GetKeyDown(KeyCode.Space) && readyToJump && isGrounded)
        {
            readyToJump = false;

            Jump();

            Invoke(nameof(ResetJump), jumpCooldown);
        }

        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
            crouching = true;
        }
        if (Input.GetKeyUp(KeyCode.LeftControl) && canStand)
        {
            Stand();
        }
        else if (Input.GetKeyUp(KeyCode.LeftControl) && !canStand)
        {
            tryingToStand = true;
        }
    }

    private void StateHandler()
    {
        // Crouch state
        if (crouching)
        {
            state = MOVEMENT_STATE.CROUCHING;
            moveSpeed = crouchSpeed;
        }

        // Sprinting state
        else if (Input.GetKey(KeyCode.LeftShift) && isGrounded)
        {
            state = MOVEMENT_STATE.SPRINT;
            moveSpeed = sprintSpeed;
        }

        // Walking state
        else if (isGrounded)
        {
            state = MOVEMENT_STATE.WALK;
            moveSpeed = walkSpeed;
        }

        // In air state
        else
        {
            state = MOVEMENT_STATE.AIR;
        }
    }

    private void MovePlayer()
    {
        moveDirection = orientation.forward * forwardlInput + orientation.right * horizontalInput;

        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection() * moveSpeed * 10f, ForceMode.Force);

            if (rb.velocity.y > 0)
            {
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
            }
        }

        if (isGrounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        }
        // In air
        else if (!isGrounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
        }

        // turn gravity off whilst on slope
        rb.useGravity = !OnSlope();
    }

    private void SpeedControl()
    {
        // limiting speed on slope
        if (OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
            {
                rb.velocity = rb.velocity.normalized * moveSpeed;
            }
        }
        // limit speed on ground
        else
        {
            Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
    }

    private void Jump()
    {
        exitingSlope = true;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    private void ResetJump()
    {
        exitingSlope = false;
        readyToJump = true;
    }

    private void Stand()
    {
        transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        crouching = false;
    }

    private bool OnSlope()
    {
        if (Physics.Raycast(groundCheck.position, Vector3.down, out slopeHit, 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }

        return false;
    }

    private Vector3 GetSlopeMoveDirection()
    {
        return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
    }
}
