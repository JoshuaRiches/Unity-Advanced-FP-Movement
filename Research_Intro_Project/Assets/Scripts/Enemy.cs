using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Stats")]
    public float maxHp;
    private float currentHp;
    public bool isDead;

    //[Header("Movement")]
    //public float moveSpeed;

    private void Start()
    {
        currentHp = maxHp;
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
        transform.Rotate(Vector3.up, 180f);
    }
}
