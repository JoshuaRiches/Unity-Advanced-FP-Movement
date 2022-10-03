using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class Player_Camera : MonoBehaviour
{
    #region VARIABLES
    [Header("Controls")]
    private PlayerControls controls;
    public float sensitivityX;
    public float sensitivityY;

    [Header("Components")]
    public Transform orientation;
    public Transform camHolder;

    private float xRotation;
    private float yRotation;
    #endregion

    #region INITIALISE
    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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

    #region UPDATE
    private void Update()
    {
        float mouseX = controls.Player.Look.ReadValue<Vector2>().x * Time.deltaTime * sensitivityX;
        float mouseY = controls.Player.Look.ReadValue<Vector2>().y * Time.deltaTime * sensitivityY;

        yRotation += mouseX;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        camHolder.rotation = Quaternion.Euler(xRotation, yRotation, 0f);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);

    }
    #endregion

    public void DoFOV(float endValue)
    {
        GetComponent<Camera>().DOFieldOfView(endValue, 0.25f);
    }

    public void DoTilt(float zTilt)
    {
        transform.DOLocalRotate(new Vector3(0, 0, zTilt), 0.25f);
    }
}
