using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Attributes")]
    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private float lifetime = 2f; // ÚNICO limite: tempo de vida

    private int bulletDamage = 1;
    private Transform target;

    private void Start()
    {
        // ÚNICA forma de destruir: tempo de vida
        Destroy(gameObject, lifetime);
    }

    public void SetTarget(Transform _target)
    {
        target = _target;
    }

    public void SetDamage(int damage)
    {
        bulletDamage = damage;
    }

    public void SetDamage(float damage)
    {
        bulletDamage = Mathf.RoundToInt(damage);
    }

    private void FixedUpdate()
    {
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
        // Atira em TODOS os inimigos que tocar (sem limite!)
        Health health = other.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(bulletDamage);
            // NÃO para, NÃO destrói, continua voando!
        }
    }
}