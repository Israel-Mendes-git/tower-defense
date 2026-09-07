using UnityEngine;

// Progresso que sobrevive à partida. Sem isto, ganhar ou perder tem exatamente a mesma consequência
// — nenhuma — e não existe motivo para abrir o jogo de novo. Aqui, cada rodada sobrevivida vira XP,
// o XP vira nível de comandante, e o nível concede bônus permanentes e desbloqueia conteúdo.
public static class PlayerProgress
{
    private const string KeyXP = "Progress_XP";
    private const string KeyBestRound = "BestRound";
    private const string KeyStagePrefix = "Stage_Best_";
    private const string KeyStageStarsPrefix = "Stage_Stars_";

    // Curva simples e legível: cada nível custa um pouco mais que o anterior.
    private const int BaseXPPerLevel = 500;
    private const int MaxLevel = 30;

    public static int TotalXP => PlayerPrefs.GetInt(KeyXP, 0);

    public static int Level
    {
        get
        {
            int xp = TotalXP, level = 1;
            while (level < MaxLevel && xp >= XPForLevel(level + 1)) level++;
            return level;
        }
    }

    // XP acumulado necessário para chegar a um nível.
    public static int XPForLevel(int level)
    {
        int total = 0;
        for (int i = 2; i <= level; i++) total += BaseXPPerLevel + (i - 2) * 150;
        return total;
    }

    public static int XPIntoCurrentLevel => TotalXP - XPForLevel(Level);
    public static int XPNeededForNext => Level >= MaxLevel ? 0 : XPForLevel(Level + 1) - XPForLevel(Level);

    // ───────── Bônus permanentes ─────────
    // Deliberadamente modestos: o nível deve dar vantagem inicial, nunca substituir jogar bem.

    // +2% de dano por nível, teto de +60%.
    public static float DamageBonus => 1f + Mathf.Min(0.60f, (Level - 1) * 0.02f);

    // +10 de verba inicial por nível.
    public static int StartingCurrencyBonus => (Level - 1) * 10;

    // A cada 5 níveis, +1 de vida inicial extra por nível alcançado.
    public static int StartingHPBonus => (Level - 1) / 5 * 5;

    // ───────── Registro de partida ─────────

    public const int XPPorRodada = 25;

    // XP CREDITADO NA HORA, a cada rodada sobrevivida.
    //
    // Antes o XP inteiro só era pago no desfecho (morte ou vitória), e isso tinha uma
    // consequência que só apareceu na telemetria de uma partida real: quem para no meio — o que
    // é comum, porque uma partida completa passa de 10 minutos — ficava com ZERO. Medido: 29
    // rodadas jogadas, nenhum XP, comandante ainda no nível 1. E como o nível 1 tranca o tier 3
    // e cinco das nove torres, o jogador voltava para a mesma partida sem perigo e sem ter no
    // que gastar o dinheiro. O progresso não pode depender de chegar ao fim.
    public static int CreditRound()
    {
        PlayerPrefs.SetInt(KeyXP, TotalXP + XPPorRodada);
        PlayerPrefs.Save();
        return XPPorRodada;
    }

    // Chamado ao fim de uma partida (vitória ou derrota). As rodadas já foram pagas uma a uma
    // por CreditRound; aqui fica só o bônus de terminar e o registro de recorde.
    public static int RecordRun(int roundsSurvived, bool victory, string stageId)
    {
        int xp = victory ? 500 : 0;

        int before = Level;
        if (xp > 0) PlayerPrefs.SetInt(KeyXP, TotalXP + xp);

        int best = Mathf.Max(PlayerPrefs.GetInt(KeyBestRound, 0), roundsSurvived);
        PlayerPrefs.SetInt(KeyBestRound, best);

        if (!string.IsNullOrEmpty(stageId))
        {
            string k = KeyStagePrefix + stageId;
            PlayerPrefs.SetInt(k, Mathf.Max(PlayerPrefs.GetInt(k, 0), roundsSurvived));

            if (victory)
            {
                string ks = KeyStageStarsPrefix + stageId;
                PlayerPrefs.SetInt(ks, Mathf.Max(PlayerPrefs.GetInt(ks, 0), 1));
            }
        }

        PlayerPrefs.Save();

        if (Level > before) Debug.Log($"Comandante subiu para o nível {Level}!");
        return xp;
    }

    public static int BestRound => PlayerPrefs.GetInt(KeyBestRound, 0);

    public static int StageBestRound(string stageId) => PlayerPrefs.GetInt(KeyStagePrefix + stageId, 0);
    public static int StageStars(string stageId) => PlayerPrefs.GetInt(KeyStageStarsPrefix + stageId, 0);
    public static bool StageCleared(string stageId) => StageStars(stageId) > 0;

    public static void SetStageStars(string stageId, int stars)
    {
        string k = KeyStageStarsPrefix + stageId;
        PlayerPrefs.SetInt(k, Mathf.Max(PlayerPrefs.GetInt(k, 0), Mathf.Clamp(stars, 0, 3)));
        PlayerPrefs.Save();
    }

    // Usado por uma opção de "apagar progresso" no menu.
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(KeyXP);
        PlayerPrefs.DeleteKey(KeyBestRound);
        PlayerPrefs.Save();
    }
}
