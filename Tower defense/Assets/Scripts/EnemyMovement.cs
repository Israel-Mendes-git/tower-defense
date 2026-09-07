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

    // Leitura para feedback visual (ver EnemyStatusFX) — não muda nenhum número de jogo, só
    // expõe o que já existia. O Gelo chama UpdateSpeed/ResetSpeed sem deixar rastro nenhum hoje:
    // um inimigo lento e um naturalmente lento (Tank, Ceramic...) ficam visualmente idênticos.
    public bool IsSlowed => moveSpeed < baseSpeedValue - 0.01f;
    public bool IsFrozen => IsSlowed && moveSpeed <= 0.01f;

    // Quanto do traçado este inimigo já venceu, de 0 a 1.
    //
    // É a medida honesta de FOLGA da defesa: se tudo morre nos primeiros 20% do caminho, o
    // jogador tem poder de fogo sobrando e nenhuma quantidade de inimigos vai ameaçá-lo; se a
    // onda chega a 80%, a rodada está no limite. "Dano recebido" só distingue 0 de alguma coisa,
    // e o jogo passou 36 rodadas em zero — um número que não gradua não serve para calibrar.
    public float ProgressoNoTracado
    {
        get
        {
            if (LevelManager.main == null || LevelManager.main.path == null) return 0f;
            int n = LevelManager.main.path.Length;
            return n <= 0 ? 0f : Mathf.Clamp01((float)pathIndex / n);
        }
    }

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

        // GUARDA CONTRA O LEVELMANAGER SUMIR NO MEIO DA VIDA DO INIMIGO.
        //
        // Isto era a causa do travamento do jogo, e não uma checagem de estilo. Quando o
        // LevelManager já foi destruído (troca ou recarga de cena) a estática `main` continua
        // apontando para ele, e `main.path` lança NullReferenceException — POR INIMIGO, POR
        // FRAME. Com cinquenta inimigos em campo são cinquenta exceções por quadro, cada uma
        // montando stack trace e alocando: medido, 33.219 alocações e 2,4 MB num único frame, com
        // o jogo caindo de 110 para 3 FPS e travadas de vários segundos.
        //
        // Exceção em Update não aparece como bug de performance no perfil — aparece como GC. Foi
        // por isso que cinco rodadas de otimização legítima não moveram o ponteiro.
        LevelManager lm = LevelManager.main;
        if (lm == null || lm.path == null) return;

        if (Vector2.Distance(transform.position, target.position) <= 0.1f)
        {
            pathIndex++;

            if (pathIndex >= lm.path.Length)
            {
                EnemySpawner.onEnemyDestroy.Invoke();
                lm.TakePlayerDamage(damage);
                Destroy(gameObject);
                return;
            }

            target = lm.path[pathIndex];
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

    // Ponto de onde o inimigo veio — destino do recuo.
    private Vector3 PreviousPoint()
    {
        if (pathIndex >= 1 && pathIndex - 1 < LevelManager.main.path.Length)
            return LevelManager.main.path[pathIndex - 1].position;
        return LevelManager.main.startPoint.position;
    }

    // Empurra o inimigo para TRÁS ao longo da rota, atravessando waypoints se preciso.
    // É o verbo da torre de gelo: em vez de matar, devolve terreno já conquistado.
    public void PushBack(float distance)
    {
        if (LevelManager.main == null || LevelManager.main.path == null) return;

        float remaining = distance;
        int guard = 0; // a rota é finita, mas nunca deixe o laço solto no Update

        while (remaining > 0.001f && guard++ < 64)
        {
            Vector3 prev = PreviousPoint();
            Vector3 toPrev = prev - transform.position;
            float d = toPrev.magnitude;

            if (d <= 0.001f)
            {
                if (pathIndex <= 0) break;   // já está no início da rota
                pathIndex--;
                target = LevelManager.main.path[pathIndex];
                continue;
            }

            float step = Mathf.Min(remaining, d);
            transform.position += toPrev.normalized * step;
            remaining -= step;

            if (step >= d - 0.001f)
            {
                if (pathIndex <= 0) break;
                pathIndex--;
                target = LevelManager.main.path[pathIndex];
            }
        }

        distanceTraveled = Mathf.Max(0f, distanceTraveled - (distance - remaining));
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