using System.Reflection;
using UnityEngine;

// Bancada de verificação que roda sozinha em Play Mode e escreve o resultado no console.
//
// POR QUE EXISTE. As correções visuais (anel de alcance preso, torre piscando) só valem se for
// possível PROVAR que chegaram à tela — regra deste projeto. A prova vinha sendo feita executando
// código no Editor sob demanda; quando essa via não está disponível, o console é o único canal de
// volta. Então o teste se monta, se mede e se relata sozinho.
//
// FERRAMENTA DE DIAGNÓSTICO, não conteúdo: com `ativo` desligado ela não faz absolutamente nada.
public class AutoTeste : MonoBehaviour
{
    [SerializeField] private bool ativo = true;

    // 0 = partida COMPLETA do início, com auto-início de rodada. É o modo para medir economia:
    // o PlaytestLogger grava o CSV normalmente e dá para comparar com uma partida humana.
    // Qualquer outro valor pula direto para aquela rodada (modo cenário).
    [SerializeField] private int rodadaParaForcar = 0;
    [SerializeField] private float velocidade = 12f;
    [SerializeField] private float intervaloDoRelatorio = 15f;
    [SerializeField] private bool anexarProbes = true;

    // Relata FPS e contagem de inimigos periodicamente — o suficiente para achar o joelho da
    // curva de performance sem depender do profiler externo.
    [SerializeField] private bool medirPerformance = false;
    private float piorFrame;
    private int inimigosNoPiorFrame;

    private float proximoRelatorio;
    private bool montado;

    // O XP é PlayerPrefs, ou seja, o save de verdade do jogador. Um teste que roda 30 rodadas
    // sobe o nível de comandante dele de brinde e falsifica a própria coisa que estamos
    // avaliando (o nível trava torres e tiers). Guarda antes, devolve depois.
    private int xpAntesDoTeste;
    private bool xpGuardado;

    // VARREDURA DE INTENSIDADE. Testa vários multiplicadores do orçamento do adversário contra a
    // MESMA defesa, na mesma sessão — a única forma de comparar sem que a defesa mude junto e
    // contamine o resultado. Rodar uma partida inteira por multiplicador levaria 7 minutos cada.
    [SerializeField] private bool varrerIntensidade = false;

    // TEM QUE SER UMA RODADA LIVRE. Nas roteirizadas (WaveScript) o Contra-Comandante nem é
    // chamado, então mexer no orçamento dele não muda nada — a primeira varredura rodou na 30 e
    // deu onda idêntica em todos os multiplicadores, inclusive x50. Livres: 4, 6, 8, 18, 19, 21,
    // 24, 26, 28, 29, 31, 32, 34, 36, 37, 39.
    [SerializeField] private int rodadaDaVarredura = 31;
    [SerializeField] private float[] multiplicadores = { 1f, 3f, 8f, 20f, 50f };

    private void Start()
    {
        if (!ativo) return;
        Invoke("Montar", 0.5f); // dá um frame para LevelManager/BuildManager terminarem o Awake
    }

