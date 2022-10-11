using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Advanced_Movement_System : MonoBehaviour
{
    #region VARIABLES
    [Header("Controls")]
    private InputManager controls;

    [Header("Components")]
    public Rigidbody rigidBody;
    public CapsuleCollider playerCollider;
    public Transform crouchCheck;
    public Transform playerCam;

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
    public float jumpCooldown;
    public float jumpForce;
    private bool readyToJump = true;
    private bool doubleJump;

    [Header("Aerial Movement")]
    public float airMultiplier;

    [Header("Slope Handling")]
    public LayerMask slopeMask;
    private float maxSlopeAngle;
    private RaycastHit slopeHit;
    private bool exitingSlope;
    private bool onSlope;
    private bool isSlopeSliding;
    public float slideYScale;

    [Header("Crouching")]
    public float crouchYScale;
    private bool crouching;

    [Header("Crouch Slide")]
    public float crouchSlideYScale;
    private bool isCrouchSlide;
    private bool wasCrouchSliding;
    public float slideForce;
    private float slideTimer;
    public float maxSlideTime;

    [Header("Standing")]
    private float startYScale;
    private bool canStand;
    private bool tryingToStand;

    [Header("Wall Running")]
    public LayerMask wallMask;
    private bool wallRunning;
    public float wallRunForce;
    public float wallJumpUpForce;
    public float wallJumpSideForce;
    public float maxWallRunTime;
    private float wallRunTimer;

    [Header("Wall Detection")]
    public float wallCheckDistance;
    public float minJumpHeight;
    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;
    private bool wallLeft;
    private bool wallRight;
    private Transform detectedWall;
    private Transform lastWallRun;

    [Header("Exit Wall Run")]
    public float exitWallTime;
    private bool exitingWall;
    private float exitWallTimer;

    [Header("Wall Run Gravity")]
    public bool useGravity;
    public float gravityCounterForce;

    [Header("Input Variables")]
    private Vector2 moveInput;

    #endregion

    #region INITIALISE
    private void Awake()
    {
        controls = InputManager.Instance;
    }

    private void Start()
    {
        startYScale = gameObject.transform.localScale.y;

        // jump when the jump control is pressed
        controls.playerControls.Player.Jump.performed += ctx => Jump();
        // Crouch when the crouch control is pressed
        controls.playerControls.Player.Crouch.started += ctx => StartCrouch();
        // when crouch is released, cancel the crouch
        controls.playerControls.Player.Crouch.canceled += ctx => CancelCrouch();
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
        else if (controls.playerControls.Player.Sprint.inProgress && isGrounded && !isCrouchSlide)
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

        // Check if there is a wall either side of the player that they can run along
        CheckForWall();

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

        // Manages the wall running state
        WallRunState();

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
        // when player is on a slope, use the slope movement
        if (OnSlope() && !exitingSlope)
        {
            SlopeMovement();
        }

        else if (isSlopeSliding)
        {
            // Function that moves the player when on a slope slide
            SlidingMovement();
        }

        else if (isCrouchSlide)
        {
            CrouchSlideMove();
        }

        else if (wallRunning)
        {
            WallRunningMove();
        }

        else
        {
            // Function that moves the player
            MovePlayer();
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
        moveInput = controls.GetPlayerMovement();
    }
    #endregion

    #region BASIC MOVEMENT

    private void MovePlayer()
    {
        // TODO: stop movement for forward when on slope

        // Calculate the direction in which to move
        //Vector3 facingDir = new Vector3(playerCam.transform.forward.x, 0, playerCam.transform.forward.z).normalized;
        Vector3 v3MoveDir = playerCam.forward * moveInput.y + playerCam.right * moveInput.x;
        v3MoveDir.y = 0;

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

        if (wallRunning)
        {
            WallJump();
            return;
        }

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
        if (crouching) return;
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
        Vector3 v3MoveDir = playerCam.forward * moveInput.y + playerCam.right * moveInput.x;

        if (OnSlope() || rigidBody.velocity.y > -0.1f)
        {
            rigidBody.AddForce(v3MoveDir.normalized * slideForce, ForceMode.Force);

            slideTimer -= Time.deltaTime;
        }
        else
        {
            rigidBody.AddForce(GetSlopeMoveDirection(v3MoveDir) * slideForce, ForceMode.Force);
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

        if (canStand && !controls.playerControls.Player.Crouch.inProgress)
        {
            IncreasePlayerScale();
        }
        else if (controls.playerControls.Player.Crouch.inProgress)
        {
            crouching = true;
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
        Vector3 v3MoveDir = playerCam.forward * moveInput.y + playerCam.right * moveInput.x;
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
        //Debug.DrawRay(groundCheck.position, Vector3.down * 0.4f, Color.red);
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
        Vector3 v3MoveDir = playerCam.forward * moveInput.y + playerCam.right * moveInput.x;
        // Applies the movement force to make the player move down the slope
        rigidBody.AddForce(GetSlopeMoveDirection(v3MoveDir) * 10f * moveSpeed, ForceMode.Force);
    }
    #endregion

    #region PLAYER SCALE
    private void ReducePlayerScale(float scale, float force)
    {
        // reduces the scale of the collider object
        //gameObject.transform.localScale = new Vector3(gameObject.transform.localScale.x, gameObject.transform.localScale.y * scale, gameObject.transform.localScale.z);
        playerCollider.height *= scale;
        playerCollider.center = new Vector3(0, -scale, 0);
        // Push the player toward the ground
        rigidBody.AddForce(Vector3.down * force, ForceMode.Impulse);

        // IDEALLY A CROUCH/SLIDE ANIMATION WOULD BE PLAYED HERE
    }

    private void IncreasePlayerScale()
    {
        // Increase the scale of the collider object back to original scale
        //gameObject.transform.localScale = new Vector3(gameObject.transform.localScale.x, startYScale, gameObject.transform.localScale.z);
        playerCollider.height = 2;
        playerCollider.center = new Vector3(0, 0, 0);
        // IDEALLY THERE WOULD BE AN ANIMATION THAT WOULD BE TRANSITIONED BACK TO STANDING HERE
    }
    #endregion

    #region GROUND
    private void GroundCheck()
    {
        // check if there is a ground object under the player
        isGrounded = Physics.CheckSphere(groundCheck.position, 0.4f, groundMask);

        if (isGrounded)
        {
            // Apply drag to the player
            rigidBody.drag = groundDrag;

            // reset double jump
            if (!controls.playerControls.Player.Jump.inProgress) doubleJump = false;

            // reset wall run
            lastWallRun = null;
        }
        else
        {
            // when in air there shouldnt be drag slowing down movement (not following real physics due to approximations and creating smooth player experience)
            rigidBody.drag = 0f;
        }
    }
    #endregion

    #region WALL RUNNING
    private void CheckForWall()
    {
        // check for a runnable wall to the right of the player
        wallRight = Physics.Raycast(transform.position, playerCam.right, out rightWallHit, wallCheckDistance, wallMask);
        // check for a runnable wall to the left of the player
        wallLeft = Physics.Raycast(transform.position, -playerCam.right, out leftWallHit, wallCheckDistance, wallMask);

        // if a wall is detected, assign it as the detected wall
        if (wallRight) detectedWall = rightWallHit.transform;
        if (wallLeft) detectedWall = leftWallHit.transform;
    }

    private void WallRunState()
    {
        // State 1 - wall running
        if ((wallLeft || wallRight) && moveInput.y > 0 && AboveGround() && !exitingWall)
        {
            if (!wallRunning)
            {
                StartWallRun();
            }

            if (wallRunTimer > 0)
            {
                wallRunTimer -= Time.deltaTime;
            }

            if (wallRunTimer <= 0 && wallRunning)
            {
                exitingWall = true;
                exitWallTimer = exitWallTime;
            }
        }
        // State 2 - exiting
        else if (exitingWall)
        {
            if (wallRunning)
            {
                StopWallRun();
            }

            if (exitWallTimer > 0)
            {
                exitWallTimer -= Time.deltaTime;
            }

            if (exitWallTimer <= 0)
            {
                exitingWall = false;
            }
        }

        // State 3 - none
        else
        {
            if (wallRunning)
            {
                StopWallRun();
            }
        }
    }

    private void StartWallRun()
    {
        // if the wall detected is the same as the last one, dont start wall running
        if (detectedWall == lastWallRun)
        {
            StopWallRun();
            return;
        }

        wallRunning = true;
        // reset wall run timer
        wallRunTimer = maxWallRunTime;
        // reset y velocity of the player
        rigidBody.velocity = new Vector3(rigidBody.velocity.x, 0f, rigidBody.velocity.z);

        // Apply camera effects
        //playerCam.DoFOV(90f);
        //if (wallLeft) playerCam.DoTilt(-5f);
        //if (wallRight) playerCam.DoTilt(5f);

        if (wallLeft) lastWallRun = leftWallHit.transform;
        if (wallRight) lastWallRun = rightWallHit.transform;
    }

    private void WallRunningMove()
    {
        // set whether gravity is being used or not
        rigidBody.useGravity = useGravity;

        // calculate the normal of the wall that was hit
        Vector3 wallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;

        // calculate the forward vector of the wall
        Vector3 wallForward = Vector3.Cross(wallNormal, transform.up);

        Vector3 dir = new Vector3(playerCam.forward.x, 0, playerCam.forward.z).normalized;

        // calculate which direction the player is moving in
        if ((dir - wallForward).magnitude > (dir - -wallForward).magnitude)
        {
            wallForward = -wallForward;
        }

        // apply the movement force along the wall
        rigidBody.AddForce(wallForward * wallRunForce, ForceMode.Force);

        // push to wall force
        if (!(wallLeft && moveInput.x > 0) && !(wallRight && moveInput.x < 0))
        {
            rigidBody.AddForce(-wallNormal * 100, ForceMode.Force);
        }

        // Weaken Gravity
        if (useGravity)
        {
            rigidBody.AddForce(transform.up * gravityCounterForce, ForceMode.Force);
        }
    }

    private void StopWallRun()
    {
        wallRunning = false;
        doubleJump = true;

        // reset camera fx
        //playerCam.DoFOV(70f);
        //playerCam.DoTilt(0f);
    }

    private void WallJump()
    {
        // enter exiting wall state
        exitingWall = true;
        exitWallTimer = exitWallTime;

        // calculate normal of the wall
        Vector3 wallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;
        // calculate the force to apply when jumping
        Vector3 forceToApply = transform.up * wallJumpUpForce + wallNormal * wallJumpSideForce;

        // reset vel and add force
        rigidBody.velocity = new Vector3(rigidBody.velocity.x, 0f, rigidBody.velocity.z);
        rigidBody.AddForce(forceToApply, ForceMode.Impulse);

        lastWallRun = null;
    }

    private bool AboveGround()
    {
        return !Physics.Raycast(transform.position, Vector3.down, minJumpHeight, groundMask);
    }
    #endregion
}
