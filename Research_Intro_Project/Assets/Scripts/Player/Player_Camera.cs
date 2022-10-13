using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Cinemachine;

public class Player_Camera : MonoBehaviour
{
    public CinemachineVirtualCamera virtualCamera;
    public CinemachineRecomposer recomp;

    private float targetFOV;
    private float targetTilt;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        targetFOV = virtualCamera.m_Lens.FieldOfView;
    }
    private void Update()
    {
        if (virtualCamera.m_Lens.FieldOfView != targetFOV)
        {
            virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(virtualCamera.m_Lens.FieldOfView, targetFOV, 0.1f);
        }

        if (transform.localRotation.z != targetTilt)
        {
            recomp.m_Dutch = Mathf.Lerp(recomp.m_Dutch, targetTilt, 0.25f);
        }

    }

    public void DoFOV(float endValue)
    {
        targetFOV = endValue;
    }

    public void DoTilt(float zTilt)
    {
        targetTilt = zTilt;
    }
}
