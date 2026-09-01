using UnityEngine;

// Ladrão — rouba dinheiro enquanto atravessa e leva o saque embora se escapar.
// Diferente de todo outro inimigo, o custo de deixá-lo passar não é vida: é a sua economia.
// Matá-lo devolve o que ele carregava, o que transforma o alvo dele numa decisão de prioridade.
public class Thief : MonoBehaviour
{
    [SerializeField] private int stealPerTick = 15;
    [SerializeField] private float interval = 2f;
    [SerializeField] private float returnRate = 1f; // fração do saque devolvida ao matá-lo

    private float nextSteal;
    private int stolen;

    private void Update()
    {
        if (LevelManager.main == null || Time.time < nextSteal) return;
        nextSteal = Time.time + interval;

        int amount = Mathf.Min(stealPerTick, LevelManager.main.currency);
        if (amount <= 0) return;

        LevelManager.main.SpendCurrency(amount);
        stolen += amount;
        FloatingText.Spawn(transform.position, "-$" + amount, new Color(1f, 0.4f, 0.4f));
    }

    // Chamado pelo Health na morte: o saque volta para o jogador.
    public void ReturnLoot()
    {
        if (stolen <= 0 || LevelManager.main == null) return;

        int devolvido = Mathf.RoundToInt(stolen * returnRate);
        LevelManager.main.IncreaseCurrency(devolvido);
        FloatingText.Spawn(transform.position, "recuperado +$" + devolvido, new Color(0.5f, 1f, 0.6f));
        stolen = 0;
    }
}
