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

    public int SellValue => Mathf.RoundToInt(invested * refundRate);

    public void Sell()
    {
        if (LevelManager.main != null)
            LevelManager.main.IncreaseCurrency(SellValue);

        if (ownerPlot != null)
            ownerPlot.ClearTower();

        if (BuildManager.main != null)
            BuildManager.main.UnregisterPlacedTower(gameObject);

        Destroy(gameObject);
    }
}
