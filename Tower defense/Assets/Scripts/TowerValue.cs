using UnityEngine;

// Guarda o total investido numa torre (construção + upgrades) e cuida da venda com reembolso.
public class TowerValue : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float refundRate = 0.7f;

    private int invested;
    private Plot ownerPlot;

    public void Init(int buildCost, Plot plot)
    {
        invested = buildCost;
        ownerPlot = plot;
    }

    public void AddInvestment(int amount) => invested += amount;

    // Quanto o jogador enterrou nesta torre (construção + upgrades). É o peso que o
    // DefenseReadout usa para medir onde está apostado o dinheiro da defesa.
    public int Invested => invested;

    public int SellValue => Mathf.RoundToInt(invested * refundRate);

    public void Sell()
    {
        if (LevelManager.main != null)
            LevelManager.main.IncreaseCurrency(SellValue);

        Remover();
    }

    // Torre destruída pelo inimigo (ver TowerIntegrity). Mesmo caminho da venda, sem o
    // reembolso — quem paga a sucata é o TowerIntegrity, com a própria taxa. Precisa passar por
    // aqui e não por um Destroy solto: sem liberar o plot e sair do registro do BuildManager, a
    // torre morta continuaria contando como defesa para o DefenseReadout e o plot ficaria
    // ocupado por um fantasma.
    public void Demolir() => Remover();

    private void Remover()
    {
        if (ownerPlot != null)
            ownerPlot.ClearTower();

        if (BuildManager.main != null)
            BuildManager.main.UnregisterPlacedTower(gameObject);

        Destroy(gameObject);
    }
}
