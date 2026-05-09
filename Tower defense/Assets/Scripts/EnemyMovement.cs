using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Attributes")]
    [SerializeField] public float moveSpeed = 2f;
    [SerializeField] public int damage;

    private Transform target;
    private int pathIndex = 0;
    private float baseSpeedValue;

    [Header("Trojan Horse Support")]
    private float distanceTraveled = 0f;

    private void Start()
    {
        baseSpeedValue = moveSpeed;
        if (LevelManager.main == null || LevelManager.main.path == null || LevelManager.main.path.Length == 0)
        {
            Debug.LogWarning("Path não encontrado no LevelManager!", this);
            return;
        }

        target = LevelManager.main.path[pathIndex];
    }

    private void Update()
    {
        if (target == null) return;

        if (Vector2.Distance(transform.position, target.position) <= 0.1f)
        {
            pathIndex++;

            if (pathIndex >= LevelManager.main.path.Length)
            {
                EnemySpawner.onEnemyDestroy.Invoke();
                LevelManager.main.TakePlayerDamage(damage);
                Destroy(gameObject);
                return;
            }

            target = LevelManager.main.path[pathIndex];
        }
    }

    private void FixedUpdate()
    {
        if (target == null) return;

        Vector2 direction = (target.position - transform.position).normalized;
        rb.velocity = direction * moveSpeed;

        // Atualiza distância percorrida (para sniper priorizar)
        distanceTraveled += moveSpeed * Time.fixedDeltaTime;
    }

    // Métodos públicos usados por torres e efeitos
    public void SetBaseSpeed(float newBaseSpeed)
    {
        baseSpeedValue = newBaseSpeed;
        moveSpeed = baseSpeedValue; // Aplica imediatamente
    }

    public float GetDistanceTraveled()
    {
        // Distância aproximada percorrida (usado pela Sniper para priorizar alvo mais avançado)
        if (LevelManager.main == null || LevelManager.main.path == null || pathIndex >= LevelManager.main.path.Length)
            return distanceTraveled;

        return distanceTraveled + Vector2.Distance(transform.position, LevelManager.main.path[pathIndex].position);
    }

    public void UpdateSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }

    public void ResetSpeed()
    {
        moveSpeed = baseSpeedValue;
    }
}