using System.Collections.Generic;
using UnityEngine;

// Monta a fase escolhida na cena de jogo, sem precisar de uma cena por mapa:
// reposiciona os waypoints ao longo do traçado da fase e desliga os plots que caem em cima do
// caminho. O mesmo tabuleiro vira mapas diferentes — e o que muda de verdade (onde dá para
// construir e por onde o inimigo passa) muda junto.
[DefaultExecutionOrder(-100)] // roda antes do LevelManager e do spawner lerem o path
public class StageLoader : MonoBehaviour
{
    [SerializeField] private float pathClearance = 0.62f; // distância mínima de um plot ao caminho
    [SerializeField] private bool applyOnAwake = true;

    private void Awake()
    {
        if (applyOnAwake) Apply(StageCatalog.Selected);
    }

    public void Apply(StageDefinition stage)
    {
        if (stage == null || stage.pathNormalized == null || stage.pathNormalized.Length < 2) return;

        LevelManager lm = GetComponent<LevelManager>();
        if (lm == null) lm = FindObjectOfType<LevelManager>();
        if (lm == null || lm.path == null || lm.path.Length == 0) return;

        // Tabuleiro isométrico: o IsoBoard roda antes daqui e já sabe por quais CÉLULAS o caminho
        // passa — as células livres viraram os plots. Espalhar o traçado sobre o retângulo dos
        // plots (o caminho de baixo) só faz sentido no tabuleiro quadrado antigo.
        IsoBoard board = IsoBoard.main != null ? IsoBoard.main : FindObjectOfType<IsoBoard>();
        if (board != null && board.PathPoints != null && board.PathPoints.Length >= 2)
        {
            LayOutPath(lm, board.PathPoints);
            ApplyRules(lm, stage);
            return;
        }

        List<Transform> plots = CollectPlots();
        if (plots.Count == 0) return;

        Bounds area = BoundsOf(plots);
        Vector3[] points = ToWorld(stage.pathNormalized, area);

        LayOutPath(lm, points);
        TogglePlots(plots, points);
        ApplyRules(lm, stage);
    }

    private static List<Transform> CollectPlots()
    {
        var list = new List<Transform>();
        foreach (Plot p in FindObjectsOfType<Plot>(true)) list.Add(p.transform);
        return list;
    }

    // Área útil do tabuleiro = retângulo que contém todos os plots.
    private static Bounds BoundsOf(List<Transform> plots)
    {
        Bounds b = new Bounds(plots[0].position, Vector3.zero);
        foreach (Transform t in plots) b.Encapsulate(t.position);
        return b;
    }

    private static Vector3[] ToWorld(Vector2[] normalized, Bounds area)
    {
        var pts = new Vector3[normalized.Length];
        for (int i = 0; i < normalized.Length; i++)
        {
            pts[i] = new Vector3(
                Mathf.Lerp(area.min.x, area.max.x, normalized[i].x),
                Mathf.Lerp(area.min.y, area.max.y, normalized[i].y),
                0f);
        }
        return pts;
    }

    // Reposiciona os waypoints existentes sobre o novo traçado. Se a fase tiver mais pontos do que
    // waypoints na cena, os excedentes são criados; se tiver menos, os extras são desativados.
    private void LayOutPath(LevelManager lm, Vector3[] points)
    {
        Transform parent = lm.path[0].parent;
        var used = new List<Transform>();

        for (int i = 0; i < points.Length; i++)
        {
            Transform t;
            if (i < lm.path.Length && lm.path[i] != null) t = lm.path[i];
            else
            {
                var go = new GameObject("Point (gerado " + i + ")");
                go.transform.SetParent(parent, false);
                t = go.transform;
            }

            t.gameObject.SetActive(true);
            t.position = points[i];
            used.Add(t);
        }

        // Waypoints que sobraram do traçado anterior não podem continuar na rota.
        for (int i = points.Length; i < lm.path.Length; i++)
            if (lm.path[i] != null) lm.path[i].gameObject.SetActive(false);

        lm.path = used.ToArray();

        // O ponto de partida é o primeiro do traçado: é de onde o spawner solta os inimigos.
        if (lm.startPoint != null) lm.startPoint.position = points[0];
    }

    // Plots em cima do caminho não podem receber torre — some com eles nesta fase.
    private void TogglePlots(List<Transform> plots, Vector3[] points)
    {
        foreach (Transform plot in plots)
        {
            bool sobreCaminho = DistanceToPath(plot.position, points) < pathClearance;
            plot.gameObject.SetActive(!sobreCaminho);
        }
    }

    private static float DistanceToPath(Vector3 p, Vector3[] points)
    {
        float best = float.MaxValue;
        for (int i = 1; i < points.Length; i++)
        {
            Vector3 a = points[i - 1], b = points[i];
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            Vector3 proj = len2 < 0.0001f ? a : a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            best = Mathf.Min(best, Vector3.Distance(p, proj));
        }
        return best;
    }

    // Regras da fase + bônus permanentes do jogador.
    private void ApplyRules(LevelManager lm, StageDefinition stage)
    {
        lm.playerHP = stage.startingHP + PlayerProgress.StartingHPBonus;
        lm.SetStartingCurrency(stage.startingCurrency + PlayerProgress.StartingCurrencyBonus);
        lm.StageId = stage.id;

        EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
        if (spawner != null) spawner.ConfigureStage(stage.maxRounds, stage.enemyHealthMultiplier);
    }
}
