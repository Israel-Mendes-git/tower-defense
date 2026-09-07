using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Grava o que realmente aconteceu numa partida, para calibrar com dados em vez de impressão.
//
// É um OBSERVADOR: pendura-se nos eventos que já existem e tira uma foto do estado ao fim de
// cada rodada. Não altera regra nenhuma, não intercepta input e não decide nada — se for
// removido, o jogo se comporta exatamente igual. Isso é de propósito: instrumentação que muda
// o que mede não serve para calibrar.
//
// A saída vai para <projeto>/Playtests/playtest-<data>.csv, uma linha por rodada.
public class PlaytestLogger : MonoBehaviour
{
    [SerializeField] private bool ativo = true;

    private string arquivo;
    private int vidaNoInicioDaRodada;
    private int rodadaAtual;
    private float inicioDaRodada;
    private bool encerrado;

    // Última composição vista, para detectar o que o jogador construiu ENTRE as rodadas.
    private readonly Dictionary<string, int> composicaoAnterior = new Dictionary<string, int>();

    private void OnEnable()
    {
        if (!ativo) return;
        EnemySpawner.onWaveComplete.AddListener(RegistrarRodada);
    }

    private void OnDisable()
    {
        EnemySpawner.onWaveComplete.RemoveListener(RegistrarRodada);
    }

    private void Start()
    {
        if (!ativo) return;

        string pasta = Application.dataPath + "/../Playtests";
        if (!System.IO.Directory.Exists(pasta)) System.IO.Directory.CreateDirectory(pasta);

        arquivo = pasta + "/playtest-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv";

        var cab = new StringBuilder();
        // `investido` é o custo de REMONTAR a defesa daquela rodada (construção + upgrades). O
        // `dinheiro` sozinho não responde isso: ele é o que sobrou no bolso, não o que está em
        // campo — e é o investido que diz se a verba de repetir a rodada está barata ou cara.
        cab.Append("rodada,vida_inicio,vida_fim,dano_recebido,dinheiro,investido,torres,aliados,")
           .Append("nivel_medio,segundos,fase,nivel_comandante,jogada_do_adversario,")
           .Append("composicao,construiu_nesta_rodada");
        Escrever(cab.ToString());

        vidaNoInicioDaRodada = LevelManager.main != null ? LevelManager.main.playerHP : 0;
        inicioDaRodada = Time.time;
        rodadaAtual = EnemySpawner.main != null ? EnemySpawner.main.CurrentWave : 1;
    }

    private void Update()
    {
        if (!ativo || encerrado) return;

        // Fim de partida: registra o desfecho e para. `isDead` também é ligado na vitória.
        if (LevelManager.main != null && LevelManager.main.isDead)
        {
            encerrado = true;
            int reached = EnemySpawner.main != null ? EnemySpawner.main.CurrentWave : 0;
            int max = EnemySpawner.main != null ? EnemySpawner.main.MaxRounds : 0;
            string desfecho = (max > 0 && reached > max) ? "VITORIA" : "DERROTA";
            Escrever("# " + desfecho + " na rodada " + reached + " de " + max
                   + " | vida final " + LevelManager.main.playerHP);
            Debug.Log("[PlaytestLogger] partida encerrada (" + desfecho + "). Dados em: " + arquivo);
            return;
        }

        // A rodada virou: o spawner incrementa CurrentWave no fim da onda.
        if (EnemySpawner.main != null && EnemySpawner.main.CurrentWave != rodadaAtual)
        {
            rodadaAtual = EnemySpawner.main.CurrentWave;
            vidaNoInicioDaRodada = LevelManager.main != null ? LevelManager.main.playerHP : 0;
            inicioDaRodada = Time.time;
        }
    }

