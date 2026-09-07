using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Mede QUANTO a altura das torres esconde do tabuleiro durante uma partida.
//
// Existe por causa de uma pergunta concreta: depois que as pilhas pararam de ficar enterradas
// (ver a regra do cache de ordenação no PLANO.md), uma torre A3 passou a ter ~3,5 unidades de
// altura, o que na tela são ~7 fileiras de célula. A suspeita é que isso esconda inimigo e
// traçado. Suspeita não é medida — este componente é a medida.
//
// É OBSERVADOR PURO, no mesmo espírito do PlaytestLogger: não altera regra nenhuma, não desenha
// nada, não decide nada. Se for removido, o jogo se comporta igual.
//
// COMO ELE DECIDE QUE ALGO ESTÁ ESCONDIDO. No tabuleiro isométrico quem tapa o quê é a
// sortingOrder, derivada da célula (ver IsoGrid). Um ponto está oculto se existe um sprite de
// torre com ordem MAIOR que a do ponto e cujo retângulo o contém.
//
// Isso é um LIMITE SUPERIOR, e o número tem que ser lido sabendo disso: o retângulo do sprite
// tem cantos transparentes (o bloco isométrico é um hexágono dentro dele), então uma fração dos
// pontos contados como ocultos na verdade aparece por um canto. Testar o alpha real exigiria
// textura marcada como legível, o que mudaria o import de todo o pacote de arte só para medir.
// O viés é conhecido e é sempre para o mesmo lado — se o limite superior já for pequeno, a
// pergunta está respondida.
public class OcclusionProbe : MonoBehaviour
{
    [SerializeField] private bool ativo = true;

    // 5 amostras por segundo: o suficiente para pegar um inimigo atravessando uma torre sem
    // custar nada. Isto roda algumas vezes por segundo, não por frame.
    [SerializeField] private float amostrasPorSegundo = 5f;

    private const int AmostrasDoTracado = 120; // mesma resolução que o DefenseReadout usa

    private class Rodada
    {
        public int numero;
        public int amostrasDeInimigo;
        public int amostrasOcultas;
        public float piorSumico;        // maior tempo CONTÍNUO que um inimigo passou escondido
        public float somaDosSumicos;
        public int quantidadeDeSumicos;
        public float tracadoOculto;     // fração do traçado coberta por torre
        public int torres;
        public float alturaMedia;       // altura média das torres, em unidades de mundo
        public float tierMedio;
    }

    private readonly List<Rodada> historico = new List<Rodada>();
    private Rodada atual;
    private float proximaAmostra;

    // instanceID do inimigo -> instante em que ele começou a ficar escondido (0 = visível)
    private readonly Dictionary<int, float> sumindoDesde = new Dictionary<int, float>();

    public static OcclusionProbe main;

    private void Awake() => main = this;

    private void OnEnable() => EnemySpawner.onWaveComplete.AddListener(FecharRodada);
    private void OnDisable() => EnemySpawner.onWaveComplete.RemoveListener(FecharRodada);

    private void Update()
    {
        if (!ativo || EnemySpawner.main == null) return;
        if (!EnemySpawner.main.IsWaveActive) return;

        if (Time.time < proximaAmostra) return;
        proximaAmostra = Time.time + 1f / Mathf.Max(0.5f, amostrasPorSegundo);

        if (atual == null || atual.numero != EnemySpawner.main.CurrentWave)
        {
            atual = new Rodada();
            atual.numero = EnemySpawner.main.CurrentWave;
            sumindoDesde.Clear();
        }

        List<SpriteRenderer> spritesDeTorre = ColetarSpritesDeTorre();

        // Uma varredura só de inimigos por amostra. A primeira versão fazia FindObjectsOfType
        // DENTRO do laço que fecha os sumiços em aberto, o que é O(n²) por amostra: com a onda
        // cheia a partida travava e a medição virava a coisa mais cara em campo.
        EnemyMovement[] vivos = FindObjectsOfType<EnemyMovement>();
        vivosAgora.Clear();

        for (int i = 0; i < vivos.Length; i++)
        {
            EnemyMovement e = vivos[i];
            // O centro do CORPO, não a origem: a origem do UFO é o pé dele, e um pé escondido
            // atrás de um bloco enquanto o corpo aparece inteiro não é o inimigo sumindo.
            Vector3 ponto = CentroVisual(e.gameObject);
            atual.amostrasDeInimigo++;

            int id = e.gameObject.GetInstanceID();
            vivosAgora.Add(id);

            if (Oculto(ponto, spritesDeTorre))
            {
                atual.amostrasOcultas++;
                if (!sumindoDesde.ContainsKey(id)) sumindoDesde[id] = Time.time;
            }
            else if (sumindoDesde.ContainsKey(id))
            {
                RegistrarSumico(Time.time - sumindoDesde[id]);
                sumindoDesde.Remove(id);
            }
        }

        // Inimigo que morreu escondido nunca "reaparece": sem isto o sumiço dele ficaria aberto
        // para sempre e não entraria na conta justamente no caso mais grave.
        mortos.Clear();
        foreach (var kv in sumindoDesde)
            if (!vivosAgora.Contains(kv.Key)) mortos.Add(kv.Key);
        for (int i = 0; i < mortos.Count; i++)
        {
            RegistrarSumico(Time.time - sumindoDesde[mortos[i]]);
            sumindoDesde.Remove(mortos[i]);
        }
    }

