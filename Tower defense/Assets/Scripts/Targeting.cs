using UnityEngine;

// Prioridades de alvo estilo Bloons TD.
public enum TargetingPriority { First, Last, Close, Strong }

// Helper compartilhado de seleção de alvo (evita duplicação entre torres).
public static class Targeting
{
    // Retorna o melhor alvo dentro do alcance conforme a prioridade escolhida.
    // range é em CÉLULAS (ver IsoGrid). O OverlapCircle é só o pré-filtro barato pelo raio de
    // mundo que cobre a elipse inteira; o descarte fino por CellDistance é quem decide de verdade
    // se o alvo está dentro do alcance — sem isso, a torre alcançaria o dobro na vertical.
    // BUFFER COMPARTILHADO — este é o caminho mais quente do jogo.
    //
    // `OverlapCircleAll` aloca um array NOVO a cada chamada, e isto roda por torre a cada frame,
    // com até algumas centenas de colliders dentro do alcance numa onda cheia. Dez torres a 60 Hz
    // são 600 arrays por segundo, cada um com centenas de entradas — pressão de GC suficiente
    // para explicar os picos de frame de vários SEGUNDOS medidos numa onda.
    //
    // 512 é folgado para a maior onda observada (486 inimigos). Se estourar, o excesso é
    // ignorado: perder um alvo num frame é invisível; alocar é que não.
    private static readonly Collider2D[] buffer = new Collider2D[512];

    // Varredura sem alocação, compartilhada por quem precisa de "todos os inimigos no alcance".
    // Devolve quantos entraram; leia-os em Alcancados[0..n). O buffer é reusado, então CONSUMA o
    // resultado antes de chamar de novo — nenhum uso aqui guarda a lista para depois.
    public static int Overlap(Vector2 origem, float alcanceEmCelulas, LayerMask mask)
        => Physics2D.OverlapCircleNonAlloc(origem, IsoGrid.WorldRadiusFor(alcanceEmCelulas), buffer, mask);

    public static Collider2D[] Alcancados => buffer;

    public static Transform FindTarget(Vector2 origin, float range, LayerMask mask, TargetingPriority priority, bool canSeeCamo = true)
    {
        int n = Physics2D.OverlapCircleNonAlloc(origin, IsoGrid.WorldRadiusFor(range), buffer, mask);

        Transform best = null;
        float bestScore = 0f;

        for (int i = 0; i < n; i++)
        {
            Collider2D hit = buffer[i];
            if (hit == null) continue;
            if (IsoGrid.CellDistance(origin, hit.transform.position) > range) continue;

            if (!canSeeCamo)
            {
                Health h = hit.GetComponent<Health>();
                if (h != null && h.IsCamo) continue; // ignora camuflados
            }

            float score = ScoreFor(hit, origin, priority);
            if (best == null || score > bestScore)
            {
                bestScore = score;
                best = hit.transform;
            }
        }
        return best;
    }

    // Quanto MAIOR o score, mais prioritário o alvo.
    private static float ScoreFor(Collider2D hit, Vector2 origin, TargetingPriority priority)
    {
        switch (priority)
        {
            case TargetingPriority.First:
            {
                var em = hit.GetComponent<EnemyMovement>();
                return em != null ? em.GetDistanceTraveled() : 0f;
            }
            case TargetingPriority.Last:
            {
                var em = hit.GetComponent<EnemyMovement>();
                return em != null ? -em.GetDistanceTraveled() : 0f;
            }
            case TargetingPriority.Close:
                return -IsoGrid.CellDistance(origin, hit.transform.position); // "perto" também é em células
            case TargetingPriority.Strong:
            {
                var h = hit.GetComponent<Health>();
                return h != null ? h.GetHitPoints() : 0f;
            }
        }
        return 0f;
    }

    public static string Label(TargetingPriority p)
    {
        switch (p)
        {
            case TargetingPriority.First:  return "Primeiro";
            case TargetingPriority.Last:   return "Último";
            case TargetingPriority.Close:  return "Perto";
            case TargetingPriority.Strong: return "Forte";
        }
        return "Primeiro";
    }

    public static TargetingPriority Next(TargetingPriority p) => (TargetingPriority)(((int)p + 1) % 4);
}
