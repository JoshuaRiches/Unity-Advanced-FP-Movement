using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Movement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed;

    public float walkSpeed;
    public float sprintSpeed;
    public float slideSpeed;
    public float wallRunSpeed;

    public float speedIncreaseMultiplier;
    public float slopeIncreaseMultiplier;

    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;

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
    public bool doubleJump;

    [Header("Crouching")]
    public float crouchSpeed;
    public float crouchYScale;
    private float startYScale;
    [SerializeField] private bool canStand = true;
    [SerializeField] private bool crouching = false;
    private bool tryingToStand = false;
    [SerializeField] private Transform crouchCheck;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    public LayerMask groundMask;
    public bool isGrounded;

    [Header("Slope Handling")]
    public float maxSlopeAngle;
    private RaycastHit slopeHit;
    private bool exitingSlope;
    private bool onSlope;
    public LayerMask slopeMask;

    [SerializeField] private Slope_Slide slopeSlide;

    private bool isSprinting;

    public bool wallRunning;

    [Header("Slide")]
    public bool isSliding;

    [Header("STATE")]
    public MOVEMENT_STATE state;

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

    public bool GetCanStand() { return canStand; }

    private void Start()
    {
        readyToJump = true;
        startYScale = transform.localScale.y;
    }

    // Update is called once per frame
    void Update()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, 0.4f, groundMask);

        onSlope = Physics.CheckSphere(groundCheck.position, 0.4f, slopeMask);

        if (onSlope)
        {
            slopeSlide.StartSlide();
        }
        else
        {
            slopeSlide.StopSlide();
        }

        if (crouching || isSliding)
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

        if (isGrounded && !Input.GetKey(KeyCode.Space)) doubleJump = false;
        if (wallRunning) doubleJump = false;
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void MoveInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        forwardlInput = Input.GetAxisRaw("Vertical");

        if (Input.GetKeyDown(KeyCode.Space) && readyToJump && (isGrounded || doubleJump && !wallRunning))
        {
            readyToJump = false;
            doubleJump = !doubleJump;

            Jump();

            Invoke(nameof(ResetJump), jumpCooldown);
        }

        if (Input.GetKeyDown(KeyCode.C) && state != MOVEMENT_STATE.SPRINT)
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
            crouching = true;
        }

        if (tryingToStand && !crouching)
        {
            crouching = true;
        }

        if (Input.GetKeyUp(KeyCode.C) && canStand)
        {
            Stand();
        }
        else if (Input.GetKeyUp(KeyCode.C) && !canStand)
        {
            tryingToStand = true;
        }
    }

    public void EndSlide()
    {
        isSliding = false;

        canStand = !Physics.CheckSphere(crouchCheck.position, 1f, groundMask);

        if (canStand)
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }
        else
        {
            tryingToStand = true;
        }
    }

    private void StateHandler()
    {
        // Wall Running
        if (wallRunning)
        {
            state = MOVEMENT_STATE.WALL_RUNNING;
            desiredMoveSpeed = wallRunSpeed;
        }

        // Crouch state
        if (crouching)
        {
            state = MOVEMENT_STATE.CROUCHING;
            desiredMoveSpeed = crouchSpeed;
        }
        // Sliding state
        else if (isSliding)
        {
            state = MOVEMENT_STATE.SLIDING;
            desiredMoveSpeed = sprintSpeed;
        }
        // Sprinting state
        else if (Input.GetKey(KeyCode.LeftShift) && isGrounded)
        {
            state = MOVEMENT_STATE.SPRINT;
            desiredMoveSpeed = sprintSpeed;
        }
        // sliding on slope state
        else if (onSlope)
        {
            state = MOVEMENT_STATE.SLOPE_SLIDING;

            if (rb.velocity.y < 0.1f)
            {
                desiredMoveSpeed = slideSpeed;
            }
            else
            {
                desiredMoveSpeed = sprintSpeed;
            }
        }
        // Walking state
        else if (isGrounded)
        {
            state = MOVEMENT_STATE.WALK;
            desiredMoveSpeed = walkSpeed;
        }
        // In air state
        else
        {
            state = MOVEMENT_STATE.AIR;
        }


        if (horizontalInput == 0 && forwardlInput == 0 && !onSlope)
        {
            StopAllCoroutines();
            moveSpeed = desiredMoveSpeed;
        }

        if (Mathf.Abs(desiredMoveSpeed - lastDesiredMoveSpeed) > 5f && moveSpeed != 0)
        {
            StopAllCoroutines();
            StartCoroutine(SmoothlyLerpMoveSpeed());
        }
        else
        {
            moveSpeed = desiredMoveSpeed;
        }

        lastDesiredMoveSpeed = desiredMoveSpeed;
    }

    private IEnumerator SmoothlyLerpMoveSpeed()
    {
        // smoothly lerp movement speed to desired value
        float time = 0;
        float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed);
        float startValue = moveSpeed;

        while (time < difference)
        {
            moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, time / difference);

            if (OnSlope())
            {
                float slopeAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
                float slopeAngleIncrease = 1 + (slopeAngle / 90f);

                time += Time.deltaTime * speedIncreaseMultiplier * slopeIncreaseMultiplier * slopeAngleIncrease;
            }
            else
            {
                time += Time.deltaTime * speedIncreaseMultiplier;
            }

            yield return null;
        }

        moveSpeed = desiredMoveSpeed;
    }

    private void MovePlayer()
    {
        moveDirection = orientation.forward * forwardlInput + orientation.right * horizontalInput;

        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection(moveDirection) * moveSpeed * 10f, ForceMode.Force);

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
        if (!wallRunning) rb.useGravity = !OnSlope();
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

    public void Jump()
    {
        exitingSlope = true;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);

        // TO DO: double jump
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

    public bool OnSlope()
    {
        if (Physics.Raycast(groundCheck.position, Vector3.down, out slopeHit, 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }

        return false;
    }

    public Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }
}
