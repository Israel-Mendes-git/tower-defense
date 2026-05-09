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

    private int currentWave = 1;
    private float timeSinceLastSpawn;
    private int enemiesAlive;
    private int enemiesLeftToSpawn;
    private float eps;
    private bool isSpawning = false;
    private bool waveActive = false;

    private List<EnemyUnlock> availableEnemies = new List<EnemyUnlock>();
    private float totalWeight;

    public static EnemySpawner main;

    private void Awake()
    {
        main = this;

        onEnemyDestroy.AddListener(EnemyDestroyed);
        onEnemySpawn.AddListener(EnemySpawned);
        UpdateAvailableEnemies();
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
        turnText.text = "Turno: " + currentWave.ToString();
        waveActive = true;
        isSpawning = true;
        enemiesLeftToSpawn = EnemiesPerWave();
        eps = EnemiesPerSecond();

        // Reset contadores
        enemiesAlive = 0;
        timeSinceLastSpawn = 0f;

        UpdateAvailableEnemies();
    }

    private void EndWave()
    {
        isSpawning = false;
        waveActive = false;
        timeSinceLastSpawn = 0f;
        currentWave++;
        UpdateAvailableEnemies();
    }

    // Novo método: Reinicia a wave atual (mantém o número da wave)
    public void RestartCurrentWave()
    {
        // Mata todos os inimigos vivos (opcional, mas recomendado)
        KillAllEnemies();

        // Reseta contadores
        enemiesAlive = 0;
        timeSinceLastSpawn = 0f;
        isSpawning = false;
        waveActive = false;

    }

    // Método auxiliar para matar todos os inimigos vivos
    private void KillAllEnemies()
    {
        // Encontra todos os objetos com tag "Enemy" e destrói
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in enemies)
        {
            Destroy(enemy);
        }
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
        if (availableEnemies.Count == 0) return;

        float roll = Random.value * totalWeight;
        foreach (var unlock in availableEnemies)
        {
            if (roll <= unlock.accumulatedWeight)
            {
                Instantiate(
                    enemyPrefabs[unlock.prefabIndex],
                    LevelManager.main.startPoint.position,
                    Quaternion.identity
                );
                return;
            }
        }

        // Fallback
        Instantiate(
            enemyPrefabs[0],
            LevelManager.main.startPoint.position,
            Quaternion.identity
        );
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