using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Stats")]
    public float maxHp;
    private float currentHp;
    public bool isDead;

    [Header("Movement")]
    public float moveSpeed;
    public Vector3 startPos;
    public Vector3 endPos;
    private Vector3 targetPos;
    public bool isMoving;
    private Quaternion rot;

    private void Start()
    {
        currentHp = maxHp;
        targetPos = endPos;

        rot = new Quaternion(0, 180f, 0, 0);
    }

    private void Update()
    {
        if (isMoving)
        {
            if (Vector3.Magnitude(transform.position - targetPos) > 0.5f)
            {
                transform.position = Vector3.Lerp(transform.position, targetPos, moveSpeed * Time.deltaTime);
            }

            if (Vector3.Magnitude(transform.position - targetPos) <= 0.5f)
            {
                if (targetPos == startPos) targetPos = endPos;
                else targetPos = startPos;
            }
        }

        if (isDead)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, rot, 0.01f * Time.deltaTime);
        }
    }

    public void Damage(float damage)
    {
        if (isDead) return;

        currentHp -= damage;

        if (currentHp <= 0)
        {
            Defeated();
        }
    }

    private void Defeated()
    {
        isDead = true;
    }
}
