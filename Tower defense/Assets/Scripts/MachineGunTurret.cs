using System.Collections.Generic;
using UnityEngine;

// Metralhadora — VERBO: rampa. Começa lenta e ACELERA enquanto tiver alvo; se ficar sem atirar, esfria.
// Péssima contra inimigos esparsos, devastadora contra fluxo contínuo — a única torre cujo
// desempenho depende de ONDE ela está na rota, e não só de quanto custou.
public class MachineGunTurret : TowerBase, ITargeting
{
    [Header("Metralhadora - referências")]
    [SerializeField] private Transform turretRotationPoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firingPoint;

    [Header("Metralhadora - atributos base")]
    [SerializeField] private float rotationSpeed = 400f;
    [SerializeField] private float bps = 4f;
    [SerializeField] private int baseDamage = 1;

    [Header("Metralhadora - aquecimento")]
    [SerializeField] private float maxSpinMult = 3f;   // cadência máxima com o cano girando a todo vapor
    [SerializeField] private float spinUpTime = 3f;    // segundos atirando até o máximo
    [SerializeField] private float coolDownRate = 2f;  // quão rápido perde o giro parado

    [Header("Mira")]
    [SerializeField] private TargetingPriority targetPriority = TargetingPriority.First;

    private Transform target;
    private float spin; // 0..1

    protected override void DefinePaths(List<UpgradeTier> a, List<UpgradeTier> b)
    {
        // Trilha A — Calibre
        a.Add(new UpgradeTier("Calibre Maior", "Cada bala dói mais.", 160, damageMult: 1.7f, scaleMult: 1.08f));
        a.Add(new UpgradeTier("Munição Perfurante", "Balas atravessam blindagem.", 340, damageMult: 1.8f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("CANHÃO ROTATIVO", "Dano brutal e detecção de camuflados.",
            700, damageMult: 2.2f, scaleMult: 1.2f, grantsCamo: true, tint: new Color(1f, 0.85f, 0.2f)));

        // Trilha B — Giro
        b.Add(new UpgradeTier("Cano Aliviado", "Aquece o dobro de rápido.", 140, scaleMult: 1.05f, ability: "fastspin"));
        b.Add(new UpgradeTier("Refrigeração", "Não esfria mais entre rajadas.", 300, rateMult: 1.2f, scaleMult: 1.08f, ability: "nocool"));
        b.Add(new UpgradeTier("GIRO PERPÉTUO", "Já começa no giro máximo e nunca desacelera.",
            620, rateMult: 1.3f, scaleMult: 1.15f, ability: "maxspin", tint: new Color(1f, 0.6f, 0.1f)));
    }

    private float SpinUpSpeed => 1f / Mathf.Max(0.05f, spinUpTime) * (HasAbility("fastspin") ? 2f : 1f);
    private float SpinMultiplier => Mathf.Lerp(1f, maxSpinMult, spin);

    protected override void Tick()
    {
        if (target == null || IsoGrid.CellDistance(target.position, transform.position) > targetingRange)
            target = AcquireTarget(targetPriority);

        if (HasAbility("maxspin")) { spin = 1f; }
        else if (target != null) spin = Mathf.Min(1f, spin + SpinUpSpeed * Time.deltaTime);
        else if (!HasAbility("nocool")) spin = Mathf.Max(0f, spin - coolDownRate * Time.deltaTime * SpinUpSpeed);

        if (target != null && turretRotationPoint != null) RotateTowardsTarget();
    }

    protected override float FireInterval() => 1f / Mathf.Max(0.0001f, bps * RateMult * SpinMultiplier);

    protected override bool TryFire()
    {
        if (target == null || bulletPrefab == null || firingPoint == null) return false;

        GameObject obj = Instantiate(bulletPrefab, firingPoint.position, Quaternion.identity);
        Bullet bullet = obj.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.SetTarget(target);
            bullet.SetDamage(CurrentDamage());
            bullet.SetCanSeeCamo(SeesCamo);
            bullet.SetSharp(true);
        }
        return true;
    }

    private int CurrentDamage() => Mathf.Max(1, Mathf.RoundToInt(baseDamage * DamageMult));

    private void RotateTowardsTarget()
    {
        Vector2 dir = target.position - turretRotationPoint.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        turretRotationPoint.rotation = Quaternion.RotateTowards(
            turretRotationPoint.rotation, Quaternion.Euler(0f, 0f, angle), rotationSpeed * Time.deltaTime);
    }

    public override string GetStatsText()
        => $"Dano {CurrentDamage()}  ·  Alcance {targetingRange:0.0}  ·  {(bps * RateMult * SpinMultiplier):0.0}/s"
         + $"  ·  Giro {spin * 100f:0}% (máx {maxSpinMult:0}x)";

    public void CycleTargeting() => targetPriority = Targeting.Next(targetPriority);
    public string GetTargetingLabel() => Targeting.Label(targetPriority);
}
