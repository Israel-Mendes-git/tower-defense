using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrojanHorse : MonoBehaviour
{
    [Header("Trojan Horse Settings")]
    [SerializeField] private float initialSpeed = 1.2f;     // Começa LENTO
    [SerializeField] private float maxSpeed = 4.5f;          // Velocidade FINAL insana
    [SerializeField] private float accelerationTime = 25f;   // Leva 25s pra atingir max 

    private EnemyMovement enemyMovement;
    private float spawnTime;
    private float currentSpeedMultiplier = 0f;

    private void Start()
    {
        enemyMovement = GetComponent<EnemyMovement>();

        // Configurar stats específicos do Trojan
        enemyMovement.SetBaseSpeed(initialSpeed);

        spawnTime = Time.time;

        // Iniciar coroutine de aceleração
        StartCoroutine(AccelerateOverTime());
    }

    private IEnumerator AccelerateOverTime()
    {
        float elapsed = 0f;

        while (elapsed < accelerationTime)
        {
            elapsed = Time.time - spawnTime;
            currentSpeedMultiplier = Mathf.Clamp01(elapsed / accelerationTime);

            // Aplica velocidade atual
            float currentSpeed = Mathf.Lerp(initialSpeed, maxSpeed, currentSpeedMultiplier);
            enemyMovement.SetBaseSpeed(currentSpeed);

            yield return new WaitForSeconds(0.1f); // Atualiza a cada 0.1s
        }
    }

    private void OnDestroy()
    {
        // Efeito especial ao morrer (opcional)
        // Instantiate(explosionPrefab, transform.position, Quaternion.identity);
    }

    // Debug: mostra velocidade atual
    private void OnDrawGizmosSelected()
    {
        // Só desenha label se o componente EnemyMovement estiver presente
        if (enemyMovement == null)
        {
            enemyMovement = GetComponent<EnemyMovement>();
            if (enemyMovement == null)
            {
                // Fallback: mostra "N/A" se não encontrar
                UnityEditor.Handles.color = Color.yellow;
                UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, "EnemyMovement não encontrado");
                return;
            }
        }

        float currentSpeed = enemyMovement.moveSpeed;

        UnityEditor.Handles.color = Color.yellow;
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 12;
        style.alignment = TextAnchor.MiddleCenter;

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.5f,
            $"Speed: {currentSpeed:F1}x",
            style
        );
    }
}