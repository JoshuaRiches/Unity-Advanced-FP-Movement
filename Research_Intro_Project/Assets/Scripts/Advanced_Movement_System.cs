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
    public Transform playerCollider;
    public Transform crouchCheck;

    [Header("Movement")]
    public float groundDrag;
    private bool isSprinting;

    [Header("Speed Control")]
    public float moveSpeed;
    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;
    public float speedIncreaseMultiplier;
    public float slopeIncreaseMultiplier;

    public float walkSpeed;
    public float sprintSpeed;
    public float slopeSpeed;
    public float wallRunSpeed;
    public float crouchSpeed;
    public float crouchSlideSpeed;

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

    [Header("Slope Handling")]
    private float maxSlopeAngle;
    private RaycastHit slopeHit;
    public LayerMask slopeMask;
    private bool exitingSlope;
    private bool onSlope;
    private bool isSlopeSliding;
    public float slideYScale;

    [Header("Crouching")]
    private bool crouching;
    public float crouchYScale;

    [Header("Crouch Slide")]
    private bool isCrouchSlide;
    private bool wasCrouchSliding;
    public float crouchSlideYScale;
    public float slideForce;
    private float slideTimer;
    public float maxSlideTime;

    [Header("Standing")]
    private float startYScale;
    private bool canStand;
    private bool tryingToStand;

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
        // get the starting scale of the players collider
        startYScale = playerCollider.localScale.y;

        // jump when the jump control is pressed
        controls.Player.Jump.performed += ctx => Jump();
        // Crouch when the crouch control is pressed
        controls.Player.Crouch.started += ctx => StartCrouch();
        // When the player holds crouch, keep them crouched
        controls.Player.Crouch.performed += ctx => Crouch();
        // when crouch is released, cancel the crouch
        controls.Player.Crouch.canceled += ctx => CancelCrouch();
    }

    private void OnEnable()
    {
        // enable the input system
        controls.Enable();
    }
    private void OnDisable()
    {
        // disable input system
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
        else if (isCrouchSlide)
        {
            state = MOVEMENT_STATE.SLIDING;
            desiredMoveSpeed = crouchSlideSpeed;
        }
        // Sprinting state
        else if (controls.Player.Sprint.inProgress && isGrounded && !isCrouchSlide)
        {
            state = MOVEMENT_STATE.SPRINT;
            desiredMoveSpeed = sprintSpeed;
            moveSpeed = desiredMoveSpeed;
        }
        // sliding on slope state
        else if (onSlope)
        {
            state = MOVEMENT_STATE.SLOPE_SLIDING;

            if (rigidBody.velocity.y < 0.1f)
            {
                desiredMoveSpeed = slopeSpeed;
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

        // if there is no movement input from the player, immediately set speed to remove the momentum from pevious action
        if (moveInput.x == 0 && moveInput.y == 0 && !onSlope)
        {
            StopAllCoroutines();
            moveSpeed = desiredMoveSpeed;
        }
        // if difference between desired speed and last desires speed is more than 5, slowly reduce the move speed to conserve some momentum
        else if (Mathf.Abs(desiredMoveSpeed - lastDesiredMoveSpeed) > 5f && moveSpeed != 0)
        {
            StopAllCoroutines();
            StartCoroutine(SmoothlyLerpMoveSpeed());
        }
        // if difference in speeds is less than 5, immediately reduce speed to the desired one
        else
        {
            moveSpeed = desiredMoveSpeed;
        }

        lastDesiredMoveSpeed = desiredMoveSpeed;
    }
    #endregion

    #region UPDATE/FIXED UPDATE
    private void Update()
    {
        // Function that checks if player is on ground
        GroundCheck();
        // function that checks for any inputs
        Inputs();
        // function that checks if player is on a slope
        CheckForSlope();

        // if the player steps onto a slope, start sliding
        if (onSlope && !isSlopeSliding)
        {
            StartSliding();
        }
        // when player leaves slope, stop sliding
        else if (isSlopeSliding)
        {
            StopSliding();
        }

        // if crouching or sliding, check to see if there is an obstacle that would stop them from standing up
        if (crouching || isCrouchSlide)
        {
            // Check if there is an obstacle above the player stopping them from standing up
            CheckCanStand();
        }
        // Limit speed of movement so the player doesnt accelerate to a high speed
        SpeedControl();

        // Manages what state the player is in
        StateHandler();

        //if the player is trying to stand but not crouching, set it so they are crouching (otherwise it will bug)
        if (tryingToStand && !crouching) crouching = true;

        if (crouching || isCrouchSlide)
        {
            if (tryingToStand && canStand)
            {
                Stand();
            }
        }
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

        if (isSlopeSliding)
        {
            // Function that moves the player when on a slope slide
            SlidingMovement();
        }

        if (isCrouchSlide)
        {
            CrouchSlideMove();
        }

        // if the player isnt wallrunning or on a slope then gravity should be on
        if (!wallRunning) rigidBody.useGravity = !OnSlope();

        // if the player is wallrunning, double jump should be false
        if (wallRunning) doubleJump = false;

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

    #region SPEED CONTROL
    private void SpeedControl()
    {
        // limiting speed on slope
        if (OnSlope() && !exitingSlope)
        {
            // if player is moving faster than the move speed, reduce the speed to be the movespeed
            if (rigidBody.velocity.magnitude > moveSpeed)
            {
                rigidBody.velocity = rigidBody.velocity.normalized * moveSpeed;
            }
        }
        // limit speed on ground
        else
        {
            // get the velocity of the x and z axis
            Vector3 flatVel = new Vector3(rigidBody.velocity.x, 0f, rigidBody.velocity.z);

            // if that velocity is faster than the move speed, reduce it
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rigidBody.velocity = new Vector3(limitedVel.x, rigidBody.velocity.y, limitedVel.z);
            }
        }
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

    #region CROUCHING
    private void CheckCanStand()
    {
        canStand = !Physics.CheckSphere(crouchCheck.position, 1f, groundMask);
    }

    private void StartCrouch()
    {
        // if the player isnt sprinting, make player crouch
        if (state != MOVEMENT_STATE.SPRINT)
        {
            ReducePlayerScale(crouchYScale, 5f);
            crouching = true;
        }
        // if the player is sprinting when they try to crouch, they will slide instead
        else if (state == MOVEMENT_STATE.SPRINT)
        {
            StartCrouchSlide();
        }
    }

    private void Crouch()
    {
        // if player was sliding and isnt crouching, set them to be crouching
        if (wasCrouchSliding && !crouching)
        {
            crouching = true;
            ReducePlayerScale(crouchYScale, 5);
        }
    }

    private void CancelCrouch()
    {
        if (isCrouchSlide)
        {
            EndSlide();
            return;
        }

        wasCrouchSliding = false;

        // if the player can stand up, make them
        if (canStand)
        {
            Stand();
        }
        // if player cant stand right now, set it so that tehy are trying to so whenever they can once again stand, they will
        else if (!canStand)
        {
            tryingToStand = true;
        }
    }

    private void Stand()
    {
        // return the collider scale back to original
        IncreasePlayerScale();
        crouching = false;
        tryingToStand = false;
    }
    #endregion

    #region CROUCH SLIDING
    private void StartCrouchSlide()
    {
        if (moveInput != Vector2.zero)
        {
            isCrouchSlide = true;

            ReducePlayerScale(crouchSlideYScale, 5f);

            slideTimer = maxSlideTime;
        }
    }

    private void CrouchSlideMove()
    {
        Vector3 inputDirection = orientation.forward * moveInput.y + orientation.right * moveInput.x;

        if (OnSlope() || rigidBody.velocity.y > -0.1f)
        {
            rigidBody.AddForce(inputDirection.normalized * slideForce, ForceMode.Force);

            slideTimer -= Time.deltaTime;
        }
        else
        {
            rigidBody.AddForce(GetSlopeMoveDirection(inputDirection) * slideForce, ForceMode.Force);
        }

        if (slideTimer <= 0f)
        {
            EndSlide();
        }
    }

    private void EndSlide()
    {
        isCrouchSlide = false;
        wasCrouchSliding = false;

        CheckCanStand();

        if (canStand)
        {
            IncreasePlayerScale();
        }
        else
        {
            tryingToStand = true;
        }
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
            // Calculated the angle of the slope
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }

        return false;
    }

    public Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        // returns the angled slope direction
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }
    #endregion

    #region SLOPE SLIDING
    private void CheckForSlope()
    {
        // checks if the player is on a slope object
        onSlope = Physics.CheckSphere(groundCheck.position, 0.4f, slopeMask);
    }

    private void StartSliding()
    {
        isSlopeSliding = true;
        // make player collider smaller
        ReducePlayerScale(slideYScale, 20f);
    }

    private void StopSliding()
    {
        isSlopeSliding = false;
        // return player collider scale back to normal
        IncreasePlayerScale();
    }

    private void SlidingMovement()
    {
        // calculates direction of movement (always moving forward even without an input)
        Vector3 inputDirection = orientation.forward * 1f + orientation.right * moveInput.x;
        // Applies the movement force to make the player move down the slope
        rigidBody.AddForce(GetSlopeMoveDirection(inputDirection) * 10f * moveSpeed, ForceMode.Force);
    }
    #endregion

    #region PLAYER SCALE
    private void ReducePlayerScale(float scale, float force)
    {
        // reduces the scale of the collider object
        playerCollider.localScale = new Vector3(playerCollider.localScale.x, scale, playerCollider.localScale.z);
        // Push the player toward the ground
        rigidBody.AddForce(Vector3.down * force, ForceMode.Impulse);

        // IDEALLY A CROUCH/SLIDE ANIMATION WOULD BE PLAYED HERE
    }

    private void IncreasePlayerScale()
    {
        // Increase the scale of the collider object back to original scale
        playerCollider.localScale = new Vector3(playerCollider.localScale.x, startYScale, playerCollider.localScale.z);

        // IDEALLY THERE WOULD BE AN ANIMATION THAT WOULD BE TRANSITIONED BACK TO STANDING HERE
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

            if (!controls.Player.Jump.inProgress) doubleJump = false;
        }
        else
        {
            // when in air there shouldnt be drag slowing down movement (not following real physics due to approximations and creating smooth player experience)
            rigidBody.drag = 0f;
        }
    }
    #endregion
}