    private readonly HashSet<int> vivosAgora = new HashSet<int>();
    private readonly List<int> mortos = new List<int>();

    private void RegistrarSumico(float duracao)
    {
        if (atual == null || duracao <= 0f) return;
        atual.somaDosSumicos += duracao;
        atual.quantidadeDeSumicos++;
        if (duracao > atual.piorSumico) atual.piorSumico = duracao;
    }

    private void FecharRodada()
    {
        if (!ativo || atual == null) return;

        // Fecha os sumiços ainda abertos, senão a rodada perde justamente os inimigos que
        // atravessaram escondidos até o fim.
        var abertos = new List<int>(sumindoDesde.Keys);
        foreach (int id in abertos) RegistrarSumico(Time.time - sumindoDesde[id]);
        sumindoDesde.Clear();

        List<SpriteRenderer> spritesDeTorre = ColetarSpritesDeTorre();
        List<Vector3> pontos = AmostrarTracado();
        int cobertos = 0;
        foreach (Vector3 p in pontos) if (Oculto(p, spritesDeTorre)) cobertos++;
        atual.tracadoOculto = pontos.Count > 0 ? (float)cobertos / pontos.Count : 0f;

        float somaAltura = 0f, somaTier = 0f;
        var raizes = new List<Transform>();
        foreach (TowerBase t in FindObjectsOfType<TowerBase>())
        {
            Transform raiz = t.transform.root;
            if (raizes.Contains(raiz)) continue;
            raizes.Add(raiz);
            somaAltura += AlturaDe(raiz);
            somaTier += t.PathLevel(0) + t.PathLevel(1);
        }
        atual.torres = raizes.Count;
        atual.alturaMedia = raizes.Count > 0 ? somaAltura / raizes.Count : 0f;
        atual.tierMedio = raizes.Count > 0 ? somaTier / raizes.Count : 0f;

        historico.Add(atual);
        atual = null;
    }

    // ───────── o teste de oclusão ─────────

