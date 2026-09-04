using UnityEngine;

// Que peças o jogador já conquistou o direito de usar.
//
// O acesso é gradual DE PROPÓSITO: com tudo liberado desde a rodada 1 (como era até
// 2026-09-03), nada é descoberto e não há motivo para voltar — o incômodo nº 4 da lista
// original do Israel. Aqui o leque abre conforme o nível de comandante (PlayerProgress).
//
// DUAS REGRAS INEGOCIÁVEIS, nesta ordem:
//
// 1. GENEROSO NO COMEÇO. O nível 1 já entrega quatro torres e um poder — o suficiente para
//    jogar de verdade, não uma versão mutilada. O que o tempo destrava é PROFUNDIDADE
//    (especialistas, terceiros tiers), nunca o básico.
//
// 2. A REGRA DE OURO VEM PRIMEIRO. O roteiro apresenta camuflado na rodada 5, chumbo na 11 e
//    cerâmica na 17. A resposta a cada um desses tem que estar disponível ANTES de a ameaça
//    chegar, senão o jogo cobra o que não deu. Por isso Detector (vê camo), Bomba (explosão,
//    fura chumbo) e Basic/Tachinha (cortante, fura cerâmica) nascem todos no nível 1.
//    Mexer nesta tabela exige refazer essa conferência — ver ConfereRegraDeOuro().
public static class Unlocks
{
    // Índice no catálogo do BuildManager -> nível de comandante exigido.
    // A ordem do catálogo é: 0 Basic, 1 Ice, 2 Tachinha, 3 Bomba, 4 Sniper, 5 Detector,
    // 6 Metralhadora, 7 Tesla, 8 Gerador.
    private static readonly int[] NivelDaTorre =
    {
        1,  // 0 Basic Turret — cortante, o pão com manteiga
        5,  // 1 Ice Turret — controle, especialista
        1,  // 2 Tachinha — cortante em área, resposta a cerâmica
        1,  // 3 Bomb Turret — explosão, resposta a chumbo
        10, // 4 Sniper — alcance global, a peça mais forte do jogo
        1,  // 5 Detector — detecção de camuflado, resposta obrigatória à rodada 5
        3,  // 6 Metralhadora
        7,  // 7 Tesla
        12, // 8 Gerador — economia por último: nas mãos de quem ainda não sabe gastar,
            //   ela só antecipa a bola de neve que já custou caro aqui
    };

    // O terceiro degrau de cada trilha é o salto de identidade da torre (LANÇA PERFURANTE,
    // BOMBA CACHO, PERMAFROST...). Fica atrás de um nível para que a primeira dezena de
    // partidas seja sobre aprender as torres, e as seguintes sobre extremá-las.
    public const int NivelParaTierMaximo = 6;

    // Poderes ativos (AbilityManager): índices 0 Bombardeio, 1 Congelar, 2 Reparo.
    // O primeiro nasce liberado: são eles que respondem ao "fico sem fazer nada durante a
    // onda", então segurar todos seria piorar justamente o que se quer melhorar.
    private static readonly int[] NivelDoPoder = { 1, 5, 9 };

    public const int NivelParaAliados = 3;

    // CHAVE DE DESENVOLVIMENTO: com isto ligado, tudo fica liberado independentemente do nível.
    // Serve para testar o jogo inteiro sem precisar subir de comandante nem mexer no save real.
    // Quem liga é o componente UnlockSettings, no inspetor — ver UnlockSettings.cs.
    public static bool LiberarTudo = false;

    public static int Nivel => PlayerProgress.Level;

    public static bool TorreLiberada(int indice)
    {
        if (LiberarTudo) return true;
        if (indice < 0 || indice >= NivelDaTorre.Length) return true; // torre nova sem tabela: liberada
        return Nivel >= NivelDaTorre[indice];
    }

    public static int NivelExigidoPelaTorre(int indice)
        => (indice < 0 || indice >= NivelDaTorre.Length) ? 1 : NivelDaTorre[indice];

    // nivelDoTier é 1-based: 1 e 2 sempre liberados, 3 exige NivelParaTierMaximo.
    public static bool TierLiberado(int nivelDoTier)
        => LiberarTudo || nivelDoTier < 3 || Nivel >= NivelParaTierMaximo;

    public static bool PoderLiberado(int indice)
    {
        if (LiberarTudo) return true;
        if (indice < 0 || indice >= NivelDoPoder.Length) return true;
        return Nivel >= NivelDoPoder[indice];
    }

    public static int NivelExigidoPeloPoder(int indice)
        => (indice < 0 || indice >= NivelDoPoder.Length) ? 1 : NivelDoPoder[indice];

    public static bool AliadosLiberados => LiberarTudo || Nivel >= NivelParaAliados;

    // Conferência da regra de ouro, para rodar em teste depois de mexer na tabela: as três
    // ameaças com imunidade precisam de resposta disponível no nível 1.
    // Devolve null se está tudo certo, ou a descrição do furo.
    public static string ConfereRegraDeOuro()
    {
        bool camo = NivelDaTorre[5] == 1;                        // Detector
        bool antiChumbo = NivelDaTorre[3] == 1;                  // Bomba (explosão)
        bool antiCeramica = NivelDaTorre[0] == 1 || NivelDaTorre[2] == 1; // cortante

        if (!camo) return "camuflado aparece na rodada 5 e o Detector não está liberado no nível 1";
        if (!antiChumbo) return "chumbo aparece na rodada 11 e nenhuma resposta não-cortante está liberada no nível 1";
        if (!antiCeramica) return "cerâmica aparece na rodada 17 e nenhuma resposta não-explosiva está liberada no nível 1";
        return null;
    }
}
