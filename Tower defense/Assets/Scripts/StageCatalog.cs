using UnityEngine;

// Definição de uma fase. O traçado é guardado em coordenadas NORMALIZADAS (0..1) sobre a área
// ocupada pelos plots — assim uma fase nova é só uma lista de pontos, sem depender das coordenadas
// absolutas da cena nem exigir montar um mapa novo no editor.
[System.Serializable]
public class StageDefinition
{
    public string id;
    public string displayName;
    public string description;

    public Vector2[] pathNormalized; // do início ao fim, em 0..1
    public int maxRounds = 40;
    public int startingHP = 150;
    public int startingCurrency = 175;
    public float enemyHealthMultiplier = 1f;
    public int requiredLevel = 1;     // nível de comandante para liberar

    public StageDefinition(string id, string displayName, string description, Vector2[] path,
        int maxRounds = 40, int startingHP = 150, int startingCurrency = 175,
        float enemyHealthMultiplier = 1f, int requiredLevel = 1)
    {
        this.id = id;
        this.displayName = displayName;
        this.description = description;
        this.pathNormalized = path;
        this.maxRounds = maxRounds;
        this.startingHP = startingHP;
        this.startingCurrency = startingCurrency;
        this.enemyHealthMultiplier = enemyHealthMultiplier;
        this.requiredLevel = requiredLevel;
    }
}

// As fases do jogo. Cada uma muda o traçado (e portanto quais posições valem a pena) e as regras.
public static class StageCatalog
{
    private const string SelectedKey = "SelectedStage";

    public static readonly StageDefinition[] All =
    {
        new StageDefinition("vale", "Vale Simples",
            "Um caminho largo em S. Bom para aprender: sobra espaço para tudo.",
            new[] {
                new Vector2(0.08f, 0.90f), new Vector2(0.08f, 0.62f), new Vector2(0.45f, 0.62f),
                new Vector2(0.45f, 0.35f), new Vector2(0.85f, 0.35f), new Vector2(0.85f, 0.10f),
            },
            maxRounds: 30, startingHP: 150, startingCurrency: 200),

        new StageDefinition("serpente", "Serpente",
            "Vai e volta várias vezes. Torres de alcance curto no meio cobrem muitas passagens.",
            new[] {
                new Vector2(0.05f, 0.92f), new Vector2(0.90f, 0.92f), new Vector2(0.90f, 0.72f),
                new Vector2(0.10f, 0.72f), new Vector2(0.10f, 0.52f), new Vector2(0.90f, 0.52f),
                new Vector2(0.90f, 0.32f), new Vector2(0.10f, 0.32f), new Vector2(0.10f, 0.10f),
                new Vector2(0.90f, 0.10f),
            },
            maxRounds: 40, startingHP: 150, startingCurrency: 175),

        new StageDefinition("espiral", "Espiral",
            "O caminho fecha em espiral até o centro. O miolo cobre quase tudo — e é disputado.",
            new[] {
                new Vector2(0.05f, 0.95f), new Vector2(0.95f, 0.95f), new Vector2(0.95f, 0.08f),
                new Vector2(0.08f, 0.08f), new Vector2(0.08f, 0.75f), new Vector2(0.75f, 0.75f),
                new Vector2(0.75f, 0.28f), new Vector2(0.30f, 0.28f), new Vector2(0.30f, 0.55f),
                new Vector2(0.55f, 0.55f),
            },
            maxRounds: 40, startingHP: 120, startingCurrency: 175,
            enemyHealthMultiplier: 1.15f, requiredLevel: 3),

        new StageDefinition("atalho", "Atalho",
            "Curto e direto: pouquíssimo tempo de exposição. Exige dano concentrado na entrada.",
            new[] {
                new Vector2(0.05f, 0.55f), new Vector2(0.50f, 0.55f), new Vector2(0.95f, 0.55f),
            },
            maxRounds: 35, startingHP: 100, startingCurrency: 250,
            enemyHealthMultiplier: 1.25f, requiredLevel: 5),

        new StageDefinition("labirinto", "Labirinto",
            "Longo e cheio de cotovelos. Recompensa quem investe em economia no começo.",
            new[] {
                new Vector2(0.05f, 0.95f), new Vector2(0.30f, 0.95f), new Vector2(0.30f, 0.70f),
                new Vector2(0.05f, 0.70f), new Vector2(0.05f, 0.45f), new Vector2(0.55f, 0.45f),
                new Vector2(0.55f, 0.85f), new Vector2(0.80f, 0.85f), new Vector2(0.80f, 0.25f),
                new Vector2(0.35f, 0.25f), new Vector2(0.35f, 0.05f), new Vector2(0.95f, 0.05f),
            },
            maxRounds: 50, startingHP: 200, startingCurrency: 150,
            enemyHealthMultiplier: 1.4f, requiredLevel: 8),
    };

    public static StageDefinition Get(string id)
    {
        foreach (StageDefinition s in All)
            if (s.id == id) return s;
        return All[0];
    }

    public static StageDefinition Selected => Get(PlayerPrefs.GetString(SelectedKey, All[0].id));

    public static void Select(string id) => PlayerPrefs.SetString(SelectedKey, id);

    public static bool IsUnlocked(StageDefinition s) => PlayerProgress.Level >= s.requiredLevel;
}
