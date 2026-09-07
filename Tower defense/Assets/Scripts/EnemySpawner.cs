using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private TMP_Text turnText;

    // ATENÇÃO: estes defaults têm que espelhar o que está calibrado na cena Game — a regra número
    // um daqui é que o valor serializado vence o default, e um default velho não quebra a cena
    // atual, mas faz QUALQUER FASE NOVA nascer com a curva errada e ninguém percebe.
    // Conferidos contra a cena em 2026-09-05; a calibragem está registrada no ROADMAP (2.2).
    [Header("Attributes")]
    [SerializeField] private int baseEnemies = 8;
    [SerializeField] private float enemiesPerSecond = 1f;
    [SerializeField] private float timeBetweenWaves = 5f;
    [SerializeField] private float difScalingFactor = 1f;   // linear; era 0,85 antes de be5dd44
    [SerializeField] private float enemiesPerSecondCap = 15f;
    [SerializeField] private int waveCompletionBonus = 35;  // dinheiro ganho ao completar a onda
    [SerializeField] private int maxRounds = 40;            // vitória ao completar esta rodada

    [Header("Enemy Unlock Settings")]
    [SerializeField] private List<EnemyUnlock> enemyUnlocks = new List<EnemyUnlock>();

    [System.Serializable]
    public class EnemyUnlock
    {
        public int prefabIndex;
        public int firstWave = 1;
        public float weight = 1f;
        [HideInInspector] public float accumulatedWeight;
    }

    public static UnityEvent onEnemyDestroy = new UnityEvent();
    public static UnityEvent onEnemySpawn = new UnityEvent();

    // Disparado ao completar uma rodada — economia por rodada (geradores, juros) se pendura aqui.
    public static UnityEvent onWaveComplete = new UnityEvent();

    private int currentWave = 1;
    private float timeSinceLastSpawn;
    private int enemiesAlive;
    private int enemiesLeftToSpawn;
    private float eps;
    private bool isSpawning = false;
    private bool waveActive = false;

    public bool IsWaveActive => waveActive;
    public int EnemiesAlive => enemiesAlive;
    public int CurrentWave => currentWave;
    public int MaxRounds => maxRounds;

    // Quanto da onda já saiu do portão, de 0 a 1. Existe para as JOGADAS DIRIGIDAS saberem a hora
    // de agir no meio da rodada (ver CommanderPlays): "metade da onda" é uma medida de progresso,
    // não de relógio — em rodada rápida o relógio mentiria.
    private int totalDoSpawnDaOnda;
    public float ProgressoDeSpawn => totalDoSpawnDaOnda <= 0
        ? 0f
        : Mathf.Clamp01((float)(totalDoSpawnDaOnda - enemiesLeftToSpawn) / totalDoSpawnDaOnda);

    // ───────── Recompensa sublinear no TAMANHO da onda ─────────
    //
    // O ADVERSÁRIO ESTAVA FINANCIANDO O JOGADOR. Cada inimigo morto paga, e desde que ele passou a
    // montar as ondas o orçamento dele compra 200+ unidades — então quanto mais ameaça ele punha
    // em campo, mais rico o jogador ficava, e mais folgada ficava a rodada seguinte. Medido numa
    // partida completa: receita prevista de $62k nas 40 rodadas, receita real de **$160.951**, com
    // rodadas individuais pagando +$34.345. É o laço que tornava a ameaça inofensiva por
    // construção — ela comprava a própria derrota.
    //
    // A correção não mexe no valor de nenhum inimigo: mexe em quantas vezes ele paga. Uma onda do
    // tamanho que a curva pediria paga cheio; uma onda inflada paga proporcionalmente menos por
    // cabeça, de modo que o TOTAL cresce sublinearmente com a contagem. Mandar mais gente continua
    // sendo mais ameaça — só deixa de ser mais dinheiro.
    [SerializeField, Range(0f, 1f)] private float expoenteDaRecompensa = 0.5f;
    [SerializeField, Range(0.1f, 1f)] private float recompensaMinima = 0.35f;

    private float fatorDeRecompensa = 1f;
    public float FatorDeRecompensa => fatorDeRecompensa;
    public int TotalDoSpawnDaOnda => totalDoSpawnDaOnda;

    private void RecalcularFatorDeRecompensa()
    {
        int normal = Mathf.Max(1, EnemiesPerWave());
        if (totalDoSpawnDaOnda <= normal) { fatorDeRecompensa = 1f; return; } // onda normal ou menor: paga cheio
        fatorDeRecompensa = Mathf.Clamp(
            Mathf.Pow((float)normal / totalDoSpawnDaOnda, expoenteDaRecompensa),
            recompensaMinima, 1f);
    }

    // Uma leva EXTRA no meio da onda, decidida e anunciada pelo adversário. Entra na mesma fila
    // fixa que o resto da rodada usa, então ela é consumida na ordem e obedece ao mesmo ritmo de
    // spawn — não é um segundo spawner correndo por fora.
    public void InjetarReforco(List<int> fila)
    {
        if (fila == null || fila.Count == 0 || !waveActive) return;

        if (scriptedQueue == null) scriptedQueue = new List<int>();
        scriptedQueue.AddRange(fila);
        enemiesLeftToSpawn += fila.Count;
        totalDoSpawnDaOnda += fila.Count;
        RecalcularFatorDeRecompensa(); // o reforço engorda a onda: a recompensa acompanha

        // A onda pode ter parado de spawnar mas ainda estar viva (inimigos em campo). Sem religar
        // isto, a leva injetada existiria na fila e nunca sairia — o defeito clássico daqui.
        isSpawning = true;
    }

    // Catálogo de inimigos, na ORDEM que o WaveScript usa como índice. Leitura apenas — quem
    // precisa disso é o CounterCommander, para saber o que existe para comprar.
    public IReadOnlyList<GameObject> EnemyPrefabs => enemyPrefabs;

    // A partir de que rodada cada inimigo é liberado. O adversário não pode comprar o que o
    // roteiro ainda não apresentou ao jogador: encontrar um tipo pela primeira vez já é
    // dificuldade suficiente, não precisa vir em massa e de surpresa.
    // Quantos inimigos a curva pediria numa dada rodada. Público para o CounterCommander poder
    // se comparar com a curva normal em vez de inventar a própria escala.
    public int EnemiesPerWaveFor(int wave)
        => Mathf.RoundToInt(baseEnemies * Mathf.Pow(Mathf.Max(1, wave), difScalingFactor));

    public int FirstWaveOf(int prefabIndex)
    {
        int menor = int.MaxValue;
        foreach (var u in enemyUnlocks)
            if (u.prefabIndex == prefabIndex && u.firstWave < menor) menor = u.firstWave;
        return menor == int.MaxValue ? int.MaxValue : menor;
    }

    // Multiplicador de vida dos inimigos definido pela fase (mapas mais difíceis endurecem a onda).
    private float enemyHealthMultiplier = 1f;
    public float EnemyHealthMultiplier => enemyHealthMultiplier;

    // VIDA QUE CRESCE COM A RODADA — e é LINEAR, não exponencial.
    //
    // Esta decisão foi revertida a pedido do Israel em 2026-09-05, depois de a medição mostrar
    // que nenhuma calibragem do lado do adversário alcançava o poder de fogo (50x de orçamento,
    // dano zero). O PLANO dizia "não escalar HP" desde o começo, e a objeção original continua
    // válida: o problema do Bloons é que a pergunta de toda rodada vira "tenho DPS?".
    //
    // Por isso LINEAR e modesto, não exponencial: com 0,06 a rodada 40 fica em 3,3x, o que dá
    // peso à ameaça sem transformar a partida numa corrida de dano puro. As imunidades, as
    // jogadas dirigidas e o acesso gradual continuam sendo o que faz a rodada perguntar; isto
    // só evita que a resposta seja sempre "já basta o que eu tenho".
    [SerializeField] private float vidaExtraPorRodada = 0.06f;

    public float MultiplicadorDeVidaDaRodada
        => 1f + Mathf.Max(0, currentWave - 1) * Mathf.Max(0f, vidaExtraPorRodada);

    // Chamado pelo StageLoader antes do primeiro Start.
    public void ConfigureStage(int rounds, float healthMultiplier)
    {
        maxRounds = rounds;
        enemyHealthMultiplier = Mathf.Max(0.1f, healthMultiplier);
        if (turnText != null) turnText.text = "Pronto p/ rodada " + currentWave + " / " + maxRounds;
    }

    private bool autoStart = false;
    private bool autoScheduled = false;
    public bool AutoStart => autoStart;

    public void ToggleAutoStart()
    {
        autoStart = !autoStart;
        if (autoStart) ScheduleAuto();
    }

    private void ScheduleAuto()
    {
        if (autoStart && !waveActive && !autoScheduled)
            StartCoroutine(AutoNext());
    }

    private IEnumerator AutoNext()
    {
        autoScheduled = true;
        yield return new WaitForSeconds(timeBetweenWaves);
        autoScheduled = false;
        if (autoStart && !waveActive && (LevelManager.main == null || !LevelManager.main.isDead))
            StartWave();
    }

    private List<EnemyUnlock> availableEnemies = new List<EnemyUnlock>();
    private float totalWeight;

    public static EnemySpawner main;

    private void Awake()
    {
        main = this;

        onEnemyDestroy.AddListener(EnemyDestroyed);
        onEnemySpawn.AddListener(EnemySpawned);
        UpdateAvailableEnemies();

        if (turnText != null)
            turnText.text = "Pronto p/ rodada " + currentWave;
    }

    private void Update()
    {
        if (!isSpawning) return;

        timeSinceLastSpawn += Time.deltaTime;
        if (timeSinceLastSpawn >= (1f / eps) && enemiesLeftToSpawn > 0)
        {
            SpawnEnemy();
            enemiesLeftToSpawn--;
            timeSinceLastSpawn = 0f;
        }

        if (enemiesAlive == 0 && enemiesLeftToSpawn == 0)
        {
            EndWave();
        }
    }

    private void EnemySpawned()
    {
        enemiesAlive++;
    }

    private void EnemyDestroyed()
    {
        enemiesAlive = Mathf.Max(0, enemiesAlive - 1);
    }

    public void StartWaveButton()
    {
        if (waveActive) return;
        StartWave();
    }

    private void StartWave()
    {
        UpdateAvailableEnemies();

        // Rodadas marcantes têm composição fixa, escalada para o tamanho que a curva pediria —
        // senão uma rodada roteirizada acabaria mais fraca que uma sorteada do mesmo número.
        scriptedQueue = WaveScript.BuildQueue(currentWave, enemyPrefabs.Length, EnemiesPerWave());
        scriptedIndex = 0;

        // Rodada NÃO roteirizada: o Contra-Comandante monta a onda lendo a defesa do jogador.
        // As roteirizadas ficam intocadas de propósito — são as âncoras de ritmo e o lugar onde
        // cada tipo novo é apresentado. Se ele estiver em modo seco ou sem plano, devolve null e
        // a rodada cai no sorteio por peso de sempre.
        bool ondaDoAdversario = false;
        if (scriptedQueue == null && CounterCommander.main != null)
        {
            scriptedQueue = CounterCommander.main.BuildQueue(currentWave);
            ondaDoAdversario = scriptedQueue != null;
        }

        string manchete = WaveScript.Headline(currentWave);
        if (manchete == null && ondaDoAdversario) manchete = CounterCommander.main.MancheteDaOnda();
        turnText.text = manchete != null
            ? $"Rodada {currentWave} — {manchete}"
            : $"Rodada {currentWave} — em andamento";

        waveActive = true;
        isSpawning = true;
        enemiesLeftToSpawn = scriptedQueue != null ? scriptedQueue.Count : EnemiesPerWave();
        totalDoSpawnDaOnda = enemiesLeftToSpawn;
        RecalcularFatorDeRecompensa();
        eps = EnemiesPerSecond();

        // Reset contadores
        enemiesAlive = 0;
        timeSinceLastSpawn = 0f;
    }

    // Fila fixa da rodada roteirizada (null quando a rodada é sorteada).
    private List<int> scriptedQueue;
    private int scriptedIndex;

    private void EndWave()
    {
        isSpawning = false;
        waveActive = false;
        timeSinceLastSpawn = 0f;

        // Recompensa de fim de rodada (dinheiro escala com o número da onda)
        if (LevelManager.main != null)
        {
            LevelManager.main.IncreaseCurrency(waveCompletionBonus + currentWave);
        }

        onWaveComplete.Invoke(); // geradores rendem aqui

        // A rodada sobrevivida vale XP AGORA, não no fim da partida (ver PlayerProgress).
        // Abandonar no meio deixava o jogador sem nada, e era isso que o prendia no nível 1.
        PlayerProgress.CreditRound();

        currentWave++;

        // Vitória: sobreviveu a todas as rodadas
        if (maxRounds > 0 && currentWave > maxRounds)
        {
            if (turnText != null) turnText.text = "VITÓRIA!";
            if (LevelManager.main != null) LevelManager.main.Win();
            return;
        }

        UpdateAvailableEnemies();

        if (turnText != null)
            turnText.text = "Pronto p/ rodada " + currentWave + " / " + maxRounds;

        ScheduleAuto(); // se auto-início estiver ligado, agenda a próxima
    }

    // Reinicia a rodada atual (mantém o número da rodada), deixando o spawner num estado limpo.
    public void RestartCurrentWave()
    {
        StopAllCoroutines(); // um AutoNext pendente dispararia uma rodada no meio do reset
        autoScheduled = false;
        autoStart = false;
        scriptedQueue = null;
        scriptedIndex = 0;

        ClearEnemies();

        // Após a vitória currentWave passa de maxRounds; repetir dali encerraria a rodada na hora.
        if (maxRounds > 0 && currentWave > maxRounds) currentWave = maxRounds;

        enemiesAlive = 0;
        enemiesLeftToSpawn = 0;
        totalDoSpawnDaOnda = 0;
        timeSinceLastSpawn = 0f;
        isSpawning = false;
        waveActive = false;

        // As jogadas dirigidas morrem com a rodada que as anunciou: sem isto, o marcador de ALVO
        // e o reforço engatilhado sobreviveriam ao reinício e a torre continuaria caçada por uma
        // decisão que o jogador nunca mais veria anunciada.
        if (CommanderPlays.main != null) CommanderPlays.main.Encerrar();

        UpdateAvailableEnemies();
        if (turnText != null)
            turnText.text = "Pronto p/ rodada " + currentWave + " / " + maxRounds;
    }

    // Remove todos os inimigos em campo. Percorre os componentes (não a tag) para pegar também
    // os filhotes gerados por SpawnAfterDead, independentemente de como o prefab foi marcado.
    private void ClearEnemies()
    {
        foreach (EnemyMovement e in FindObjectsOfType<EnemyMovement>())
            Destroy(e.gameObject);
        enemiesAlive = 0;
    }

    private void UpdateAvailableEnemies()
    {
        availableEnemies.Clear();
        totalWeight = 0f;

        foreach (var unlock in enemyUnlocks)
        {
            if (currentWave >= unlock.firstWave)
            {
                availableEnemies.Add(unlock);
                unlock.accumulatedWeight = totalWeight + unlock.weight;
                totalWeight += unlock.weight;
            }
        }

        // Fallback caso ninguém esteja liberado
        if (availableEnemies.Count == 0 && enemyPrefabs.Length > 0)
        {
            availableEnemies.Add(new EnemyUnlock { prefabIndex = 0, weight = 1f });
            totalWeight = 1f;
        }
    }

    private void SpawnEnemy()
    {
        // Rodada roteirizada: consome a fila fixa, em ordem.
        if (scriptedQueue != null && scriptedIndex < scriptedQueue.Count)
        {
            int idx = scriptedQueue[scriptedIndex++];
            if (idx >= 0 && idx < enemyPrefabs.Length) { Spawn(idx); return; }
        }

        if (availableEnemies.Count == 0) return;

        float roll = Random.value * totalWeight;
        foreach (var unlock in availableEnemies)
        {
            if (roll <= unlock.accumulatedWeight) { Spawn(unlock.prefabIndex); return; }
        }

        Spawn(0); // Fallback
    }

    // Os prefabs vêm com sortingOrder fixo, que no tabuleiro isométrico deixaria o inimigo
    // escondido atrás do próprio caminho por onde ele está passando. O IsoSorter mantém a ordem
    // de desenho acompanhando a posição enquanto ele anda.
    private void Spawn(int prefabIndex)
    {
        GameObject go = Instantiate(enemyPrefabs[prefabIndex],
            LevelManager.main.startPoint.position, Quaternion.identity);

        // EnemyStatusFX ANTES do IsoSorter: cria os filhos MarkFX/FrostFX a tempo de o IsoSorter
        // já capturá-los no Init() e pulá-los (ver EnemyStatusFXLayer) — senão eles nasceriam
        // depois do snapshot e o IsoSorter nunca chegaria a vê-los (inofensivo, mas por garantia).
        EnemyStatusFX.Attach(go);
        IsoSorter.Attach(go, moves: true);
    }

    private int EnemiesPerWave()
    {
        return Mathf.RoundToInt(baseEnemies * Mathf.Pow(currentWave, difScalingFactor));
    }

    private float EnemiesPerSecond()
    {
        return Mathf.Clamp(
            enemiesPerSecond * Mathf.Pow(currentWave, difScalingFactor),
            0,
            enemiesPerSecondCap
        );
    }
}