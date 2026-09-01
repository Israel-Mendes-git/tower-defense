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
    public static Transform FindTarget(Vector2 origin, float range, LayerMask mask, TargetingPriority priority, bool canSeeCamo = true)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, IsoGrid.WorldRadiusFor(range), mask);

        Transform best = null;
        float bestScore = 0f;

        foreach (var hit in hits)
        {
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
