using UnityEngine;

// Aliado móvel — a peça que separa este jogo de um tower defense comum.
// Não ocupa plot e não fica parado: você marca uma ZONA no mapa e ele patrulha dentro dela,
// caçando o que entrar. Sobe de nível sozinho durante a partida, morre e renasce.
// A decisão que ele cria é contínua: para onde mandar quando a pressão muda de lugar.
public class Ally : MonoBehaviour
{
    [Header("Combate")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float attackRange = 0.7f;
    [SerializeField] private float attacksPerSecond = 1.5f;
    [SerializeField] private int baseDamage = 5;
    [SerializeField] private LayerMask enemyMask;

    [Header("Vida")]
    [SerializeField] private int maxHP = 40;
    [SerializeField] private float respawnTime = 12f;
    [SerializeField] private float damagePerSecondInContact = 4f; // desgaste ao brigar

    [Header("Progressão")]
    [SerializeField] private int xpPerKill = 10;
    [SerializeField] private int xpPerLevel = 60;
    [SerializeField] private int maxLevel = 10;

    [Header("Zona de patrulha")]
    [SerializeField] private float zoneRadius = 2.5f;

    private Vector3 zoneCenter;
    private Vector3 wanderTarget;
    private Transform prey;
    private float attackTimer;
    private float wanderTimer;
    private float contactDamageAccum;

    private int hp;
    private int level = 1;
    private int xp;
    private bool dead;
    private float respawnAt;

    private SpriteRenderer sr;
    private RangeIndicator zoneVisual;
    private Color baseColor;

    public string AllyName = "Aliado";
    public int Level => level;
    public bool IsDead => dead;
    public float ZoneRadius => zoneRadius;
    public Vector3 ZoneCenter => zoneCenter;

    // Escala de poder por nível: cresce, mas sem virar bola de neve.
    private int CurrentDamage => Mathf.RoundToInt(baseDamage * (1f + 0.25f * (level - 1)));
    private int CurrentMaxHP => Mathf.RoundToInt(maxHP * (1f + 0.2f * (level - 1)));

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        zoneCenter = transform.position;
        wanderTarget = zoneCenter;
        hp = CurrentMaxHP;
    }

    // Define para onde o aliado deve ir patrulhar. É a ordem que o jogador dá durante a onda.
    public void SetZone(Vector3 center)
    {
        zoneCenter = center;
        wanderTarget = center;
        prey = null;
        if (zoneVisual != null && zoneVisual.gameObject.activeSelf) ShowZone();
    }

    private void Update()
    {
        if (dead)
        {
            if (Time.time >= respawnAt) Respawn();
            return;
        }

        AcquirePrey();

        if (prey != null) ChaseAndAttack();
        else Wander();
    }

    // Só caça o que estiver dentro da zona (com uma folga): a ordem do jogador é para valer.
    private void AcquirePrey()
    {
        if (prey != null)
        {
            bool foraDaZona = IsoGrid.CellDistance(prey.position, zoneCenter) > zoneRadius * 1.35f;
            if (!prey.gameObject.activeInHierarchy || foraDaZona) prey = null;
            else return;
        }

        // Pré-filtro pelo raio de mundo que cobre a elipse (ver IsoGrid), corte fino por célula:
        // a zona marcada pelo jogador tem que valer o mesmo raio redondo em qualquer direção.
        Collider2D[] hits = Physics2D.OverlapCircleAll(zoneCenter, IsoGrid.WorldRadiusFor(zoneRadius), enemyMask);
        float best = float.MaxValue;
        foreach (var hit in hits)
        {
            if (hit.GetComponent<Health>() == null) continue;
            if (IsoGrid.CellDistance(hit.transform.position, zoneCenter) > zoneRadius) continue;
            float d = IsoGrid.CellDistance(hit.transform.position, transform.position);
            if (d < best) { best = d; prey = hit.transform; }
        }
    }

