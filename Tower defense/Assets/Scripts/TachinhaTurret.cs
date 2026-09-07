using System.Collections.Generic;
using UnityEngine;

// Tachinha — VERBO: negar área. Sozinha, espalha pregos em 8 direções. Com a trilha de Armadilha,
// deixa de atirar e passa a PLANTAR campos de espinhos sobre a rota, que ficam esperando os inimigos.
// É a única torre cujo dano não depende de mirar em alguém.
public class TachinhaTurret : TowerBase
{
    [Header("Tachinha - referências")]
    [SerializeField] private Transform firingPoint;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Tachinha - atributos base")]
    [SerializeField] private float aps = 1f;
    [SerializeField] private float baseDamage = 8f;
    [SerializeField] private float spawnOffset = 0.4f;

    [Header("Tachinha - campo de espinhos")]
    [SerializeField] private float fieldRadius = 0.9f;
    [SerializeField] private float fieldLifetime = 6f;
    [SerializeField] private int fieldCharges = 12;

    private static readonly Vector2[] directions = {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
        new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1)
    };

    protected override void DefinePaths(List<UpgradeTier> a, List<UpgradeTier> b)
    {
        // Trilha A — Poder dos pregos
        a.Add(new UpgradeTier("Pregos Afiados", "Pregos mais fortes.", 100, damageMult: 1.5f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("Pregos de Aço", "Ainda mais dano.", 200, damageMult: 1.6f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("ESTREPES", "Dano devastador e detecção de camuflados.",
            420, damageMult: 2f, scaleMult: 1.15f, grantsCamo: true, tint: new Color(1f, 0.75f, 0.3f)));

        // Trilha B — Armadilha: para de atirar e passa a plantar campos na rota
        b.Add(new UpgradeTier("Mão Rápida", "Dispara mais rápido.", 120, rateMult: 1.5f, scaleMult: 1.05f));
        b.Add(new UpgradeTier("Armadilha", "PLANTA campos de espinhos sobre a rota em vez de atirar pregos.",
            300, rangeMult: 1.3f, scaleMult: 1.1f, ability: "field"));
        b.Add(new UpgradeTier("CAMPO MINADO", "Campos permanentes, maiores e sem limite de cargas.",
            600, rangeMult: 1.4f, damageMult: 1.5f, scaleMult: 1.15f, ability: "minefield",
            tint: new Color(0.9f, 0.5f, 0.2f)));
    }

    protected override float FireInterval() => 1f / Mathf.Max(0.0001f, aps * RateMult);

    private bool PlantsField => HasAbility("field") || HasAbility("minefield");
    private bool Permanent => HasAbility("minefield");

    protected override bool TryFire()
    {
        if (!AnyEnemyInRange()) return false;

        if (PlantsField) return PlantField();
        return ShootNails();
    }

    // Substitui o antigo Physics2D.OverlapCircle: aquele único hit não dava pra refinar por
    // célula, então virava elipse. OverlapCircleAll + corte fino consegue achar "existe ALGUM
    // inimigo dentro do alcance de verdade" sem essa distorção.
    private bool AnyEnemyInRange()
    {
        int n = Targeting.Overlap(transform.position, targetingRange, enemyMask);
        for (int i = 0; i < n; i++)
        {
            Collider2D hit = Targeting.Alcancados[i];
            if (hit == null) continue;
            if (IsoGrid.CellDistance(transform.position, hit.transform.position) <= targetingRange) return true;
        }
        return false;
    }

    // Planta o campo sobre o ponto da rota mais próximo — o dano fica no terreno, não no alvo.
    private bool PlantField()
    {
        if (LevelManager.main == null) return false;

        Vector3 spot = LevelManager.main.ClosestPointOnPath(transform.position);
        if (IsoGrid.CellDistance(spot, transform.position) > targetingRange) return false;

        float radius = fieldRadius * (Permanent ? 1.8f : 1f);
        SpikeField.Spawn(
            spot, radius, CurrentDamage(),
            Permanent ? 0 : fieldCharges,
            Permanent ? 0f : fieldLifetime,
            0.25f, enemyMask, SeesCamo,
            new Color(1f, 0.6f, 0.2f, 0.6f));
        return true;
    }

    private bool ShootNails()
    {
        int dmg = CurrentDamage();
        foreach (Vector2 dir in directions)
        {
            Vector2 nd = dir.normalized;
            Vector3 spawnPos = firingPoint.position + (Vector3)(nd * spawnOffset);
            float angle = Mathf.Atan2(nd.y, nd.x) * Mathf.Rad2Deg - 90f;
            GameObject obj = Instantiate(bulletPrefab, spawnPos, Quaternion.Euler(0f, 0f, angle));
            IsoSorter.Attach(obj, moves: true); // prefab vem com sortingOrder fixo (1) — some atrás do tabuleiro sem isto
            TachinhaBullet bullet = obj.GetComponent<TachinhaBullet>();
            if (bullet != null) bullet.Init(nd, dmg, SeesCamo);
        }
        return true;
    }

    private int CurrentDamage() => Mathf.Max(1, Mathf.RoundToInt(baseDamage * DamageMult));

    public override string GetStatsText()
    {
        if (PlantsField)
        {
            string dur = Permanent ? "permanente" : $"{fieldLifetime:0}s · {fieldCharges} cargas";
            return $"Campo de espinhos: {CurrentDamage()} dano ({dur})  ·  Alcance {targetingRange:0.0}  ·  {(aps * RateMult):0.0}/s";
        }
        return $"Dano {CurrentDamage()}  ·  Alcance {targetingRange:0.0}  ·  {(aps * RateMult):0.0}/s  ·  8 direções";
    }
}
