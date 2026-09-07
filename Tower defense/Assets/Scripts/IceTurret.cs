using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Torre de gelo — VERBO: controle de posição. Não mata: desacelera e, com a trilha de Ventania,
// EMPURRA os inimigos de volta pela rota, desfazendo o avanço deles.
// É a única torre que devolve terreno; o valor dela cresce com o quanto o inimigo já andou.
public class IceTurret : TowerBase
{
    [Header("Gelo - atributos base")]
    [SerializeField] private float aps = 1f;
    [SerializeField] private float freezeTime = 1f;
    [SerializeField] private float slowedSpeed = 0.5f;

    [Header("Gelo - empurrão")]
    [SerializeField] private float pushDistance = 1.2f; // por pulso, quando a habilidade está ativa

    protected override void DefinePaths(List<UpgradeTier> a, List<UpgradeTier> b)
    {
        // Trilha A — Frio: trava o avanço
        a.Add(new UpgradeTier("Frio Intenso", "Desacelera mais e por mais tempo.", 120, damageMult: 1.5f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("Gelo Profundo", "Congelamento severo.", 220, damageMult: 1.5f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("PERMAFROST", "Inimigos congelados PARAM por completo enquanto durar o gelo.",
            500, damageMult: 1.8f, scaleMult: 1.2f, ability: "freeze", tint: new Color(0.55f, 0.85f, 1f)));

        // Trilha B — Ventania: devolve terreno
        b.Add(new UpgradeTier("Sopro Gelado", "Aumenta o alcance.", 130, rangeMult: 1.4f, scaleMult: 1.05f));
        b.Add(new UpgradeTier("Ventania", "EMPURRA os inimigos de volta pela rota a cada pulso.",
            280, rateMult: 1.2f, scaleMult: 1.1f, ability: "push"));
        b.Add(new UpgradeTier("VENTO POLAR", "Empurrão muito mais forte, mais alcance e detecção de camuflados.",
            520, rangeMult: 1.3f, rateMult: 1.3f, grantsCamo: true, scaleMult: 1.15f, ability: "bigpush",
            tint: new Color(0.7f, 0.95f, 1f)));
    }

    protected override float FireInterval() => 1f / Mathf.Max(0.0001f, aps * RateMult);

    private bool Pushes => HasAbility("push") || HasAbility("bigpush");
    private float PushAmount => pushDistance * (HasAbility("bigpush") ? 2.5f : 1f);
    private float SlowSpeed => HasAbility("freeze") ? 0f : slowedSpeed / Mathf.Max(1f, DamageMult);

    protected override bool TryFire()
    {
        float slow = SlowSpeed;
        float freeze = freezeTime * DamageMult;
        bool hitAny = false;

        int n = Targeting.Overlap(transform.position, targetingRange, enemyMask);
        for (int i = 0; i < n; i++)
        {
            Collider2D hit = Targeting.Alcancados[i];
            if (hit == null) continue;
            if (IsoGrid.CellDistance(transform.position, hit.transform.position) > targetingRange) continue;

            if (!SeesCamo)
            {
                Health h = hit.GetComponent<Health>();
                if (h != null && h.IsCamo) continue;
            }

            EnemyMovement em = hit.GetComponent<EnemyMovement>();
            if (em == null) continue;

            em.UpdateSpeed(slow);
            StartCoroutine(ResetEnemySpeed(em, freeze));

            if (Pushes) em.PushBack(PushAmount);
            hitAny = true;
        }
        return hitAny;
    }

    private IEnumerator ResetEnemySpeed(EnemyMovement em, float freeze)
    {
        yield return new WaitForSeconds(freeze);
        if (em != null) em.ResetSpeed();
    }

    public override string GetStatsText()
    {
        string effect = HasAbility("freeze") ? "PARALISA" : $"Lentidão p/ {SlowSpeed:0.00}";
        string push = Pushes ? $"  ·  Empurra {PushAmount:0.0}m" : "";
        return $"{effect}  ·  {freezeTime * DamageMult:0.0}s{push}  ·  Alcance {targetingRange:0.0}  ·  {(aps * RateMult):0.0}/s";
    }
}
