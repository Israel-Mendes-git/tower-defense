using UnityEngine;

public class TachinhaBullet : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Attributes")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private LayerMask enemyMask;

    private bool canSeeCamo;

    public void Init(Vector2 direction, float dmg, bool seesCamo = false)
    {
        damage = dmg;
        canSeeCamo = seesCamo;
        rb.velocity = direction.normalized * speed;
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & enemyMask) != 0)
        {
            if (collision.gameObject.TryGetComponent(out Health health))
                health.TakeDamage(Mathf.RoundToInt(damage), canSeeCamo, true); // tachinha é cortante (não fura chumbo)
        }

        Destroy(gameObject);
    }
}
