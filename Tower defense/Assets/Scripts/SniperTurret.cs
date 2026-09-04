using System.Collections.Generic;
using UnityEngine;

// Sniper — VERBO: alcance sem limite. Com a trilha de Observação, deixa de ter alcance e passa a mirar
// QUALQUER inimigo do mapa — é a única torre cuja posição no tabuleiro não importa.
// Em troca, atira devagar e num alvo só: existe para executar as ameaças grandes, não para limpar onda.
public class SniperTurret : TowerBase, ITargeting
{
    [Header("Sniper - referências")]
    [SerializeField] private Transform turretRotationPoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firingPoint;

    [Header("Sniper - atributos base")]
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float aps = 0.25f;
    [SerializeField] private float baseDamage = 150f;

    [Header("Mira")]
    [SerializeField] private TargetingPriority targetPriority = TargetingPriority.First;

    private Transform target;

    protected override void DefinePaths(List<UpgradeTier> a, List<UpgradeTier> b)
    {
        // Trilha A — Poder de fogo: executa o que for grande demais para as outras
        a.Add(new UpgradeTier("Munição Pesada", "Tiros muito mais fortes.", 200, damageMult: 1.8f, scaleMult: 1.08f));
        a.Add(new UpgradeTier("Perfurante", "IGNORA a armadura do alvo por completo.",
            400, damageMult: 1.6f, scaleMult: 1.08f, ability: "pierce_armor"));
        a.Add(new UpgradeTier("ANTI-BLINDADO", "Dano devastador e a bala atravessa uma fila inteira.",
            750, damageMult: 2.2f, scaleMult: 1.15f, ability: "line_shot", tint: new Color(0.9f, 0.3f, 0.3f)));

        // Trilha B — Observação: o alcance deixa de existir
        b.Add(new UpgradeTier("Ferrolho Rápido", "Recarrega mais rápido.", 250, rateMult: 1.5f, scaleMult: 1.05f));
        b.Add(new UpgradeTier("Luneta Térmica", "Enxerga camuflados; recarrega mais rápido.", 350, rateMult: 1.25f, grantsCamo: true, scaleMult: 1.05f));
        b.Add(new UpgradeTier("VISÃO TOTAL", "Alcance INFINITO: mira qualquer inimigo do mapa.",
            700, rateMult: 1.5f, scaleMult: 1.1f, ability: "global", tint: new Color(0.4f, 0.9f, 0.6f)));
    }

    // Alcance efetivo: a trilha de Observação simplesmente remove o limite.
    private float EffectiveRange => HasAbility("global") ? 1000f : targetingRange;

    protected override void Tick()
    {
        if (target == null || IsoGrid.CellDistance(target.position, transform.position) > EffectiveRange)
            target = Targeting.FindTarget(transform.position, EffectiveRange, enemyMask, targetPriority, SeesCamo);

        if (target != null) RotateTowardsTarget();
    }

    protected override float FireInterval() => 1f / Mathf.Max(0.0001f, aps * RateMult);

    protected override bool TryFire()
    {
        if (target == null) return false;

        GameObject obj = Instantiate(bulletPrefab, firingPoint.position, Quaternion.identity);
        IsoSorter.Attach(obj, moves: true); // prefab vem com sortingOrder fixo (1) — some atrás do tabuleiro sem isto
        Bullet bullet = obj.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.SetTarget(target);
            bullet.SetDamage(baseDamage * DamageMult);
            bullet.SetCanSeeCamo(SeesCamo);
            bullet.SetIgnoreArmor(HasAbility("pierce_armor") || HasAbility("line_shot"));
            if (HasAbility("line_shot")) bullet.SetPierce(10); // atravessa a fila inteira
        }
        return true;
    }

    private void RotateTowardsTarget()
    {
        Vector2 dir = (target.position - turretRotationPoint.position);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);
        turretRotationPoint.rotation = Quaternion.RotateTowards(turretRotationPoint.rotation, rot, rotationSpeed * Time.deltaTime);
    }

    public override string GetStatsText()
    {
        string alcance = HasAbility("global") ? "MAPA TODO" : $"{targetingRange:0.0}";
        string extra = HasAbility("line_shot") ? "  ·  atravessa fila" : (HasAbility("pierce_armor") ? "  ·  ignora armadura" : "");
        return $"Dano {baseDamage * DamageMult:0}  ·  Alcance {alcance}  ·  {(aps * RateMult):0.00}/s{extra}";
    }

    public void CycleTargeting() => targetPriority = Targeting.Next(targetPriority);
    public string GetTargetingLabel() => Targeting.Label(targetPriority);
}
