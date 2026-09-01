using System.Collections.Generic;
using UnityEngine;

// Torre básica — VERBO: volume de dardos. Uma trilha engrossa o dardo até ele ATRAVESSAR a fila
// inteira; a outra multiplica os dardos num leque. Barata e sempre útil, é a régua do jogo.
public class Turret : TowerBase, ITargeting
{
    [Header("Torre - referências")]
    [SerializeField] private Transform turretRotationPoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firingPoint;

    [Header("Torre - atributos base")]
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float bps = 1f;
    [SerializeField] private int baseDamage = 1;

    [Header("Mira")]
    [SerializeField] private TargetingPriority targetPriority = TargetingPriority.First;

    private Transform target;

    protected override void DefinePaths(List<UpgradeTier> a, List<UpgradeTier> b)
    {
        // Trilha A — Penetração: o dardo deixa de parar no primeiro alvo
        a.Add(new UpgradeTier("Dardos Afiados", "Aumenta o dano por dardo.", 90, damageMult: 1.6f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("Ponta de Aço", "Cada dardo ATRAVESSA até 3 inimigos.",
            180, damageMult: 1.5f, scaleMult: 1.1f, ability: "pierce3"));
        a.Add(new UpgradeTier("LANÇA PERFURANTE", "Dano massivo e o dardo varre a fila inteira.",
            420, damageMult: 2f, scaleMult: 1.15f, ability: "pierce8", tint: new Color(0.85f, 0.85f, 0.95f)));

        // Trilha B — Volume: mais dardos por disparo
        b.Add(new UpgradeTier("Cano Longo", "Aumenta o alcance.", 80, rangeMult: 1.4f, scaleMult: 1.05f));
        b.Add(new UpgradeTier("Tiro Triplo", "Dispara 3 dardos em leque de uma vez.",
            240, rateMult: 1.2f, scaleMult: 1.1f, ability: "triple"));
        b.Add(new UpgradeTier("METRALHA DE DARDOS", "5 dardos por disparo, mais cadência e detecção de camuflados.",
            480, rateMult: 1.4f, grantsCamo: true, scaleMult: 1.15f, ability: "spread5",
            tint: new Color(0.6f, 1f, 0.8f)));
    }

    private int ShotCount => HasAbility("spread5") ? 5 : (HasAbility("triple") ? 3 : 1);
    private int PierceCount => HasAbility("pierce8") ? 8 : (HasAbility("pierce3") ? 3 : 1);

    protected override void Tick()
    {
        if (target == null || IsoGrid.CellDistance(target.position, transform.position) > targetingRange)
            target = AcquireTarget(targetPriority);

        if (target != null) RotateTowardsTarget();
    }

    protected override float FireInterval() => 1f / Mathf.Max(0.0001f, bps * RateMult);

    protected override bool TryFire()
    {
        if (target == null) return false;

        int shots = ShotCount;
        const float spreadDegrees = 12f; // abertura entre dardos vizinhos do leque

        for (int i = 0; i < shots; i++)
        {
            // Centraliza o leque no alvo: com 1 tiro o desvio é zero.
            float offset = shots == 1 ? 0f : (i - (shots - 1) * 0.5f) * spreadDegrees;
            Vector3 dir = Quaternion.Euler(0f, 0f, offset) * (target.position - firingPoint.position);

            GameObject obj = Instantiate(bulletPrefab, firingPoint.position, Quaternion.identity);
            Bullet bullet = obj.GetComponent<Bullet>();
            if (bullet == null) continue;

            // Os dardos laterais não são teleguiados: viajam na direção do leque.
            if (Mathf.Abs(offset) < 0.01f) bullet.SetTarget(target);
            else bullet.SetDirection(dir.normalized);

            bullet.SetDamage(CurrentDamage());
            bullet.SetCanSeeCamo(SeesCamo);
            bullet.SetSharp(true); // dardo é cortante (não fura chumbo)
            bullet.SetPierce(PierceCount);
        }
        return true;
    }

    private int CurrentDamage() => Mathf.Max(1, Mathf.RoundToInt(baseDamage * DamageMult));

    private void RotateTowardsTarget()
    {
        float angle = Mathf.Atan2(target.position.y - transform.position.y, target.position.x - transform.position.x) * Mathf.Rad2Deg - 90f;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);
        turretRotationPoint.rotation = Quaternion.RotateTowards(turretRotationPoint.rotation, rot, rotationSpeed * Time.deltaTime);
    }

    public override string GetStatsText()
        => $"Dano {CurrentDamage()}  ·  Alcance {targetingRange:0.0}  ·  {(bps * RateMult):0.0}/s";

    public void CycleTargeting() => targetPriority = Targeting.Next(targetPriority);
    public string GetTargetingLabel() => Targeting.Label(targetPriority);
}
