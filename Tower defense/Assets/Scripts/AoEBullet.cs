using UnityEngine;

public class AoEBullet : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private GameObject explosionPrefab;

    [Header("Attributes")]
    [SerializeField] private float bulletSpeed = 6f;
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private float explosionDamage = 30f;
    [SerializeField] private LayerMask enemyMask;

    private Transform target;
    private bool hasExploded;

    public void SetTarget(Transform _target) => target = _target;

    public void SetDamage(float dmg) => explosionDamage = dmg;

    public void SetRadius(float rad) => explosionRadius = rad;

    private void FixedUpdate()
    {
        if (hasExploded) return;

        if (target == null)
        {
            Explode();
            return;
        }

        Vector2 dir = (target.position - transform.position).normalized;
        rb.velocity = dir * bulletSpeed;

        if (Vector2.Distance(transform.position, target.position) <= 0.3f)
        {
            Explode();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasExploded) return;
        Explode();
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        if (explosionPrefab != null)
        {
            GameObject fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            ParticleSystem ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                Destroy(fx, ps.main.duration + ps.main.startLifetime.constantMax);
            }
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            explosionRadius,
            enemyMask
        );

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out Health health))
                health.TakeDamage(Mathf.RoundToInt(explosionDamage));
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}