    private void ChaseAndAttack()
    {
        float dist = IsoGrid.CellDistance(transform.position, prey.position);

        if (dist > attackRange)
        {
            transform.position = Vector3.MoveTowards(transform.position, prey.position, moveSpeed * Time.deltaTime);
            return;
        }

        // Em contato: bate e apanha. É o que faz o aliado ser um recurso perecível, não uma torre grátis.
        attackTimer += Time.deltaTime;
        if (attackTimer >= 1f / Mathf.Max(0.01f, attacksPerSecond))
        {
            attackTimer = 0f;
            Health h = prey.GetComponent<Health>();
            if (h != null)
            {
                int before = h.GetHitPoints();
                h.TakeDamage(CurrentDamage, true); // aliados enxergam camuflados
                if (before > 0 && (h == null || h.GetHitPoints() <= 0)) GainXP(xpPerKill);
            }
        }

        contactDamageAccum += damagePerSecondInContact * Time.deltaTime;
        if (contactDamageAccum >= 1f)
        {
            int dmg = Mathf.FloorToInt(contactDamageAccum);
            contactDamageAccum -= dmg;
            TakeDamage(dmg);
        }
    }

    private void Wander()
    {
        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0f || Vector2.Distance(transform.position, wanderTarget) < 0.15f)
        {
            Vector2 offset = Random.insideUnitCircle * zoneRadius * 0.8f;
            wanderTarget = zoneCenter + new Vector3(offset.x, offset.y, 0f);
            wanderTimer = Random.Range(1.2f, 2.5f);
        }
        transform.position = Vector3.MoveTowards(transform.position, wanderTarget, moveSpeed * 0.6f * Time.deltaTime);
    }

    private void TakeDamage(int amount)
    {
        hp -= amount;
        if (sr != null) sr.color = Color.Lerp(new Color(0.6f, 0.2f, 0.2f), baseColor, Mathf.Clamp01((float)hp / CurrentMaxHP));
        if (hp <= 0) Die();
    }

    private void Die()
    {
        dead = true;
        respawnAt = Time.time + respawnTime;
        prey = null;
        if (sr != null) sr.enabled = false;
        FloatingText.Spawn(transform.position, AllyName + " caiu!", new Color(1f, 0.4f, 0.4f), transform.localScale.x);
    }

    private void Respawn()
    {
        dead = false;
        hp = CurrentMaxHP;
        contactDamageAccum = 0f;
        transform.position = zoneCenter;
        if (sr != null) { sr.enabled = true; sr.color = baseColor; }
        FloatingText.Spawn(transform.position, AllyName + " voltou", new Color(0.6f, 1f, 0.6f), transform.localScale.x);
    }

    private void GainXP(int amount)
    {
        if (level >= maxLevel) return;

        xp += amount;
        while (xp >= xpPerLevel * level && level < maxLevel)
        {
            xp -= xpPerLevel * level;
            level++;
            hp = CurrentMaxHP; // subir de nível cura
            FloatingText.Spawn(transform.position, "Nível " + level + "!", new Color(1f, 0.9f, 0.3f), transform.localScale.x);
            transform.localScale *= 1.05f;
        }
    }

    // ───────── Seleção / UI ─────────
    private void OnMouseDown()
    {
        if (UIManager.main != null && UIManager.main.IsHoveringUI()) return;
        if (AllyManager.main != null) AllyManager.main.Select(this);
    }

    public void ShowZone()
    {
        if (zoneVisual == null) zoneVisual = RangeIndicator.Create(transform);
        // O indicador é filho do aliado, mas a zona é fixa no mundo: reposiciona a cada exibição.
        zoneVisual.transform.position = zoneCenter;
        zoneVisual.Show(zoneRadius, new Color(0.5f, 1f, 0.6f, 0.5f));
    }

    public void HideZone()
    {
        if (zoneVisual != null) zoneVisual.Hide();
    }

    private void LateUpdate()
    {
        // Mantém o círculo da zona ancorado no mundo enquanto o aliado anda por dentro dela.
        if (zoneVisual != null && zoneVisual.gameObject.activeSelf)
            zoneVisual.transform.position = zoneCenter;
    }

    public string StatusText()
    {
        if (dead) return $"{AllyName} — caído (volta em {Mathf.CeilToInt(respawnAt - Time.time)}s)";
        return $"{AllyName} · Nível {level}/{maxLevel} · Vida {hp}/{CurrentMaxHP} · Dano {CurrentDamage}";
    }
}