    // Varre o FATOR GLOBAL DE DANO das torres, não o orçamento do adversário: medido, o lado da
    // ameaça satura (50x de orçamento, dano zero) porque o teto de vida por onda é estrutural.
    // Quem tem folga para dar é o poder de fogo.
    private System.Collections.IEnumerator Varrer()
    {
        float original = TowerBase.FatorGlobalDeDano;

        FieldInfo waveField = typeof(EnemySpawner).GetField("currentWave",
            BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (float m in multiplicadores)
        {
            TowerBase.FatorGlobalDeDano = m;

            // Vida cheia a cada teste: senão o dano acumulado do teste anterior mata o jogador no
            // meio da varredura e os multiplicadores seguintes medem uma partida já perdida.
            LevelManager.main.playerHP = 150;
            LevelManager.main.IncreaseCurrency(50000);
            if (waveField != null) waveField.SetValue(EnemySpawner.main, rodadaDaVarredura);

            int vidaAntes = LevelManager.main.playerHP;
            EnemySpawner.main.StartWaveButton();
            yield return null;
            // O tamanho tem que ser lido AGORA: depois que a onda acaba o contador já foi
            // reciclado, e a primeira varredura reportou "onda=8" numa rodada 30.
            int tamanhoDaOnda = EnemySpawner.main.TotalDoSpawnDaOnda;
            while (EnemySpawner.main.IsWaveActive) yield return null;

            ThreatProbe tp = ThreatProbe.main;
            Debug.Log("[VARREDURA] danoDasTorres x" + m.ToString("0.##")
                + "  dano_sofrido=" + (vidaAntes - LevelManager.main.playerHP)
                + "  onda=" + tamanhoDaOnda
                + "  " + (tp != null ? tp.Resumo() : ""));

            yield return new WaitForSeconds(0.5f);
        }
        TowerBase.FatorGlobalDeDano = original;
        Debug.Log("[VARREDURA] fim — fator de dano devolvido a " + original);
    }

    private void Montar()
    {
        if (LevelManager.main == null || BuildManager.main == null)
        {
            Debug.LogWarning("[AutoTeste] cena sem LevelManager/BuildManager — nada a fazer.");
            return;
        }

        // QUANTO DO HEAP É LIXO E QUANTO ESTÁ VIVO.
        //
        // O profiler acusa ~644 MB de heap gerenciado, e é ele que faz cada coleta travar
        // segundos. Mas heap grande pode ser duas coisas MUITO diferentes: lixo acumulado (que uma
        // coleta forçada devolve) ou objetos ainda referenciados (vazamento de verdade). A conta
        // abaixo separa as duas em uma linha — sem ela eu continuaria otimizando no escuro.
        long heapAntes = System.GC.GetTotalMemory(false);
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
        long heapDepois = System.GC.GetTotalMemory(true);
        Debug.Log("[GC] heap antes=" + (heapAntes / 1048576) + "MB  depois da coleta="
            + (heapDepois / 1048576) + "MB  (se continuar alto, é objeto VIVO, não lixo)");

        xpAntesDoTeste = PlayerProgress.TotalXP;
        xpGuardado = true;
        Debug.Log("[AutoTeste] XP antes do teste: " + xpAntesDoTeste
            + " (nivel " + PlayerProgress.Level + ") — sera restaurado ao sair do Play");

        Unlocks.LiberarTudo = true;

        GameObject go = LevelManager.main.gameObject;
        if (go.GetComponent<TestPilot>() == null) go.AddComponent<TestPilot>();

        // Os probes varrem a cena várias vezes por segundo. Para medir PERFORMANCE eles têm que
        // sair: instrumento que pesa falseia a medida do peso.
        if (anexarProbes)
        {
            if (go.GetComponent<FlickerProbe>() == null) go.AddComponent<FlickerProbe>();
            if (go.GetComponent<ThreatProbe>() == null) go.AddComponent<ThreatProbe>();
        }

        // DINHEIRO DE GRAÇA SÓ NO MODO CENÁRIO. Numa partida completa ele destruiria justamente
        // a medida que se quer tirar dela — a receita é o objeto do teste, não um meio.
        if (rodadaParaForcar > 0)
        {
            LevelManager.main.IncreaseCurrency(300000);
            for (int i = 0; i < 5; i++) EnemySpawner.onWaveComplete.Invoke();
            VerificarAnelDeAlcance();
        }

        // A Game View precisa estar VISÍVEL, senão o Play Mode roda a menos de 1 FPS e a partida
        // não avança (armadilha medida em 2026-09-04; foco da janela não resolve).
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Game");
#endif
        QualitySettings.vSyncCount = 0;
        Time.timeScale = velocidade;

        if (rodadaParaForcar > 0)
        {
            FieldInfo f = typeof(EnemySpawner).GetField("currentWave",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(EnemySpawner.main, rodadaParaForcar);
        }

        if (varrerIntensidade)
        {
            // Monta uma defesa forte primeiro: a varredura precisa medir contra o jogador no
            // fim de partida, que é onde a folga é maior.
            LevelManager.main.IncreaseCurrency(300000);
            for (int i = 0; i < 12; i++) EnemySpawner.onWaveComplete.Invoke();
            Debug.Log("[VARREDURA] defesa montada: " + ContarTorres() + " torres");
            StartCoroutine(Varrer());
            montado = true;
            proximoRelatorio = Time.time + intervaloDoRelatorio;
            return;
        }

        if (!EnemySpawner.main.AutoStart) EnemySpawner.main.ToggleAutoStart();
        EnemySpawner.main.StartWaveButton();

        montado = true;
        proximoRelatorio = Time.time + intervaloDoRelatorio;
        Debug.Log("[AutoTeste] montado: rodada " + EnemySpawner.main.CurrentWave
            + ", torres=" + ContarTorres());
    }

    // O bug era: clicar numa segunda torre trocava a referência do painel sem mandar a primeira
    // esconder o anel, e ele ficava preso na tela para sempre. Aqui a sequência é reproduzida
    // sem mouse — abre o painel de uma torre, depois de outra, e pergunta à primeira se o anel
    // dela sumiu.
    private void VerificarAnelDeAlcance()
    {
        // A Farm NÃO entra: o ShowRange dela é vazio de propósito (ela não tem alcance), então
        // ela nunca acende anel e faria o teste acusar falha onde não há.
        TowerBase a = null, b = null;
        foreach (TowerBase t in FindObjectsOfType<TowerBase>())
        {
            if (t is FarmTower) continue;
            if (a == null) { a = t; continue; }
            if (b == null && t.transform.root != a.transform.root) { b = t; break; }
        }
        if (a == null || b == null || UIManager.main == null)
        {
            Debug.LogWarning("[AutoTeste] anel: torres insuficientes para testar.");
            return;
        }

        a.OpenUpgradeUI();
        bool anelDeAVisivelAntes = AnelVisivel(a);
        b.OpenUpgradeUI();
        bool anelDeADepois = AnelVisivel(a);
        bool anelDeBDepois = AnelVisivel(b);

        Debug.Log("[AutoTeste] ANEL DE ALCANCE -> A visivel apos abrir A: " + anelDeAVisivelAntes
            + " | A visivel apos abrir B: " + anelDeADepois
            + " | B visivel: " + anelDeBDepois
            + "  ==> " + (anelDeAVisivelAntes && !anelDeADepois && anelDeBDepois
                ? "OK (a anterior foi deselecionada)"
                : "FALHOU"));

        UIManager.main.HideUpgradeUI();
    }

    private static bool AnelVisivel(TowerBase t)
    {
        foreach (RangeIndicator ri in t.GetComponentsInChildren<RangeIndicator>(true))
            if (ri.gameObject.activeSelf) return true;
        return false;
    }

    // A composição da última onda, numa linha. O Describe() do CounterCommander é multilinha e o
    // console só mostra a primeira linha na listagem.
    private static string ResumoDoPlano()
    {
        if (CounterCommander.main == null || CounterCommander.main.Ultimo == null) return "(sem plano)";
        CounterPlan p = CounterCommander.main.Ultimo;

        var sb = new System.Text.StringBuilder();
        sb.Append("rodada ").Append(p.wave).Append(" orcamento=").Append(p.orcamento)
          .Append(" gasto=").Append(p.gasto).Append(" | ");
        foreach (PlanItem it in p.itens) sb.Append(it.quantidade).Append("x ").Append(it.nome).Append("  ");
        sb.Append("| jogadas: ");
        if (p.jogadas.Count == 0) sb.Append("nenhuma");
        foreach (CommanderPlay j in p.jogadas) sb.Append(j.nome).Append(" ");
        return sb.ToString();
    }

    private void OnDisable()
    {
        if (!xpGuardado) return;
        PlayerPrefs.SetInt("Progress_XP", xpAntesDoTeste);
        PlayerPrefs.Save();
        Debug.Log("[AutoTeste] XP restaurado para " + xpAntesDoTeste);
    }

    // CENSO DE OBJETOS. O profiler acusou ~4.590 objetos de cena com só 50 inimigos e 10 torres
    // em campo — seis a nove vezes o que a conta de cabeça dá. Ou existe vazamento, ou algum
    // sistema cria muito mais filhos do que parece. Contar por nome responde qual dos dois em uma
    // linha, o que nenhuma leitura de código responderia com a mesma certeza.
    private static void Censo()
    {
        var porNome = new System.Collections.Generic.Dictionary<string, int>();
        foreach (GameObject go in FindObjectsOfType<GameObject>())
        {
            // Agrupa "Enemy(Clone)", "Enemy(Clone) (1)" etc. sob o mesmo rótulo.
            string nome = go.name;
            int corte = nome.IndexOf('(');
            if (corte > 0) nome = nome.Substring(0, corte);
            nome = nome.Trim();

            int n;
            porNome[nome] = porNome.TryGetValue(nome, out n) ? n + 1 : 1;
        }

        var lista = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(porNome);
        lista.Sort(delegate (System.Collections.Generic.KeyValuePair<string, int> a,
                             System.Collections.Generic.KeyValuePair<string, int> b)
        { return b.Value.CompareTo(a.Value); });

        var sb = new System.Text.StringBuilder("[CENSO] total=");
        int total = 0;
        foreach (var kv in lista) total += kv.Value;
        sb.Append(total).Append(" | ");
        for (int i = 0; i < lista.Count && i < 12; i++)
            sb.Append(lista[i].Key).Append("=").Append(lista[i].Value).Append("  ");
        Debug.Log(sb.ToString());
    }

    private static int ContarTorres()
    {
        var raizes = new System.Collections.Generic.List<Transform>();
        foreach (TowerBase t in FindObjectsOfType<TowerBase>())
            if (!raizes.Contains(t.transform.root)) raizes.Add(t.transform.root);
        return raizes.Count;
    }

    private void Update()
    {
        if (medirPerformance && montado)
        {
            // unscaled: o timeScale acelera o jogo, não a máquina. Medir o frame real é o ponto.
            float ms = Time.unscaledDeltaTime * 1000f;
            if (ms > piorFrame)
            {
                piorFrame = ms;
                inimigosNoPiorFrame = FindObjectsOfType<EnemyMovement>().Length;
            }
        }

        if (!ativo || !montado || Time.time < proximoRelatorio) return;
        proximoRelatorio = Time.time + intervaloDoRelatorio;

        FlickerProbe p = FlickerProbe.main;
        if (p != null) Debug.Log("[AutoTeste] PISCAR " + p.ResumoNumaLinha());

        Debug.Log("[AutoTeste] XP: total=" + PlayerProgress.TotalXP
            + " ganho_no_teste=" + (PlayerProgress.TotalXP - xpAntesDoTeste)
            + " nivel=" + PlayerProgress.Level
            + "  (rodada " + EnemySpawner.main.CurrentWave + ")");

        Debug.Log("[AutoTeste] COMPOSICAO " + ResumoDoPlano());

        Debug.Log("[AutoTeste] RECOMPENSA fator=" + EnemySpawner.main.FatorDeRecompensa.ToString("0.000")
            + "  onda_atual=" + EnemySpawner.main.TotalDoSpawnDaOnda
            + "  curva_pediria=" + EnemySpawner.main.EnemiesPerWaveFor(EnemySpawner.main.CurrentWave)
            + "  (rodada " + EnemySpawner.main.CurrentWave + ")");

        if (medirPerformance)
        {
            int vivos = FindObjectsOfType<EnemyMovement>().Length;
            // O heap acompanha o PERF: o que interessa não é o valor absoluto (o Boehm do Unity
            // não devolve memória ao sistema, então ele só sobe) e sim se ele CONTINUA subindo.
            // Heap estável = vazamento estancado; heap subindo = ainda vaza.
            Debug.Log("[PERF] heap=" + (System.GC.GetTotalMemory(false) / 1048576) + "MB"
                + "  inimigos=" + vivos
                + "  fps_agora=" + Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime))
                + "  frame_agora=" + (Time.unscaledDeltaTime * 1000f).ToString("0.0") + "ms"
                + "  PIOR_FRAME=" + piorFrame.ToString("0") + "ms com " + inimigosNoPiorFrame + " inimigos"
                + "  escudeiros=" + FindObjectsOfType<Shielder>().Length
                + "  torres=" + ContarTorres());
            piorFrame = 0f;
        }

        if (medirPerformance) Censo();

        Debug.Log("[AutoTeste] estado: rodada " + EnemySpawner.main.CurrentWave
            + " vida=" + LevelManager.main.playerHP
            + " inimigos=" + FindObjectsOfType<EnemyMovement>().Length
            + " fps~" + Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)));
    }
}
