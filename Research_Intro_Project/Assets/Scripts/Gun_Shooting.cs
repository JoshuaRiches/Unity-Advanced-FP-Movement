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
    public ParticleSystem muzzleFlash;
    public ParticleSystem reloadVent;
    public ParticleSystem hitFX;
    public Transform firePoint;
    public LayerMask enemyLayer;

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
        Instantiate(muzzleFlash, firePoint.position, Quaternion.identity);

        bool hasHit = Physics.Raycast(firePoint.position, Vector3.forward, out targetHit, gunRange, enemyLayer);

        if (hasHit)
        {
            Quaternion rot = Quaternion.LookRotation(targetHit.normal);
            Instantiate(hitFX, targetHit.point, rot);
        }
    }
}