    // Chamado pelo evento de fim de onda. Neste ponto o spawner ainda não incrementou a rodada.
    private void RegistrarRodada()
    {
        if (!ativo || encerrado) return;

        LevelManager lm = LevelManager.main;
        if (lm == null) return;

        int rodada = EnemySpawner.main != null ? EnemySpawner.main.CurrentWave : 0;
        int vidaFim = lm.playerHP;
        int dano = Mathf.Max(0, vidaNoInicioDaRodada - vidaFim);

        // Composição: quantas torres de cada tipo, e quanto estão evoluídas.
        var porTipo = new Dictionary<string, int>();
        int somaNiveis = 0, totalTorres = 0;

        foreach (TowerBase t in FindObjectsOfType<TowerBase>())
        {
            string tipo = t.GetType().Name;
            int nivel = t.GetCurrentLevel();
            string chave = tipo + ":" + t.PathLevel(0) + "-" + t.PathLevel(1);

            if (porTipo.ContainsKey(chave)) porTipo[chave]++;
            else porTipo[chave] = 1;

            somaNiveis += nivel;
            totalTorres++;
        }

        int aliados = FindObjectsOfType<Ally>().Length;
        float nivelMedio = totalTorres > 0 ? (float)somaNiveis / totalTorres : 0f;

        // Soma pelo TowerValue, que é quem guarda construção + upgrades. Lido da raiz de cada
        // torre: uma torre é um objeto composto, e contar por TowerBase somaria o mesmo
        // investimento mais de uma vez quando a pilha tem vários renderers com script.
        int investido = 0;
        foreach (TowerValue tv in FindObjectsOfType<TowerValue>()) investido += tv.Invested;

        // O que apareceu desde a rodada anterior — mostra QUANDO o jogador investiu em quê.
        string novidades = Diferenca(porTipo);

        var linha = new StringBuilder();
        linha.Append(rodada).Append(',')
             .Append(vidaNoInicioDaRodada).Append(',')
             .Append(vidaFim).Append(',')
             .Append(dano).Append(',')
             .Append(lm.currency).Append(',')
             .Append(investido).Append(',')
             .Append(totalTorres).Append(',')
             .Append(aliados).Append(',')
             .Append(nivelMedio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
             .Append((Time.time - inicioDaRodada).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
             .Append(lm.StageId).Append(',')
             .Append(PlayerProgress.Level).Append(',')
             // O que o adversário ANUNCIOU nesta rodada. Sem esta coluna não dá para separar
             // "a rodada 22 dói" de "a rodada 22 dói quando ele marca a sua torre principal",
             // que são dois problemas de calibragem opostos.
             .Append('"').Append(JogadaDaRodada(rodada)).Append('"').Append(',')
             .Append('"').Append(Resumo(porTipo)).Append('"').Append(',')
             .Append('"').Append(novidades).Append('"');

        Escrever(linha.ToString());

        composicaoAnterior.Clear();
        foreach (var kv in porTipo) composicaoAnterior[kv.Key] = kv.Value;
    }

    private static string JogadaDaRodada(int rodada)
    {
        if (CommanderPlays.main == null) return "";
        return CommanderPlays.main.ResumoDaOndaEm(rodada);
    }

    private static string Resumo(Dictionary<string, int> comp)
    {
        var sb = new StringBuilder();
        foreach (var kv in comp)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append(kv.Key).Append('x').Append(kv.Value);
        }
        return sb.Length > 0 ? sb.ToString() : "(vazio)";
    }

    // Só o que aumentou desde a última rodada: construção nova ou upgrade comprado.
    private string Diferenca(Dictionary<string, int> agora)
    {
        var sb = new StringBuilder();
        foreach (var kv in agora)
        {
            int antes = composicaoAnterior.ContainsKey(kv.Key) ? composicaoAnterior[kv.Key] : 0;
            if (kv.Value <= antes) continue;
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append('+').Append(kv.Value - antes).Append(' ').Append(kv.Key);
        }
        return sb.Length > 0 ? sb.ToString() : "";
    }

    private void Escrever(string linha)
    {
        if (string.IsNullOrEmpty(arquivo)) return;
        try { System.IO.File.AppendAllText(arquivo, linha + "\n", System.Text.Encoding.UTF8); }
        catch (System.Exception e) { Debug.LogWarning("[PlaytestLogger] não consegui gravar: " + e.Message); }
    }
}
