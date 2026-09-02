using System.Collections.Generic;
using UnityEngine;

// Gerador — VERBO: economia. Não atira, não mira, não mata NADA: rende dinheiro no fim de cada rodada.
// É a torre que cria a decisão central de um TD de progressão — gastar agora para sobreviver à próxima
// rodada, ou plantar economia agora para poder comprar o que realmente vence a partida lá na frente.
// Ocupa um plot e não defende: cada gerador é um buraco na sua defesa apostando no futuro.
public class FarmTower : TowerBase
{
    [Header("Gerador - produção")]
    [SerializeField] private int incomePerWave = 100;

    [Header("Gerador - juros")]
    // Recalibrado: 0.05/300 deixava o caixa compor rápido demais (ver Playtests/ — r8→r9 saltou
    // de $2.041 para $5.075 numa rodada só). 0.02/100 ainda recompensa investir em Juros, mas sem
    // a bola de neve que tornava a defesa irrelevante por volta da rodada 9.
    [SerializeField, Range(0f, 0.5f)] private float interestRate = 0.02f; // sobre o saldo, com a habilidade
    [SerializeField] private int interestCap = 100;                       // teto por rodada, evita bola de neve

    private int totalGenerated;

    protected override void DefinePaths(List<UpgradeTier> a, List<UpgradeTier> b)
    {
        // Trilha A — Produção: renda fixa maior
        a.Add(new UpgradeTier("Plantio Denso", "Rende bem mais por rodada.", 250, damageMult: 1.8f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("Colheita Mecanizada", "Renda muito maior.", 500, damageMult: 1.9f, scaleMult: 1.1f));
        a.Add(new UpgradeTier("LATIFÚNDIO", "Renda enorme por rodada.",
            1100, damageMult: 2.2f, scaleMult: 1.25f, tint: new Color(0.4f, 0.9f, 0.4f)));

        // Trilha B — Finanças: renda proporcional ao que você guardou
        b.Add(new UpgradeTier("Cofre", "Rende um pouco mais e guarda melhor.", 220, damageMult: 1.3f, scaleMult: 1.05f));
        b.Add(new UpgradeTier("Juros", "Paga JUROS sobre o dinheiro que você tem em caixa.",
            480, scaleMult: 1.1f, ability: "interest"));
        b.Add(new UpgradeTier("BANCO CENTRAL", "Juros muito maiores e teto bem mais alto.",
            1200, damageMult: 1.3f, scaleMult: 1.2f, ability: "centralbank", tint: new Color(1f, 0.85f, 0.3f)));
    }

    // A torre não atira: DamageMult é reaproveitado como multiplicador de renda.
    private int BaseIncome => Mathf.RoundToInt(incomePerWave * DamageMult);
    private bool PaysInterest => HasAbility("interest") || HasAbility("centralbank");
    private float CurrentInterest => HasAbility("centralbank") ? interestRate * 2f : interestRate;
    // Não é mais "sem teto": não medimos o Banco Central em playtest nenhum (ninguém chegou lá
    // nas 11 rodadas testadas), então um teto ilimitado aqui era chute puro. 3x o teto base é
    // generoso pelo investimento (1200, o upgrade mais caro do jogo) sem reabrir a bola de neve.
    private int CurrentCap => HasAbility("centralbank") ? interestCap * 3 : interestCap;

    private void OnEnable() => EnemySpawner.onWaveComplete.AddListener(Collect);
    private void OnDisable() => EnemySpawner.onWaveComplete.RemoveListener(Collect);

    private void Collect()
    {
        if (LevelManager.main == null) return;

        int amount = BaseIncome;
        if (PaysInterest)
            amount += Mathf.Min(CurrentCap, Mathf.RoundToInt(LevelManager.main.currency * CurrentInterest));

        LevelManager.main.IncreaseCurrency(amount);
        totalGenerated += amount;

        FloatingText.Spawn(transform.position, "+$" + amount, new Color(0.4f, 1f, 0.5f));
    }

    // Sem alvo, sem tiro: o gerador nunca dispara.
    protected override float FireInterval() => float.MaxValue;
    protected override bool TryFire() => false;

    // Não tem alcance de combate — mostrar o anel só confundiria o jogador.
    public override void ShowRange() { }

    public override string GetStatsText()
    {
        string juros = PaysInterest
            ? $"  ·  +{CurrentInterest * 100f:0}% do caixa (máx ${CurrentCap})"
            : "";
        return $"Renda ${BaseIncome}/rodada{juros}  ·  Total gerado: ${totalGenerated}";
    }
}
