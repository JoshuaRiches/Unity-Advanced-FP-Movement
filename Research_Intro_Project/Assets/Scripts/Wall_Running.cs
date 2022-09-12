using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wall_Running : MonoBehaviour
{
    [Header("Wall Running")]
    public LayerMask wallMask;
    public LayerMask groundMask;
    public float wallRunForce;
    public float wallJumpUpForce;
    public float wallJumpSideForce;
    public float maxWallRunTime;
    private float wallRunTimer;

    [Header("Detection")]
    public float wallCheckDistance;
    public float minJumpHeight;
    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;
    private bool wallLeft;
    private bool wallRight;

    [Header("Exiting")]
    private bool exitingWall;
    public float exitWallTime;
    private float ExitWallTimer;

    [Header("Gravity")]
    public bool useGravity;
    public float gravityCounterForce;

    [Header("Inputs")]
    private float horizontalInput;
    private float forwardInput;

    [Header("References")]
    public Transform orientation;
    [SerializeField] private Player_Movement playerMoveScript;
    [SerializeField] private Rigidbody rb;

    private void Update()
    {
        CheckForWall();
        StateMachine();
    }

    private void FixedUpdate()
    {
        if (playerMoveScript.wallRunning)
        {
            WallRunningMovement();
        }
    }

    private void CheckForWall()
    {
        wallRight = Physics.Raycast(transform.position, orientation.right, out rightWallHit, wallCheckDistance, wallMask);
        wallLeft = Physics.Raycast(transform.position, -orientation.right, out leftWallHit, wallCheckDistance, wallMask);
    }

    private bool AboveGround()
    {
        return !Physics.Raycast(transform.position, Vector3.down, minJumpHeight, groundMask);
    }

    private void StateMachine()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        forwardInput = Input.GetAxisRaw("Vertical");

        // State 1 - wall running
        if ((wallLeft || wallRight) && forwardInput > 0 && AboveGround() && !exitingWall)
        {
            if (!playerMoveScript.wallRunning)
            {
                StartWallRun();
            }

            if (wallRunTimer > 0)
            {
                wallRunTimer -= Time.deltaTime;
            }

            if (wallRunTimer <= 0 && playerMoveScript.wallRunning)
            {
                exitingWall = true;
                ExitWallTimer = exitWallTime;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                WallJump();
            }
        }
        // State 2 - exiting
        else if (exitingWall)
        {
            if (playerMoveScript.wallRunning)
            {
                StopWallRun();
            }

            if (ExitWallTimer > 0)
            {
                ExitWallTimer -= Time.deltaTime;
            }

            if (ExitWallTimer <= 0)
            {
                exitingWall = false;
            }
        }

        // State 3 - none
        else
        {
            if (playerMoveScript.wallRunning)
            {
                StopWallRun();
            }
        }
    }

    private void StartWallRun()
    {
        playerMoveScript.wallRunning = true;

        wallRunTimer = maxWallRunTime;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
    }

    private void WallRunningMovement()
    {
        rb.useGravity = useGravity;


        Vector3 wallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;

        Vector3 wallForward = Vector3.Cross(wallNormal, transform.up);

        if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude)
        {
            wallForward = -wallForward;
        }

        rb.AddForce(wallForward * wallRunForce, ForceMode.Force);

        // push to wall force
        if (!(wallLeft && horizontalInput > 0) && !(wallRight && horizontalInput < 0))
        {
            rb.AddForce(-wallNormal * 100, ForceMode.Force);
        }

        // Weaken Gravity
        if (useGravity)
        {
            rb.AddForce(transform.up * gravityCounterForce, ForceMode.Force);
        }
    }

    private void StopWallRun()
    {
        playerMoveScript.wallRunning = false;
    }

    private void WallJump()
    {
        // enter exiting wall state
        exitingWall = true;
        ExitWallTimer = exitWallTime;

        Vector3 wallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;

        Vector3 forceToApply = transform.up * wallJumpUpForce + wallNormal * wallJumpSideForce;

        // reset vel and add force
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(forceToApply, ForceMode.Impulse);
    }
}
