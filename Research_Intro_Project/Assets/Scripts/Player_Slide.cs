using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Slide : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
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

    // Start is called before the first frame update
    void Start()
    {
        startYScale = transform.transform.localScale.y;
    }

    private void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        forwardInput = Input.GetAxisRaw("Vertical");
        
        if (Input.GetKeyDown(KeyCode.C) && (horizontalInput != 0 || forwardInput != 0) && moveScript.state == Player_Movement.MOVEMENT_STATE.SPRINT)
        {
            StartSlide();
        }


        if (Input.GetKeyUp(KeyCode.C) && moveScript.isSliding)
        {
            StopSlide();
        }
    }

    private void FixedUpdate()
    {
        if (moveScript.isSliding)
        {
            SlidingMovement();
        }
    }

    private void StartSlide()
    {
        transform.localScale = new Vector3(transform.localScale.x, slideYScale, transform.localScale.z);
        rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);

        slideTimer = maxSlideTime;

        moveScript.isSliding = true;
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
        moveScript.EndSlide();
    }
}
