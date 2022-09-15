using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Slope_Slide : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform player;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Player_Movement moveScript;

    [Header("Sliding")]
    public float slideForce;
    public float slideYScale;
    private float startYScale;

    private float horizontalInput;

    private bool isSliding;

    // Start is called before the first frame update
    void Start()
    {
        startYScale = player.transform.localScale.y;
    }

    private void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
    }

    private void FixedUpdate()
    {
        if (isSliding)
        {
            SlidingMovement();
        }
    }

    public void StartSlide()
    {
        if (isSliding) return;

        isSliding = true;

        player.localScale = new Vector3(player.localScale.x, slideYScale, player.localScale.z);
        rb.AddForce(Vector3.down * 20f, ForceMode.Impulse);
    }

    private void SlidingMovement()
    {
        Vector3 inputDirection = orientation.forward * 1f + orientation.right * horizontalInput;

        rb.AddForce(moveScript.GetSlopeMoveDirection(inputDirection) * 10f * moveScript.moveSpeed, ForceMode.Force);
    }

    public void StopSlide()
    {
        if (!isSliding) return;

        isSliding = false;
        player.localScale = new Vector3(player.localScale.x, startYScale, player.localScale.z);
    }
}
