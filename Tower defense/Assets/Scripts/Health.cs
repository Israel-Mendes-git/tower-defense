using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("Attributes")]
    [SerializeField] private int hitPoints = 2;
    [SerializeField] private int currencyWorth = 50;

    private bool isDestroyed = false;
    private bool registered = false;

    private void OnEnable()
    {
        if (registered) return;
        registered = true;
        EnemySpawner.onEnemySpawn.Invoke();
    }

    public void TakeDamage(int dmg)
    {
        if (isDestroyed) return;

        hitPoints -= dmg;

        if (hitPoints <= 0)
        {
            isDestroyed = true;

            SpawnAfterDead spawnComponent = GetComponent<SpawnAfterDead>();
            if (spawnComponent != null)
            {
                spawnComponent.SpawnEnemiesOnDeath();
            }

            EnemySpawner.onEnemyDestroy.Invoke();
            LevelManager.main.IncreaseCurrency(currencyWorth);
            Destroy(gameObject);
        }
    }

}