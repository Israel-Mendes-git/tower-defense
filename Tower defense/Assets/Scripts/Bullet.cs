using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Attributes")]
    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private float lifetime = 2f;       // limite de tempo de vida
    [SerializeField] private int pierce = 1;            // quantos inimigos atravessa antes de sumir

    private int bulletDamage = 1;
    private int remainingPierce;
    private bool canSeeCamo;
    private bool isSharp;
    private bool ignoresArmor;
    private Transform target;
    private Vector2 fixedDirection;
    private bool hasFixedDirection;

    private void Start()
    {
        remainingPierce = pierce;
        Destroy(gameObject, lifetime);
    }

    public void SetTarget(Transform _target)
    {
        target = _target;
    }

    // Projétil não-teleguiado: segue reto na direção dada (dardos laterais de um leque).
    public void SetDirection(Vector2 direction)
    {
        target = null;
        fixedDirection = direction.normalized;
        hasFixedDirection = true;
        if (rb != null) rb.velocity = fixedDirection * bulletSpeed;
    }

    public void SetDamage(int damage)
    {
        bulletDamage = damage;
    }

    public void SetDamage(float damage)
    {
        bulletDamage = Mathf.RoundToInt(damage);
    }

    public void SetCanSeeCamo(bool value)
    {
        canSeeCamo = value;
    }

    public void SetSharp(bool value)
    {
        isSharp = value;
    }

    // Munição perfurante: a armadura do alvo não reduz o dano.
    public void SetIgnoreArmor(bool value)
    {
        ignoresArmor = value;
    }

    // Perfuração extra concedida por upgrades (quantos inimigos o projétil atravessa).
    public void SetPierce(int value)
    {
        pierce = Mathf.Max(1, value);
        remainingPierce = pierce;
    }

    private void FixedUpdate()
    {
        if (hasFixedDirection)
        {
            rb.velocity = fixedDirection * bulletSpeed;
            return;
        }

        // Continua voando SEMPRE (não para em nada)
        if (target == null)
        {
            // Continua reto se perdeu alvo
            rb.velocity = rb.velocity.normalized * bulletSpeed;
            return;
        }

        Vector2 direction = (target.position - transform.position).normalized;
        rb.velocity = direction * bulletSpeed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Health health = other.GetComponent<Health>();
        if (health == null) return;

        health.TakeDamage(bulletDamage, canSeeCamo, isSharp, ignoresArmor);

        // Consome uma perfuração; some ao esgotar
        remainingPierce--;
        if (remainingPierce <= 0)
        {
            Destroy(gameObject);
        }
    }
}