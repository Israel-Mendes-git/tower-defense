using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Tipo de dano que uma torre entrega. É o vocabulário com que o jogo enxerga a sua defesa:
// as imunidades dos inimigos são declaradas nestes mesmos termos (chumbo ignora Sharp,
// cerâmica ignora Explosive), então "que perguntas a sua defesa não sabe responder" vira
// uma conta em vez de opinião.
public enum DamageKind
{
    Sharp,      // dardos, espinhos — chumbo é imune
    Explosive,  // explosão — cerâmica é imune
    Fire,       // napalm: queima chumbo E cerâmica
    Energy,     // Tesla
    Piercing,   // ignora armadura (Sniper)
    Control,    // Gelo: não mata, atrasa
    Support,    // Detector: marca e empresta detecção
    Economy,    // Gerador
    Unknown     // torre que ninguém classificou — ver AVISO em KindOf
}

// Retrato da defesa do jogador no fim de uma rodada. Dados puros, sem comportamento:
// quem decide o que fazer com isto é outro sistema.
public class DefenseSnapshot
{
    public int wave;
    public int towerCount;
    public int investedTotal;

    // Fração do INVESTIMENTO por tipo de dano (não do DPS): o quanto do seu dinheiro está
    // apostado em cada resposta. É medida mais honesta que DPS nominal, porque Bomba e
    // Tachinha acertam vários alvos por disparo e o DPS nominal delas mente (ver ROADMAP 2.4).
    public readonly Dictionary<DamageKind, float> investShare = new Dictionary<DamageKind, float>();

    public float pathCoverage;      // fração do traçado ao alcance de pelo menos uma torre
    public float camoCoverage;      // idem, mas só torres que enxergam camuflado
    public float averageOverlap;    // torres por ponto coberto: 1 = fila indiana, 3+ = cluster
    public int uncoveredStretches;  // trechos contíguos do traçado sem cobertura nenhuma

    public int currency;
    public int freePlots;

    // A REGRA DE OURO mora aqui: tipos de dano que o jogador consegue adquirir agora
    // (existe no catálogo, custa menos que o caixa, e há plot livre para colocar).
    // Quem for explorar uma lacuna da defesa precisa checar esta lista primeiro — cobrar
    // uma resposta que o jogador não tem como comprar não é dificuldade, é armadilha.
    public readonly HashSet<DamageKind> affordableKinds = new HashSet<DamageKind>();

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== defesa na rodada " + wave + " ===");
        sb.AppendLine("torres=" + towerCount + "  investido=$" + investedTotal + "  caixa=$" + currency + "  plots livres=" + freePlots);
        sb.Append("investimento por tipo:");
        foreach (var kv in investShare)
            if (kv.Value > 0f) sb.Append("  " + kv.Key + "=" + Mathf.RoundToInt(kv.Value * 100f) + "%");
        sb.AppendLine();
        sb.AppendLine("cobertura do traçado=" + Mathf.RoundToInt(pathCoverage * 100f) + "%"
            + "  camo=" + Mathf.RoundToInt(camoCoverage * 100f) + "%"
            + "  sobreposição média=" + averageOverlap.ToString("0.0")
            + "  buracos=" + uncoveredStretches);
        var lista = new List<string>();
        foreach (var k in affordableKinds) lista.Add(k.ToString());
        sb.Append("pode comprar agora: " + (lista.Count > 0 ? string.Join(", ", lista.ToArray()) : "nada"));
        return sb.ToString();
    }
}

// O "olho": lê a defesa do jogador ao fim de cada rodada e guarda o retrato.
//
// NESTA ETAPA ELE SÓ OBSERVA. Não altera regra nenhuma, não gasta, não decide — igual ao
// PlaytestLogger. Isso é de propósito: dá para validar as leituras contra partidas reais
// antes de existir qualquer adversário que dependa delas.
public class DefenseReadout : MonoBehaviour
{
    [SerializeField] private bool logarNoConsole = true;

    // Quantos pontos amostrar ao longo do traçado. 120 dá resolução de sobra em qualquer das
    // 5 fases sem custar nada: isto roda uma vez por rodada, não por frame.
    private const int AmostrasDoTracado = 120;

    public static DefenseReadout main;
    private DefenseSnapshot ultimo;
    public DefenseSnapshot Ultimo => ultimo;

    // Histórico das últimas rodadas. Existe para o CounterCommander poder decidir olhando a
    // defesa de DUAS rodadas atrás em vez da atual: reagir ao que o jogador acabou de comprar
    // pune quem acertou a resposta, que é o pior tipo de dificuldade adaptativa.
    private readonly List<DefenseSnapshot> historico = new List<DefenseSnapshot>();
    private const int MaximoNoHistorico = 8;

    private void Awake() => main = this;

    private void OnEnable() => EnemySpawner.onWaveComplete.AddListener(Capturar);
    private void OnDisable() => EnemySpawner.onWaveComplete.RemoveListener(Capturar);

    public void Capturar()
    {
        ultimo = Ler();
        if (ultimo != null)
        {
            historico.Add(ultimo);
            if (historico.Count > MaximoNoHistorico) historico.RemoveAt(0);
            if (logarNoConsole) Debug.Log(ultimo.Describe());
        }
    }

