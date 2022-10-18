using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class Advanced_Movement_System : MonoBehaviour
{
    #region VARIABLES
    [Header("Controls")]
    private PlayerControls controls;

    [Header("Components")]
    public Rigidbody rigidBody;
    public CapsuleCollider playerCollider;
    public Transform crouchCheck;
    public Transform playerCam;
    public GameObject camSettings;
    public Transform camHolder;
    public Player_Camera camScript;
    public Transform model;
    public Transform stepRayUpper;
    public Transform stepRayLower;

    [Header("Scales")]
    public float startScale;
    public float camStartYPos;
    public float camCrouchYPos;
    public float crouchYScale;

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
    public bool isGrounded;
    private int stepsSinceLastGrounded;
    public float groundCheckDistance = 0.01f;
    public float stickToGroundDist = 0.5f;
    public float shellOffset = 0.1f;
    private bool previouslyGrounded;
    private Vector3 groundContactNormal;

    [Header("Jumping")]
    public float jumpCooldown;
    public float jumpForce;
    private bool readyToJump = true;
    private bool doubleJump;
    private bool jumping;

    [Header("Aerial Movement")]
    public float airMultiplier;

    [Header("Slope Handling")]
    public LayerMask slopeMask;
    public float maxSlopeAngle;
    private RaycastHit slopeHit;
    private bool exitingSlope;
    private bool enteredSlope;
    private bool onSlope;
    private bool isSlopeSliding;
    private RaycastHit slideHit;

    [Header("Step Management")]
    public float stepheight;
    public float stepSmooth;
    private bool climbingStairs;
    public LayerMask stairMask;

    [Header("Crouching")]
    private bool crouching;

    [Header("Crouch Slide")]
    public float slideForce;
    private bool isCrouchSlide;
    private float slideTimer;
    public float maxSlideTime;

    [Header("Standing")]
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
        controls = new PlayerControls();
        startScale = playerCollider.height;

        stepRayUpper.localPosition = new Vector3(stepRayUpper.localPosition.x, -(1 - stepheight), stepRayUpper.localPosition.z);
    }

    private void Start()
    {
        // jump when the jump control is pressed
        controls.Player.Jump.performed += ctx => Jump();
        // Crouch when the crouch control is pressed
        controls.Player.Crouch.started += ctx => StartCrouch();
        // when crouch is released, cancel the crouch
        controls.Player.Crouch.canceled += ctx => CancelCrouch();
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
        if (!isSlopeSliding)
        {
            transform.rotation = Quaternion.AngleAxis(playerCam.eulerAngles.y, Vector3.up);
        }

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
        else if (!onSlope && isSlopeSliding)
        {
            StopSliding();
        }

        // function that handles fov of the camera
        FOVControl();

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

        // checks to see if you are at steps 
        StepClimb();

        //if the player is trying to stand but not crouching, set it so they are crouching (otherwise it will bug)
        if (tryingToStand && !crouching) crouching = true;

        if (crouching || isCrouchSlide)
        {
            if (tryingToStand && canStand)
            {
                Stand();
            }
        }

        if (!OnSlope())
        {
            enteredSlope = false;
        }
    }

    private void FixedUpdate()
    {
        // when player is on a slope, use the slope movement
        if (OnSlope() && !exitingSlope && !isSlopeSliding)
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

        if (previouslyGrounded && !jumping && !wallRunning && !climbingStairs)
        {
            StickToGround();
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
        // Calculate the direction in which to move
        Vector3 v3MoveDir = transform.forward * moveInput.y + transform.right * moveInput.x;

        v3MoveDir = Vector3.ProjectOnPlane(v3MoveDir, groundContactNormal).normalized;

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

    #region FOV CONTROL
    private void FOVControl()
    {
        if (wallRunning)
        {
            camScript.DoFOV(90f);
        }
        else if (state == MOVEMENT_STATE.SPRINT && moveInput.magnitude > 0)
        {
            camScript.DoFOV(90f);
        }
        else if (state == MOVEMENT_STATE.SLIDING || state == MOVEMENT_STATE.SLOPE_SLIDING)
        {
            camScript.DoFOV(100f);
        }
        else
        {
            camScript.DoFOV(70f);
        }
        
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
        if ((isGrounded || isSlopeSliding) || doubleJump && !wallRunning)
        {
            jumping = true;

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
        canStand = !Physics.CheckSphere(crouchCheck.position, 0.5f, groundMask);
    }

    private void StartCrouch()
    {
        if (!isGrounded) return;

        if (crouching) return;

        // if the player isnt sprinting, make player crouch
        if (state != MOVEMENT_STATE.SPRINT)
        {
            ReducePlayerScale(crouchYScale, 5f, camCrouchYPos);
            crouching = true;
        }
        // if the player is sprinting when they try to crouch, they will slide instead
        else if (state == MOVEMENT_STATE.SPRINT)
        {
            StartCrouchSlide();
            return;
        }
    }
    private void CancelCrouch()
    {
        if (isCrouchSlide && slideTimer <= 0f)
        {
            EndSlide();
            return;
        }
        else if (isCrouchSlide && slideTimer > 0) return;

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
        if (!isGrounded) return;

        if (moveInput != Vector2.zero)
        {
            isCrouchSlide = true;

            ReducePlayerScale(crouchYScale, 5f, camCrouchYPos);

            slideTimer = maxSlideTime;
        }
    }

    private void CrouchSlideMove()
    {
        Vector3 v3MoveDir = transform.forward * moveInput.y + transform.right * moveInput.x;
        v3MoveDir = Vector3.ProjectOnPlane(v3MoveDir, groundContactNormal).normalized;

        if (!OnSlope() || rigidBody.velocity.y > -0.1f)
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
        if (slideTimer > 0f) return;

        isCrouchSlide = false;

        CheckCanStand();

        if (canStand && !controls.Player.Crouch.IsPressed())
        {
            IncreasePlayerScale();
        }
        else if (controls.Player.Crouch.IsPressed())
        {
            crouching = true;
            ReducePlayerScale(crouchYScale, 5f, camCrouchYPos);
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
        Vector3 v3MoveDir = transform.forward * moveInput.y + transform.right * moveInput.x;

        // add the slope movement force
        rigidBody.AddForce(GetSlopeMoveDirection(v3MoveDir) * moveSpeed * 20f, ForceMode.Force);

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
        onSlope = Physics.Raycast(groundCheck.position, Vector3.down, out slideHit, 0.5f, slopeMask);
    }

    private void StartSliding()
    {
        readyToJump = true;

        isSlopeSliding = true;

        Vector3 faceDir = new Vector3(slideHit.normal.x, 0, slideHit.normal.z).normalized;
        transform.forward = faceDir;

        // make player collider smaller
        ReducePlayerScale(crouchYScale, 20f, camCrouchYPos);

        // Push the player toward the ground
        //rigidBody.AddForce(Vector3.down * 20f, ForceMode.Impulse);

        // lock camera
        camSettings.GetComponent<CinemachineInputProvider>().enabled = false;
        playerCam.forward = faceDir;
    }

    private void StopSliding()
    {
        isSlopeSliding = false;
        // return player collider scale back to normal
        IncreasePlayerScale();

        camSettings.GetComponent<CinemachineInputProvider>().enabled = true;
    }

    private void SlidingMovement()
    {
        // calculates direction of movement (always moving forward even without an input)
        Vector3 v3MoveDir = transform.forward + transform.right * moveInput.x;

        // Applies the movement force to make the player move down the slope
        rigidBody.AddForce(GetSlopeMoveDirection(v3MoveDir) * 10f * moveSpeed, ForceMode.Force);
        rigidBody.AddForce(-slideHit.normal * 5f, ForceMode.Impulse);
    }
    #endregion

    #region PLAYER SCALE
    private void ReducePlayerScale(float scale, float force, float camPos)
    {
        // reduces the scale of the collider object
        
        if (scale < 0.5f)
        {
            playerCollider.radius = scale;
        }

        playerCollider.height =  startScale * scale;
        playerCollider.center = new Vector3(0, 0 - (1- scale), 0);

        model.localScale = new Vector3(model.localScale.x, scale, model.localScale.z);
        model.localPosition = new Vector3(model.localPosition.x, -scale, model.localPosition.z);

        // Push the player toward the ground - do not remove otherwise stuff breaks
        rigidBody.AddForce(Vector3.down * force, ForceMode.Impulse);

        camHolder.localPosition = new Vector3(camHolder.localPosition.x, camPos, camHolder.localPosition.z);

    }

    private void IncreasePlayerScale()
    {
        // Increase the scale of the collider object back to original scale
        playerCollider.radius = 0.5f;
        playerCollider.height = startScale;
        playerCollider.center = new Vector3(0, 0, 0);

        model.localScale = new Vector3(model.localScale.x, 1, model.localScale.z);
        model.localPosition = new Vector3(model.localPosition.x, 0, model.localPosition.z);

        camHolder.localPosition = new Vector3(camHolder.localPosition.x, camStartYPos, camHolder.localPosition.z);
    }
    #endregion

    #region GROUND
    private void GroundCheck()
    {
        previouslyGrounded = isGrounded;

        float maxDist = ((playerCollider.height / 2f) - playerCollider.radius) + groundCheckDistance;
        float rad = playerCollider.radius * (1 - shellOffset);

        RaycastHit hitInfo;

        if (Physics.SphereCast(transform.position, rad, Vector3.down, out hitInfo, maxDist, groundMask, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;
            groundContactNormal = hitInfo.normal;

            // Apply drag to the player
            rigidBody.drag = groundDrag;

            // reset double jump
            if (!controls.Player.Jump.inProgress) doubleJump = false;

            // reset wall run
            lastWallRun = null;

            if (!previouslyGrounded && jumping)
            {
                jumping = false;
            }
        }
        else
        {
            isGrounded = false;
            groundContactNormal = Vector3.up;

            // when in air there shouldnt be drag slowing down movement (not following real physics due to approximations and creating smooth player experience)
            rigidBody.drag = 0f;
        }
    }

    private void StickToGround()
    {
        float maxDist = ((playerCollider.height / 2f) - playerCollider.radius) + stickToGroundDist;
        float rad = playerCollider.radius * (1 - shellOffset);
        RaycastHit hitInfo;

        if (Physics.SphereCast(transform.position, rad, Vector3.down, out hitInfo, maxDist, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (Mathf.Abs(Vector3.Angle(hitInfo.normal, Vector3.up)) < 85f)
            {
                rigidBody.velocity = Vector3.ProjectOnPlane(rigidBody.velocity, hitInfo.normal);
            }
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
        //camScript.DoFOV(90f);
        if (wallLeft) camScript.DoTilt(-5f);
        if (wallRight) camScript.DoTilt(5f);

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

        // calculate which direction the player is moving in
        if ((transform.forward - wallForward).magnitude > (transform.forward - -wallForward).magnitude)
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
        //camScript.DoFOV(70f);
        camScript.DoTilt(0f);
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

    #region STEPS/STAIRS
    void StepClimb()
    {
        if (!isGrounded || moveInput.magnitude == 0) return;

        RaycastHit hitLower;
        if (Physics.Raycast(stepRayLower.position, transform.forward, out hitLower, 0.2f, stairMask))
        {
            RaycastHit hitUpper;
            if (!Physics.Raycast(stepRayUpper.position, transform.forward, out hitUpper, 0.2f, stairMask))
            {
                climbingStairs = true;
                rigidBody.position -= new Vector3(0f, -stepSmooth, 0f);
            }
            else
            {
                climbingStairs = false;
            }
        }

        RaycastHit hitLower45;
        if (Physics.Raycast(stepRayLower.position, transform.TransformDirection(1.5f, 0, -1f), out hitLower45, 0.2f, stairMask))
        {
            RaycastHit hitUpper45;
            if (!Physics.Raycast(stepRayUpper.position, transform.TransformDirection(1.5f, 0, -1f), out hitUpper45, 0.2f, stairMask))
            {
                climbingStairs = true;
                rigidBody.position -= new Vector3(0f, -stepSmooth, 0f);
            }
            else
            {
                climbingStairs = false;
            }
        }
        else climbingStairs = false;

        RaycastHit hitLowerM45;
        if (Physics.Raycast(stepRayLower.position, transform.TransformDirection(-1.5f, 0, -1f), out hitLowerM45, 0.2f, stairMask))
        {
            RaycastHit hitUpperM45;
            if (!Physics.Raycast(stepRayUpper.position, transform.TransformDirection(-1.5f, 0, -1f), out hitUpperM45, 0.2f, stairMask))
            {
                climbingStairs = true;
                rigidBody.position -= new Vector3(0f, -stepSmooth, 0f);
            }
            else
            {
                climbingStairs = false;
            }
        }
        else climbingStairs = false;
    }
    #endregion
}
