using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CamOverride : CinemachineExtension
{
    [SerializeField] private PlayerControls controls;
    private Vector3 startingRotation;

    protected override void Awake()
    {
        //controls = new PlayerControls();

        if (startingRotation == null) startingRotation = transform.localRotation.eulerAngles;
        base.Awake();
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (vcam.Follow)
        {
            if (stage == CinemachineCore.Stage.Aim)
            {
                Vector2 deltaInput = controls.Player.Look.ReadValue<Vector2>();

                startingRotation.x += deltaInput.x * Time.deltaTime;
                startingRotation.y += deltaInput.y * Time.deltaTime;
            }
        }
    }
}