    // Retrato de N rodadas atrás (0 = o mais recente). Se ainda não houver histórico suficiente
    // — começo de partida —, devolve o mais antigo que existir, nunca nulo por falta de dado.
    public DefenseSnapshot Antigo(int rodadasAtras)
    {
        if (historico.Count == 0) return Ler();
        int i = historico.Count - 1 - Mathf.Max(0, rodadasAtras);
        return historico[Mathf.Clamp(i, 0, historico.Count - 1)];
    }

    public DefenseSnapshot Ler()
    {
        var s = new DefenseSnapshot();
        if (LevelManager.main == null) return s;

        s.wave = EnemySpawner.main != null ? EnemySpawner.main.CurrentWave : 0;
        s.currency = LevelManager.main.currency;

        var torres = new List<TowerBase>();
        foreach (var t in FindObjectsOfType<TowerBase>()) torres.Add(t);
        s.towerCount = torres.Count;

        // --- investimento por tipo de dano ---
        var porTipo = new Dictionary<DamageKind, int>();
        foreach (DamageKind k in System.Enum.GetValues(typeof(DamageKind))) porTipo[k] = 0;

        foreach (var t in torres)
        {
            int investido = InvestimentoDe(t);
            s.investedTotal += investido;
            porTipo[KindOf(t)] += investido;
        }
        foreach (var kv in porTipo)
            s.investShare[kv.Key] = s.investedTotal > 0 ? (float)kv.Value / s.investedTotal : 0f;

        // --- cobertura do traçado ---
        var pontos = AmostrarTracado();
        if (pontos.Count > 0)
        {
            int cobertos = 0, comCamo = 0, buracos = 0, somaSobreposicao = 0;
            bool anteriorCoberto = true; // começa true para não contar o início como buraco

            foreach (var p in pontos)
            {
                int quantas = 0; bool veCamo = false;
                foreach (var t in torres)
                {
                    if (t.CurrentRange <= 0f) continue; // Gerador não cobre nada
                    if (IsoGrid.CellDistance(t.transform.position, p) > t.CurrentRange) continue;
                    quantas++;
                    if (t.CanSeeCamo) veCamo = true;
                }
                if (quantas > 0) { cobertos++; somaSobreposicao += quantas; }
                if (veCamo) comCamo++;
                if (quantas == 0 && anteriorCoberto) buracos++;
                anteriorCoberto = quantas > 0;
            }

            s.pathCoverage = (float)cobertos / pontos.Count;
            s.camoCoverage = (float)comCamo / pontos.Count;
            s.averageOverlap = cobertos > 0 ? (float)somaSobreposicao / cobertos : 0f;
            s.uncoveredStretches = buracos;
        }

        // --- espaço de resposta disponível (a regra de ouro) ---
        s.freePlots = ContarPlotsLivres();
        if (BuildManager.main != null && s.freePlots > 0)
        {
            foreach (var par in BuildManager.main.CatalogoResumido())
                if (par.Value <= s.currency) s.affordableKinds.Add(par.Key);
        }

        return s;
    }

    // Classificação central por tipo de componente.
    //
    // AVISO: torre nova que ninguém classificar aqui cai em Unknown DE PROPÓSITO, e Unknown
    // aparece no relatório. É para a lacuna gritar em vez de sumir — o defeito clássico deste
    // projeto é código que existe e nunca chega a lugar nenhum.
    public static DamageKind KindOf(TowerBase t)
    {
        if (t is SniperTurret) return DamageKind.Piercing;
        if (t is AoETurret) return t.HasAbility("napalm") ? DamageKind.Fire : DamageKind.Explosive;
        if (t is TeslaTurret) return DamageKind.Energy;
        if (t is IceTurret) return DamageKind.Control;
        if (t is DetectorTurret) return DamageKind.Support;
        if (t is FarmTower) return DamageKind.Economy;
        if (t is TachinhaTurret) return DamageKind.Sharp;
        if (t is MachineGunTurret) return DamageKind.Sharp;
        if (t is Turret) return DamageKind.Sharp;
        return DamageKind.Unknown;
    }

    private static int InvestimentoDe(TowerBase t)
    {
        var v = t.GetComponentInParent<TowerValue>();
        return v != null ? v.Invested : 0;
    }

    private static int ContarPlotsLivres()
    {
        int n = 0;
        foreach (var p in FindObjectsOfType<Plot>())
            if (p.towerObj == null && p.gameObject.activeInHierarchy) n++;
        return n;
    }

    // Pontos igualmente espaçados ao longo do traçado real, do ponto de entrada ao fim.
    private static List<Vector3> AmostrarTracado()
    {
        var saida = new List<Vector3>();
        var lm = LevelManager.main;
        if (lm == null || lm.path == null || lm.path.Length == 0) return saida;

        var vertices = new List<Vector3>();
        if (lm.startPoint != null) vertices.Add(lm.startPoint.position);
        foreach (var t in lm.path) if (t != null) vertices.Add(t.position);
        if (vertices.Count < 2) return saida;

        float total = 0f;
        for (int i = 1; i < vertices.Count; i++) total += Vector3.Distance(vertices[i - 1], vertices[i]);
        if (total <= 0f) return saida;

        float passo = total / AmostrasDoTracado;
        float restante = 0f;
        for (int i = 1; i < vertices.Count; i++)
        {
            Vector3 a = vertices[i - 1], b = vertices[i];
            float trecho = Vector3.Distance(a, b);
            if (trecho <= 0f) continue;
            for (float d = restante; d < trecho; d += passo)
                saida.Add(Vector3.Lerp(a, b, d / trecho));
            restante = passo - ((trecho - restante) % passo);
        }
        return saida;
    }
}
