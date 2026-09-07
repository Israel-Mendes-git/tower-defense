using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Mede a FOLGA da defesa: o quanto a ameaça chega perto de importar.
//
// POR QUE ISTO EXISTE. O jogo vinha sendo vencido com dano zero em 36 rodadas seguidas, e "dano
// recebido = 0" não gradua nada — não distingue "passou raspando" de "não chegou perto". Sem um
// número que varie, calibrar intensidade é chutar e torcer.
//
// A medida é: QUANTO DO TRAÇADO os inimigos conseguem percorrer antes de morrer. Se a onda toda
// morre nos primeiros 20%, o poder de fogo é tão maior que a ameaça que aumentar a quantidade não
// muda nada — eles só morrem um pouco mais adiante. Se a onda chega a 80%, a rodada está no fio.
// É essa fração que precisa subir com a rodada, não o dano.
//
// OBSERVADOR PURO: não altera nada. Anexado em runtime.
public class ThreatProbe : MonoBehaviour
{
    [SerializeField] private float amostrasPorSegundo = 4f;

    private class Rodada
    {
        public int numero;
        public float progressoMaximo;   // o mais longe que ALGUÉM chegou
        public float somaDosMaximos;    // para a média por inimigo
        public int inimigosVistos;
        public int vazaram;             // chegaram ao fim
    }

    private readonly List<Rodada> historico = new List<Rodada>();
    private readonly Dictionary<int, float> maximoPorInimigo = new Dictionary<int, float>();
    private Rodada atual;
    private float proximaAmostra;

    public static ThreatProbe main;
    private void Awake() => main = this;

    private void OnEnable() => EnemySpawner.onWaveComplete.AddListener(FecharRodada);
    private void OnDisable() => EnemySpawner.onWaveComplete.RemoveListener(FecharRodada);

    private void Update()
    {
        if (EnemySpawner.main == null || !EnemySpawner.main.IsWaveActive) return;
        if (Time.time < proximaAmostra) return;
        proximaAmostra = Time.time + 1f / Mathf.Max(0.5f, amostrasPorSegundo);

        if (atual == null || atual.numero != EnemySpawner.main.CurrentWave)
        {
            atual = new Rodada();
            atual.numero = EnemySpawner.main.CurrentWave;
            maximoPorInimigo.Clear();
        }

        foreach (EnemyMovement e in FindObjectsOfType<EnemyMovement>())
        {
            int id = e.gameObject.GetInstanceID();
            float p = e.ProgressoNoTracado;

            float anterior;
            // Guarda o MÁXIMO por inimigo: ele morre no ponto mais distante que alcançou, e é
            // esse ponto que diz onde a defesa o parou.
            if (!maximoPorInimigo.TryGetValue(id, out anterior) || p > anterior)
                maximoPorInimigo[id] = p;

            if (p > atual.progressoMaximo) atual.progressoMaximo = p;
        }
    }

    private void FecharRodada()
    {
        if (atual == null) return;

        foreach (var kv in maximoPorInimigo)
        {
            atual.somaDosMaximos += kv.Value;
            atual.inimigosVistos++;
            if (kv.Value >= 0.999f) atual.vazaram++;
        }
        maximoPorInimigo.Clear();

        historico.Add(atual);
        Debug.Log("[ThreatProbe] " + LinhaDa(atual));
        atual = null;
    }

    private static string LinhaDa(Rodada r)
    {
        float media = r.inimigosVistos > 0 ? r.somaDosMaximos / r.inimigosVistos : 0f;
        return "rodada=" + r.numero
            + " progresso_medio=" + Mathf.RoundToInt(media * 100f) + "%"
            + " progresso_maximo=" + Mathf.RoundToInt(r.progressoMaximo * 100f) + "%"
            + " vazaram=" + r.vazaram + "/" + r.inimigosVistos;
    }

    public string Resumo()
    {
        if (historico.Count == 0) return "(sem rodadas)";
        float soma = 0f, pior = 0f;
        int vazaramTotal = 0;
        foreach (Rodada r in historico)
        {
            float media = r.inimigosVistos > 0 ? r.somaDosMaximos / r.inimigosVistos : 0f;
            soma += media;
            if (r.progressoMaximo > pior) pior = r.progressoMaximo;
            vazaramTotal += r.vazaram;
        }
        return "rodadas=" + historico.Count
            + " progresso_medio_geral=" + Mathf.RoundToInt(soma / historico.Count * 100f) + "%"
            + " maior_progresso=" + Mathf.RoundToInt(pior * 100f) + "%"
            + " vazamentos=" + vazaramTotal;
    }
}
