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

    [Header("Attributes")]
    [SerializeField] private int baseEnemies = 8;
    [SerializeField] private float enemiesPerSecond = 0.5f;
    [SerializeField] private float timeBetweenWaves = 5f;
    [SerializeField] private float difScalingFactor = 0.75f;
    [SerializeField] private float enemiesPerSecondCap = 15f;
    [SerializeField] private int waveCompletionBonus = 100; // dinheiro ganho ao completar a onda (estilo Bloons)
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
    public int CurrentWave => currentWave;
    public int MaxRounds => maxRounds;

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
        timeSinceLastSpawn = 0f;
        isSpawning = false;
        waveActive = false;

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