using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Slide : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform player;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Player_Movement moveScript;

    [Header("Sliding")]
    public float maxSlideTime;
    public float slideForce;
    private float slideTimer;

    public float slideYScale;
    private float startYScale;

    private float horizontalInput;
    private float forwardInput;

    private bool isSliding;

    // Start is called before the first frame update
    void Start()
    {
        startYScale = player.transform.localScale.y;
    }

    private void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        forwardInput = Input.GetAxisRaw("Vertical");
        
        if (Input.GetKeyDown(KeyCode.C) && (horizontalInput != 0 || forwardInput != 0) && moveScript.state == Player_Movement.MOVEMENT_STATE.SPRINT)
        {
            StartSlide();
        }


        if (Input.GetKeyUp(KeyCode.C) && isSliding)
        {
            StopSlide();
        }
    }

    private void FixedUpdate()
    {
        if (isSliding)
        {
            SlidingMovement();
        }
    }

    private void StartSlide()
    {
        isSliding = true;

        player.localScale = new Vector3(player.localScale.x, slideYScale, player.localScale.z);
        rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);

        slideTimer = maxSlideTime;
    }

    private void SlidingMovement()
    {
        Vector3 inputDirection = orientation.forward * forwardInput + orientation.right * horizontalInput;

        // sliding normal
        if (!moveScript.OnSlope() || rb.velocity.y > -0.1f)
        {
            rb.AddForce(inputDirection.normalized * slideForce, ForceMode.Force);

            slideTimer -= Time.deltaTime;
        }
        // sliding down a slope
        else
        {
            rb.AddForce(moveScript.GetSlopeMoveDirection(inputDirection) * slideForce, ForceMode.Force);
        }

        if (slideTimer <= 0)
        {
            StopSlide();
        }
    }

    private void StopSlide()
    {
        isSliding = false;

        moveScript.SetCrouching(true);
        moveScript.SetTryingToStand(true);

        //if (!moveScript.GetCanStand())
        //{
        //    return;
        //}

        //player.localScale = new Vector3(player.localScale.x, startYScale, player.localScale.z);
    }
}
