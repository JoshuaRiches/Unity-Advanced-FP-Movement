using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gun_Shooting : MonoBehaviour
{
    [Header("GunStats")]
    public float fireRate;
    private float fireCooldown;
    public float gunRange;
    public float maxClip;
    private float currentClip;
    public bool isFullAuto;

    [Header("References")]
    public GameObject muzzleFlash;
    public GameObject reloadVent;
    public GameObject hitFX;
    public Transform firePoint;
    public LayerMask enemyLayer;
    public Camera cam;

    private RaycastHit targetHit;

    private void Start()
    {
        currentClip = maxClip;
    }

    private void Update()
    {
        if (isFullAuto)
        {
            if (Input.GetMouseButton(0) && fireCooldown <= 0)
            {
                Fire();
                fireCooldown = fireRate;
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0) && fireCooldown <= 0)
            {
                Fire();
                fireCooldown = fireRate;
            }
        }

        fireCooldown -= Time.deltaTime;
    }

    private void Fire()
    {
        GameObject flash = Instantiate(muzzleFlash, firePoint.position, Quaternion.LookRotation(firePoint.forward), firePoint);
        flash.transform.localScale *= 7;

        bool hasHit = Physics.Raycast(cam.transform.position, cam.transform.forward, out targetHit, gunRange);

        if (hasHit && targetHit.transform.tag != "Player")
        {
            Quaternion rot = Quaternion.LookRotation(targetHit.normal);
            Instantiate(hitFX, targetHit.point, rot);
        }
    }
}
