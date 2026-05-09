using UnityEngine;

public class SpawnAfterDead : MonoBehaviour
{
    [SerializeField] private GameObject[] EnemiesToSpawn;

    public void SpawnEnemiesOnDeath()
    {
        foreach (GameObject enemyPrefab in EnemiesToSpawn)
        {
            if (enemyPrefab != null)
            {
                Instantiate(enemyPrefab, transform.position, Quaternion.identity);
                
            }
        }
    }
}
