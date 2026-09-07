using System.Collections.Generic;
using UnityEngine;

// Tesla — VERBO: energizar. Eletrocuta todos no alcance e, pela trilha do Gerador, ENERGIZA as torres
// vizinhas (mais cadência e dano). É a torre que se paga pelo que faz as outras renderem —
// vale mais cercada de torres do que sozinha na melhor posição do mapa.
public class TeslaTurret : TowerBase
{
    [Header("Tesla - atributos base")]
    [SerializeField] private float aps = 1.5f;
    [SerializeField] private int baseDamage = 3;

    [Header("Tesla - gerador")]
    [SerializeField] private float energizeRange = 3f;
    [SerializeField] private float energizeRate = 1.25f;   // multiplicador de cadência nas vizinhas
    [SerializeField] private float energizeDamage = 1.15f;

    protected override void DefinePaths(List<UpgradeTier> a, List<UpgradeTier> b)
    {
        // Trilha A — Voltagem: dano em área
        a.Add(new UpgradeTier("Alta Voltagem", "Choques mais fortes.", 150, damageMult: 1.6f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("Sobrecarga", "Dano elétrico severo.", 300, damageMult: 1.7f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("TEMPESTADE", "Dano brutal em área e detecção de camuflados.",
            600, damageMult: 2.2f, scaleMult: 1.2f, grantsCamo: true, tint: new Color(0.5f, 0.8f, 1f)));

        // Trilha B — Gerador: vira torre de suporte
        b.Add(new UpgradeTier("Bobina Ampliada", "Aumenta o alcance.", 130, rangeMult: 1.4f, scaleMult: 1.05f));
        b.Add(new UpgradeTier("Gerador", "ENERGIZA as torres vizinhas: mais cadência e dano para elas.",
            320, rateMult: 1.2f, scaleMult: 1.1f, ability: "energize"));
        b.Add(new UpgradeTier("USINA", "Energiza muito mais forte e num raio bem maior.",
            680, rangeMult: 1.3f, scaleMult: 1.2f, ability: "powerplant", tint: new Color(0.3f, 0.6f, 1f)));
    }

    private bool Energizes => HasAbility("energize") || HasAbility("powerplant");
    private float EnergizeRadius => energizeRange * RangeMult * (HasAbility("powerplant") ? 2f : 1f);
    private float EnergizeRateBonus => HasAbility("powerplant") ? energizeRate + 0.35f : energizeRate;
    private float EnergizeDamageBonus => HasAbility("powerplant") ? energizeDamage + 0.25f : energizeDamage;

    protected override void Tick()
    {
        if (!Energizes) return;

        // Reaplicado a cada frame: quando esta torre some, o buff das vizinhas simplesmente deixa
        // de ser renovado (o TowerBuffs limpa os buffs no início do frame).
        foreach (TowerBase t in FindObjectsOfType<TowerBase>())
        {
            if (t == this) continue;
            if (IsoGrid.CellDistance(t.transform.position, transform.position) <= EnergizeRadius)
                t.ApplyBuff(damage: EnergizeDamageBonus, rate: EnergizeRateBonus);
        }
    }

    protected override float FireInterval() => 1f / Mathf.Max(0.0001f, aps * RateMult);

    protected override bool TryFire()
    {
        int dmg = CurrentDamage();
        int n = Targeting.Overlap(transform.position, targetingRange, enemyMask);
        bool hitAny = false;
        for (int i = 0; i < n; i++)
        {
            Collider2D hit = Targeting.Alcancados[i];
            if (hit == null) continue;
            if (IsoGrid.CellDistance(transform.position, hit.transform.position) > targetingRange) continue;

            Health h = hit.GetComponent<Health>();
            if (h == null) continue;
            if (h.IsCamo && !SeesCamo) continue;
            h.TakeDamage(dmg, SeesCamo);
            hitAny = true;
        }
        return hitAny;
    }

    private int CurrentDamage() => Mathf.Max(1, Mathf.RoundToInt(baseDamage * DamageMult));

    public override string GetStatsText()
    {
        string energia = Energizes
            ? $"  ·  ENERGIZA vizinhas em {EnergizeRadius:0.0}: +{(EnergizeRateBonus - 1f) * 100f:0}% cadência, +{(EnergizeDamageBonus - 1f) * 100f:0}% dano"
            : "";
        return $"Dano {CurrentDamage()} em área  ·  Alcance {targetingRange:0.0}  ·  {(aps * RateMult):0.0}/s{energia}";
    }
}
