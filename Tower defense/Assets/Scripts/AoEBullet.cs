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
    private bool canSeeCamo;

    // Efeitos destravados por upgrade da torre.
    private int clusterCount;      // explosões secundárias disparadas ao redor
    private float fireDamage;      // se > 0, deixa uma poça de fogo no ponto do impacto
    private float fireRadius;
    private float fireDuration;

    public void SetTarget(Transform _target) => target = _target;

    public void SetDamage(float dmg) => explosionDamage = dmg;

    public void SetRadius(float rad) => explosionRadius = rad;

    public void SetCanSeeCamo(bool value) => canSeeCamo = value;

    // Bomba cacho: ao explodir, dispara N explosões menores em volta.
    public void SetCluster(int count) => clusterCount = count;

    // Napalm: deixa fogo queimando no chão após a explosão.
    public void SetFire(float damagePerTick, float radius, float duration)
    {
        fireDamage = damagePerTick;
        fireRadius = radius;
        fireDuration = duration;
    }

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

        AudioManager.Cue(AudioManager.Sfx.Explosion, 0.6f);

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

        // explosionRadius é em CÉLULAS (ver IsoGrid): pré-filtro pelo raio de mundo que cobre a
        // elipse, corte fino por célula pra explosão valer o mesmo raio redondo em qualquer direção.
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            IsoGrid.WorldRadiusFor(explosionRadius),
            enemyMask
        );

        foreach (var hit in hits)
        {
            if (IsoGrid.CellDistance(transform.position, hit.transform.position) > explosionRadius) continue;
            if (hit.TryGetComponent(out Health health))
                health.TakeDamage(Mathf.RoundToInt(explosionDamage), canSeeCamo, isExplosive: true); // é a explosão em si — o que a cerâmica é imune
        }

        // Napalm: o dano fica no terreno depois que a explosão passa.
        if (fireDamage > 0f)
        {
            SpikeField.Spawn(
                transform.position, fireRadius, Mathf.Max(1, Mathf.RoundToInt(fireDamage)),
                0, fireDuration, 0.3f, enemyMask, canSeeCamo,
                new Color(1f, 0.45f, 0.1f, 0.55f), isSharp: false); // fogo queima até chumbo E até cerâmica (isExplosive fica false por padrão: fogo não é a explosão)
        }

        // Bomba cacho: explosões secundárias em anel, com metade do dano e do raio.
        if (clusterCount > 0)
        {
            int count = clusterCount;
            clusterCount = 0; // as filhas não voltam a se dividir
            for (int i = 0; i < count; i++)
            {
                float ang = (360f / count) * i * Mathf.Deg2Rad;
                Vector3 pos = transform.position + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * explosionRadius;
                SecondaryBlast(pos, explosionRadius * 0.6f, explosionDamage * 0.5f);
            }
        }

        Destroy(gameObject);
    }

    // Explosão secundária: só dano em área, sem projétil nem novos filhotes.
    private void SecondaryBlast(Vector3 position, float radius, float damage)
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(position, IsoGrid.WorldRadiusFor(radius), enemyMask))
        {
            if (IsoGrid.CellDistance(position, hit.transform.position) > radius) continue;
            if (hit.TryGetComponent(out Health health))
                health.TakeDamage(Mathf.RoundToInt(damage), canSeeCamo, isExplosive: true); // filhote da bomba cacho: também é explosão
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}