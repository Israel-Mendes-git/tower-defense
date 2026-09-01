using System.Collections.Generic;
using UnityEngine;

// Detector — VERBO: suporte. Praticamente não mata: MARCA inimigos (quem atirar neles bate mais forte)
// e empresta detecção de camuflados para as torres vizinhas.
// É a primeira torre do jogo cujo valor não é o dano dela, e sim o que ela faz as outras renderem.
public class DetectorTurret : TowerBase, ITargeting
{
    [Header("Detector - referências")]
    [SerializeField] private Transform turretRotationPoint;

    [Header("Detector - atributos base")]
    [SerializeField] private float aps = 1.2f;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private float markMultiplier = 1.35f; // dano que o alvo marcado passa a receber
    [SerializeField] private float markDuration = 2.5f;
    [SerializeField] private int marksPerPulse = 1;
    [SerializeField] private float supportRange = 3.5f;    // raio em que empresta detecção às vizinhas

    [Header("Mira")]
    [SerializeField] private TargetingPriority targetPriority = TargetingPriority.Strong;

    private Transform target;
    private int markedThisPulse;

    protected override void DefinePaths(List<UpgradeTier> a, List<UpgradeTier> b)
    {
        // Trilha A — Análise: marcas mais fortes
        a.Add(new UpgradeTier("Mira Assistida", "Alvos marcados recebem bem mais dano.", 150, damageMult: 1.3f, scaleMult: 1.05f));
        a.Add(new UpgradeTier("Ponto Fraco", "Expõe a falha na blindagem: marca muito mais forte.", 320, damageMult: 1.4f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("ALVO PRIORITÁRIO", "A marca DOBRA o dano recebido e dura o triplo.",
            650, damageMult: 1.5f, scaleMult: 1.15f, ability: "primetarget", tint: new Color(1f, 0.4f, 0.4f)));

        // Trilha B — Rede: marca mais alvos e cobre mais torres
        b.Add(new UpgradeTier("Varredura Ampla", "Aumenta o alcance de marcação e de suporte.", 140, rangeMult: 1.4f, scaleMult: 1.05f));
        b.Add(new UpgradeTier("Multi-alvo", "Marca 3 inimigos por pulso em vez de 1.",
            300, rateMult: 1.3f, scaleMult: 1.1f, ability: "multimark"));
        b.Add(new UpgradeTier("REDE DE SENSORES", "Marca TODOS no alcance e revela camuflados para o mapa inteiro.",
            700, rangeMult: 1.5f, scaleMult: 1.15f, ability: "sensornet", tint: new Color(0.7f, 0.5f, 1f)));
    }

    private float CurrentMarkMult => markMultiplier * DamageMult * (HasAbility("primetarget") ? 1.5f : 1f);
    private float CurrentMarkDuration => markDuration * (HasAbility("primetarget") ? 3f : 1f);
    private int CurrentMarks => HasAbility("sensornet") ? 99 : (HasAbility("multimark") ? 3 : marksPerPulse);
    private float CurrentSupportRange => HasAbility("sensornet") ? 999f : supportRange * RangeMult;

    protected override void Tick()
    {
        if (target == null || IsoGrid.CellDistance(target.position, transform.position) > targetingRange)
            target = AcquireTarget(targetPriority);

        if (target != null && turretRotationPoint != null) RotateTowardsTarget();

        LendDetectionToNeighbours();
    }

    // Empresta detecção de camuflados às torres no raio de suporte. Reaplicado todo frame:
    // se esta torre for vendida, o empréstimo simplesmente para de ser renovado.
    private void LendDetectionToNeighbours()
    {
        foreach (TowerBase t in FindObjectsOfType<TowerBase>())
        {
            if (t == this) continue;
            if (IsoGrid.CellDistance(t.transform.position, transform.position) <= CurrentSupportRange)
                t.GrantCamoDetection();
        }
    }

    protected override float FireInterval() => 1f / Mathf.Max(0.0001f, aps * RateMult);

    protected override bool TryFire()
    {
        markedThisPulse = 0;
        int limit = CurrentMarks;

        // Pré-filtro pelo raio de mundo (ver IsoGrid), corte fino por célula: sem o corte, o pulso
        // marcaria inimigo na diagonal bem além do alcance de verdade.
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, IsoGrid.WorldRadiusFor(targetingRange), enemyMask);
        foreach (var hit in hits)
        {
            if (markedThisPulse >= limit) break;
            if (IsoGrid.CellDistance(transform.position, hit.transform.position) > targetingRange) continue;

            Health h = hit.GetComponent<Health>();
            if (h == null) continue;
            if (h.IsCamo && !SeesCamo) continue;

            h.Mark(CurrentMarkMult, CurrentMarkDuration);
            markedThisPulse++;
        }
        return markedThisPulse > 0;
    }

    private void RotateTowardsTarget()
    {
        Vector2 dir = target.position - turretRotationPoint.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        turretRotationPoint.rotation = Quaternion.RotateTowards(
            turretRotationPoint.rotation, Quaternion.Euler(0f, 0f, angle), rotationSpeed * Time.deltaTime);
    }

    public override string GetStatsText()
    {
        string alvos = CurrentMarks >= 99 ? "todos" : CurrentMarks.ToString();
        string rede = HasAbility("sensornet") ? "mapa todo" : $"{CurrentSupportRange:0.0}";
        return $"Marca {alvos} alvo(s): +{(CurrentMarkMult - 1f) * 100f:0}% dano por {CurrentMarkDuration:0.0}s"
             + $"  ·  Alcance {targetingRange:0.0}  ·  Revela camo p/ torres em {rede}";
    }

    public void CycleTargeting() => targetPriority = Targeting.Next(targetPriority);
    public string GetTargetingLabel() => Targeting.Label(targetPriority);
}