    private static List<SpriteRenderer> ColetarSpritesDeTorre()
    {
        var saida = new List<SpriteRenderer>();
        foreach (TowerBase t in FindObjectsOfType<TowerBase>())
        {
            foreach (SpriteRenderer sr in t.transform.root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!sr.enabled || sr.sprite == null) continue;
                if (sr.GetComponent<RangeIndicator>() != null) continue; // translúcido, não esconde
                saida.Add(sr);
            }
        }
        return saida;
    }

    private static bool Oculto(Vector3 ponto, List<SpriteRenderer> spritesDeTorre)
    {
        Vector3 origem = IsoBoard.main != null ? IsoBoard.main.Origin : Vector3.zero;
        int ordemDoPonto = IsoGrid.SortingOrderAt(ponto, origem);

        for (int i = 0; i < spritesDeTorre.Count; i++)
        {
            SpriteRenderer sr = spritesDeTorre[i];
            if (sr == null) continue;
            if (sr.sortingOrder <= ordemDoPonto) continue; // desenhado antes: não tapa

            Bounds b = sr.bounds;
            if (ponto.x < b.min.x || ponto.x > b.max.x) continue;
            if (ponto.y < b.min.y || ponto.y > b.max.y) continue;
            return true;
        }
        return false;
    }

    private static Vector3 CentroVisual(GameObject go)
    {
        bool primeiro = true;
        Bounds b = new Bounds();
        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!sr.enabled || sr.sprite == null) continue;
            if (primeiro) { b = sr.bounds; primeiro = false; } else b.Encapsulate(sr.bounds);
        }
        return primeiro ? go.transform.position : b.center;
    }

    private static float AlturaDe(Transform raiz)
    {
        bool primeiro = true;
        Bounds b = new Bounds();
        foreach (SpriteRenderer sr in raiz.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!sr.enabled || sr.sprite == null) continue;
            if (sr.GetComponent<RangeIndicator>() != null) continue;
            if (primeiro) { b = sr.bounds; primeiro = false; } else b.Encapsulate(sr.bounds);
        }
        return primeiro ? 0f : b.size.y;
    }

    private static List<Vector3> AmostrarTracado()
    {
        var saida = new List<Vector3>();
        LevelManager lm = LevelManager.main;
        if (lm == null || lm.path == null || lm.path.Length == 0) return saida;

        var vertices = new List<Vector3>();
        if (lm.startPoint != null) vertices.Add(lm.startPoint.position);
        foreach (Transform t in lm.path) if (t != null) vertices.Add(t.position);
        if (vertices.Count < 2) return saida;

        float total = 0f;
        for (int i = 1; i < vertices.Count; i++) total += Vector3.Distance(vertices[i - 1], vertices[i]);
        if (total <= 0f) return saida;

        float passo = total / AmostrasDoTracado;
        for (int i = 1; i < vertices.Count; i++)
        {
            Vector3 a = vertices[i - 1], b = vertices[i];
            float trecho = Vector3.Distance(a, b);
            for (float d = 0f; d < trecho; d += passo) saida.Add(Vector3.Lerp(a, b, d / trecho));
        }
        return saida;
    }

    // ───────── leitura ─────────

    // InvariantCulture no decimal, senão a máquina em pt-BR escreve "2,85" e a vírgula decimal
    // colide com a vírgula que separa as colunas — o CSV sai com 12 campos onde deveria ter 8.
    private static readonly System.Globalization.CultureInfo Inv =
        System.Globalization.CultureInfo.InvariantCulture;

    public string Relatorio()
    {
        var sb = new StringBuilder();
        sb.AppendLine("rodada,torres,tier_medio,altura_media,inimigo_oculto_%,tracado_oculto_%,sumico_medio_s,pior_sumico_s");
        foreach (Rodada r in historico)
        {
            float fr = r.amostrasDeInimigo > 0 ? (float)r.amostrasOcultas / r.amostrasDeInimigo : 0f;
            float medio = r.quantidadeDeSumicos > 0 ? r.somaDosSumicos / r.quantidadeDeSumicos : 0f;
            sb.AppendLine(r.numero
                + "," + r.torres
                + "," + r.tierMedio.ToString("0.0", Inv)
                + "," + r.alturaMedia.ToString("0.00", Inv)
                + "," + Mathf.RoundToInt(fr * 100f)
                + "," + Mathf.RoundToInt(r.tracadoOculto * 100f)
                + "," + medio.ToString("0.00", Inv)
                + "," + r.piorSumico.ToString("0.00", Inv));
        }
        return sb.ToString();
    }

    public int RodadasMedidas => historico.Count;

    // Fração do traçado escondida atrás de torre, AGORA, sem esperar a rodada fechar.
    //
    // É a medida geométrica pura — não depende de onde os inimigos morreram, que é o que faz a
    // coluna de "inimigo oculto" oscilar de rodada para rodada. Serve para varrer valores de
    // altura contra a MESMA defesa, que é a única comparação A/B honesta: refazer a partida
    // inteira a cada valor traria uma defesa diferente junto e misturaria as duas causas.
    public float TracadoOcultoAgora()
    {
        List<SpriteRenderer> spritesDeTorre = ColetarSpritesDeTorre();
        List<Vector3> pontos = AmostrarTracado();
        if (pontos.Count == 0) return 0f;
        int cobertos = 0;
        foreach (Vector3 p in pontos) if (Oculto(p, spritesDeTorre)) cobertos++;
        return (float)cobertos / pontos.Count;
    }

    // Maior trecho CONTÍNUO do traçado escondido, em segundos de caminhada.
    // Uma oclusão de 30% espalhada em vinte pedacinhos é irritante; concentrada num trecho só é
    // o inimigo sumindo do mapa. Os dois casos pedem respostas diferentes, então medem separado.
    public float MaiorSumicoEmSegundos(float velocidade)
    {
        List<SpriteRenderer> spritesDeTorre = ColetarSpritesDeTorre();
        List<Vector3> pontos = AmostrarTracado();
        if (pontos.Count < 2 || velocidade <= 0f) return 0f;

        float maior = 0f, atualLen = 0f;
        for (int i = 1; i < pontos.Count; i++)
        {
            float passo = Vector3.Distance(pontos[i - 1], pontos[i]);
            if (Oculto(pontos[i], spritesDeTorre))
            {
                atualLen += passo;
                if (atualLen > maior) maior = atualLen;
            }
            else atualLen = 0f;
        }
        return maior / velocidade;
    }
}
