using System.Collections.Generic;
using UnityEngine;

public enum PlayKind { Marcacao, Cerco, Reforco }

// Uma JOGADA do adversário: uma decisão ANUNCIADA que age durante a onda, em vez de só compor a
// fila de inimigos.
//
// A composição (CounterCommander) responde "o que ele manda". A jogada responde "o que ele vai
// FAZER" — e é ela que cobra atenção durante a rodada, não só na hora de comprar. Sem isso, a
// partida inteira cabe em "monte a defesa certa e assista".
public class CommanderPlay
{
    public PlayKind tipo;
    public string nome;
    public string manchete;   // o anúncio, curto, que vai para a HUD antes da onda
    public string razao;      // por que ele escolheu ESTA jogada contra ESTA defesa
    public float eficacia;
    public int custo;

    // Inimigos que a jogada traz junto (os sabotadores dirigidos). Entram na composição normal.
    public readonly List<PlanItem> itens = new List<PlanItem>();

    public TowerBase alvo;              // Marcacao
    public Vector3 centro;              // Cerco
    public float raio;                  // Cerco, em células
    public readonly List<int> reforco = new List<int>();  // Reforco: a leva guardada
}

// AS JOGADAS DIRIGIDAS — a metade da Fase D que faltava.
//
// O Sabotador comprado é pressão REATIVA: ele aparece porque a defesa está amontoada e ataca o
// que estiver por perto. Isso é pouco. O que faltava é DECISÃO ANUNCIADA: o adversário escolhe um
// alvo, diz qual é, e vai atrás — e o jogador tem a onda inteira para responder.
//
// As três jogadas atacam três folgas diferentes do jogador:
//
//   MARCAÇÃO — contra o pilar. Elege a torre de maior investimento e manda sabotadores atrás dela
//              especificamente, com dano estrutural dobrado. Cobra cobertura mútua: uma torre
//              cara sozinha num canto deixa de ser aposta segura.
//   CERCO    — contra o amontoado. Elege o ponto do traçado com mais torres em cima e, ali
//              dentro, a sabotagem vira dano em ÁREA em vez de atingir só a mais próxima. Cobra
//              distribuição: empilhar tudo num ponto passa a ter preço.
//   REFORÇO  — contra a atenção. Guarda parte da onda e injeta no meio dela, anunciado. Cobra
//              presença: a rodada deixa de acabar no instante em que começa.
//
// AS TRÊS REGRAS DA PERDA VALEM AQUI INTEIRAS. Telegrafada: manchete antes, anel pulsante no
// tabuleiro durante. Evitável: o executor é sempre um Sabotador visível e matável, e matá-lo
// interrompe. Nunca aleatória: o alvo sai de critério declarado (o maior investimento, o ponto
// mais denso), nunca de sorteio.
//
// E A REGRA DE OURO TAMBÉM: ele não marca o que o jogador não tem como defender. Torre marcada
// precisa estar coberta por alguma torre que atire; cerco precisa de um amontoado real; e nenhuma
// das duas acontece antes de o Sabotador ter sido apresentado pelo roteiro.
public class CommanderPlays : MonoBehaviour
{
    [Header("Travas")]
    // Duas jogadas por rodada é o teto porque três viram ruído: o jogador precisa conseguir
    // repetir em voz alta o que o adversário anunciou, senão o anúncio não é informação.
    [SerializeField] private int maximoDeJogadas = 2;

    // Nada de jogada dirigida nas primeiras rodadas. Não é rampa arbitrária: antes disso o jogador
    // tem uma ou duas torres, e não existe nem pilar para marcar nem amontoado para cercar — a
    // jogada não teria o que dizer.
    [SerializeField] private int rodadaMinima = 6;

    // Sabotador é o executor de TODAS as jogadas de perda. Poucos demais e a marcação é teatro;
    // muitos demais e a rodada vira um enxame anti-torre e nada mais.
    //
    // Quantos vêm NÃO é número fixo, e isso importa: o Sabotador tem 12 de vida, então dois deles
    // numa rodada 20 morrem antes de chegar perto da torre marcada e a jogada anunciada não
    // acontece — anúncio sem consequência ensina o jogador a ignorar o anúncio. O piso garante
    // que a jogada exista; o teto impede que ela vire a rodada inteira; entre os dois, quem
    // decide é o orçamento.
    [SerializeField] private int sabotadoresPorJogada = 2;
    [SerializeField] private int maximoDeSabotadores = 5;

    // Fatia do orçamento de jogadas que pode ir para executores.
    [SerializeField, Range(0.1f, 0.8f)] private float fatiaParaExecutores = 0.4f;

    [Header("Marcação")]
    // O prêmio da direção, cobrado sobre o investimento da torre marcada. Mirar de propósito custa
    // onda: ele troca volume por precisão, que é exatamente a troca que se quer que ele pense
    // duas vezes antes de fazer.
    [SerializeField, Range(0f, 0.3f)] private float premioDaMarcacao = 0.10f;
    [SerializeField] private float multiplicadorNoAlvo = 2f;

    // O EXECUTOR PRECISA CHEGAR. O Sabotador tem 12 de vida; numa rodada 30 ele morre muito antes
    // das 1,6 células que precisa para sabotar, e a jogada anunciada não acontece — medido, uma
    // onda com 55 sabotadores não desligou uma única torre. Anunciar e não cumprir é pior que não
    // anunciar: ensina o jogador a ignorar a manchete.
    //
    // Enquanto uma jogada dirigida está ativa, o Sabotador vem reforçado. Continua matável e
    // continua sendo a resposta certa — só deixa de ser teatro.
    [SerializeField] private float vidaDoExecutor = 6f;

    // Consultado pelo EnemySpawner na hora de instanciar (ver Spawn).
    public float MultiplicadorDoExecutor
        => (alvoMarcado != null || cercoAtivo) ? Mathf.Max(1f, vidaDoExecutor) : 1f;

    // Raio (em células) dentro do qual um Sabotador abandona o alvo mais próximo e vai atrás da
    // torre marcada. Maior que o raio de sabotagem de propósito: é isso que faz ele "ir atrás".
    [SerializeField] private float raioDeCaca = 5f;

    [Header("Cerco")]
    [SerializeField] private float raioDoCerco = 2.5f;
    [SerializeField, Range(0f, 0.3f)] private float premioDoCerco = 0.08f;

    [Header("Reforço")]
    // Fração dos spawns da onda em que a leva guardada entra. 0,55 põe o susto DEPOIS de o jogador
    // ter lido a onda e decidido que estava tranquilo, que é quando ele custa alguma coisa; a 0,9
    // chegaria junto com o fim e não mudaria decisão nenhuma.
    [SerializeField, Range(0.2f, 0.9f)] private float gatilhoDoReforco = 0.55f;

    // Que fatia de cada tipo da composição ele guarda para o meio.
    [SerializeField, Range(0.1f, 0.6f)] private float fatiaDoReforco = 0.30f;

    [Header("Depuração")]
    [SerializeField] private bool logarNoConsole = true;

    public static CommanderPlays main;

    private void Awake()
    {
        main = this;
        // O índice do Sabotador é cacheado num estático, e estático sobrevive à troca de cena.
        // Cada fase tem o próprio EnemySpawner: sem esta limpeza, entrar numa fase depois de
        // outra dirigiria sabotadores usando o índice do catálogo anterior.
        cacheIndiceSabotador = -2;
    }

    // ───────── estado da onda em curso ─────────

    private TowerBase alvoMarcado;
    private bool cercoAtivo;
    private Vector3 centroDoCerco;
    private float raioAtivoDoCerco;

    private List<int> reforcoPendente;
    private readonly List<MarcadorDeJogada> marcadores = new List<MarcadorDeJogada>();
    private bool temUltimoTipo;
    private PlayKind ultimoTipo;

    // O que ele anunciou nesta rodada, para a telemetria (ver PlaytestLogger). Fica AMARRADO ao
    // número da rodada de propósito: rodada roteirizada não passa por Ativar, e um resumo solto
    // sobreviveria a ela, creditando à rodada 17 uma marcação que aconteceu na 16. Dado errado é
    // pior que dado ausente, porque a calibragem confia nele.
    private string resumoDaOnda = "";
    private int rodadaDoResumo = -1;

    public string ResumoDaOndaEm(int rodada) => rodadaDoResumo == rodada ? resumoDaOnda : "";

    // Consultado pelo Saboteur. Devolve null quando não há marcação — ou quando a torre marcada já
    // caiu, que é o desfecho que a jogada estava buscando.
    public TowerBase AlvoMarcado => alvoMarcado != null ? alvoMarcado : null;
    public float MultiplicadorNoAlvo => multiplicadorNoAlvo;
    public float RaioDeCaca => raioDeCaca;

    // Dentro do cerco a sabotagem vira dano de ÁREA: atinge todas as torres no raio do Sabotador,
    // não só a mais próxima. É a punição direta ao amontoamento, na mesma moeda que o criou.
    public bool NoCerco(Vector3 posicao)
        => cercoAtivo && IsoGrid.CellDistance(posicao, centroDoCerco) <= raioAtivoDoCerco;

    private void OnEnable() => EnemySpawner.onWaveComplete.AddListener(Encerrar);
    private void OnDisable() => EnemySpawner.onWaveComplete.RemoveListener(Encerrar);

    // ───────── decisão (pura: não liga nada, só escolhe) ─────────

    // d: a defesa de algumas rodadas atrás, que é como o adversário decide (ver a memória, no
    //   CounterCommander). Vale para CERCO e REFORÇO: amontoamento e cobertura mudam devagar.
    // atual: o estado de agora. A MARCAÇÃO precisa dele, e não por preferência — marcar exige uma
    //   referência VIVA a uma torre, que um snapshot não guarda; decidir a proporção com números
    //   velhos e o objeto novo daria uma concentração que não corresponde a nada.
    // orcamento: o que ficou reservado para jogadas, na mesma moeda da composição (vida).
    // contagemRestante: quantos inimigos ainda cabem na onda — sabotador dirigido também ocupa
    //   vaga, senão a jogada estouraria o teto que o CounterCommander acabou de respeitar.
    // composicao: a onda já montada. O Reforço é fatiado dela, e não sorteado à parte, para não
    //   introduzir um tipo que a regra de ouro não tenha aprovado.
    public List<CommanderPlay> Decidir(DefenseSnapshot d, DefenseSnapshot atual, int orcamento,
        int contagemRestante, int rodada, List<PlanItem> composicao, List<string> bloqueadas,
        List<string> descartadas)
    {
        var escolhidas = new List<CommanderPlay>();
        if (rodada < rodadaMinima || orcamento <= 0) return escolhidas;

        var candidatas = new List<CommanderPlay>();
        int paraExecutores = Mathf.RoundToInt(orcamento * fatiaParaExecutores);

        // Quantos sabotadores a COMPOSIÇÃO já trouxe. Isto muda o que a jogada precisa comprar:
        // uma jogada dirigida é uma ORDEM, não necessariamente uma compra. Se a onda já vem com
        // executor em campo, marcar uma torre custa só o prêmio da direção.
        int jaNaOnda = SabotadoresNa(composicao);

        CommanderPlay marcacao = MontarMarcacao(atual, rodada, paraExecutores, jaNaOnda, bloqueadas);
        CommanderPlay cerco = MontarCerco(d, rodada, paraExecutores, jaNaOnda);
        CommanderPlay reforco = MontarReforco(d, composicao);
        if (marcacao != null) candidatas.Add(marcacao);
        if (cerco != null) candidatas.Add(cerco);
        if (reforco != null) candidatas.Add(reforco);

        // Penaliza repetir a jogada da rodada anterior. Não é variedade por estética: jogada que
        // se repete vira cenário e para de ser anúncio — o jogador deixa de ler a manchete porque
        // já sabe o que ela diz.
        candidatas.Sort((a, b) => Peso(b).CompareTo(Peso(a)));

        int restante = orcamento;
        int vagas = contagemRestante;
        int sabotadoresJa = SabotadoresNa(composicao);

        // Toda jogada que ele QUIS fazer e não fez vira linha no relatório, com o motivo. É a
        // mesma lição que a composição já pagou: escolha que some do log é dado de calibragem
        // perdido, e o motivo importa porque cada um pede uma correção oposta — falta de
        // orçamento é preço; falta de vaga é onda cheia; teto de sabotador é trava minha.
        foreach (var jogada in candidatas)
        {
            if (escolhidas.Count >= maximoDeJogadas)
            {
                descartadas.Add(jogada.nome + " — cabia, mas o teto é de "
                    + maximoDeJogadas + " jogada(s) por rodada");
                continue;
            }

            int precisaDeVagas = jogada.reforco.Count;
            foreach (var it in jogada.itens) precisaDeVagas += it.quantidade;

            // O teto limita quantos sabotadores as JOGADAS ACRESCENTAM, e por isso só se aplica
            // a quem acrescenta algum.
            //
            // A versão anterior comparava o teto com o total da onda, e isso matava a Fase D
            // inteira em silêncio: contra defesa amontoada a composição compra dezenas de
            // sabotadores (é o counter de maior eficácia ali), então "já tem 55, teto 5" barrava
            // até o REFORÇO, que não usa sabotador nenhum. Medido numa partida real: nove rodadas
            // do Contra-Comandante, zero jogadas dirigidas. A trava impedia a jogada exatamente
            // no cenário para o qual ela foi desenhada.
            int sabotadoresDaJogada = SabotadoresNa(jogada.itens);
            if (sabotadoresDaJogada > 0 && sabotadoresJa + sabotadoresDaJogada > maximoDeSabotadores)
            {
                descartadas.Add(jogada.nome + " — acrescentaria " + sabotadoresDaJogada
                    + " sabotadores sobre os " + sabotadoresJa + " da onda"
                    + " (teto " + maximoDeSabotadores + ")");
                continue;
            }

            // O REFORÇO É APARADO em vez de descartado inteiro. As outras duas são indivisíveis
            // (metade de uma marcação não marca nada), mas uma leva menor continua sendo uma leva
            // — e a alternativa era o reforço quase nunca acontecer, porque ele é a jogada mais
            // cara e chega por último na fila da eficácia.
            if (jogada.tipo == PlayKind.Reforco && (jogada.custo > restante || precisaDeVagas > vagas))
            {
                Aparar(jogada, restante, vagas);
                precisaDeVagas = jogada.reforco.Count;
                if (jogada.reforco.Count == 0)
                {
                    descartadas.Add("REFORÇO — não sobrou nem orçamento ($" + restante
                        + ") nem vaga (" + vagas + ") para uma leva mínima");
                    continue;
                }
            }

            if (jogada.custo > restante || precisaDeVagas > vagas)
            {
                descartadas.Add(jogada.nome + " — custaria $" + jogada.custo + " e "
                    + precisaDeVagas + " vaga(s); sobraram $" + restante + " e " + vagas);
                continue;
            }

            restante -= jogada.custo;
            vagas -= precisaDeVagas;
            sabotadoresJa += sabotadoresDaJogada;
            escolhidas.Add(jogada);
        }

        return escolhidas;
    }

    private float Peso(CommanderPlay j)
        => j.eficacia - (temUltimoTipo && ultimoTipo == j.tipo ? 0.25f : 0f);

    // ───────── as três jogadas ─────────

    private CommanderPlay MontarMarcacao(DefenseSnapshot d, int rodada, int paraExecutores,
        int jaNaOnda, List<string> bloqueadas)
    {
        if (!SabotadorDisponivel(rodada)) return null;

        int investido;
        TowerBase alvo = TorreMaisCara(out investido);
        if (alvo == null || investido <= 0 || d.investedTotal <= 0) return null;

        // Um pilar só é pilar se concentra dinheiro. Marcar a "mais cara" de uma defesa toda igual
        // não diz nada e ainda gasta orçamento que faria falta na composição.
        float concentracao = Mathf.Clamp01((float)investido / d.investedTotal);
        float eficacia = Mathf.Clamp01(concentracao / 0.35f);
        if (eficacia < 0.2f) return null;

        // REGRA DE OURO. A resposta a esta jogada é matar os sabotadores antes que cheguem — o que
        // exige que ALGUMA torre que atire alcance a torre marcada. Marcar uma Farm isolada num
        // canto sem cobertura é executar, não desafiar.
        if (!CobertaPorAlgumaArma(alvo))
        {
            bloqueadas.Add("MARCAÇÃO em " + NomeDe(alvo)
                + " — nenhuma torre sua alcança aquele ponto para defendê-la");
            return null;
        }

        var j = new CommanderPlay();
        j.tipo = PlayKind.Marcacao;
        j.nome = "MARCAÇÃO";
        j.eficacia = eficacia;
        j.alvo = alvo;
        j.manchete = "MARCOU sua " + NomeDe(alvo);
        j.razao = Mathf.RoundToInt(concentracao * 100f) + "% do seu dinheiro está nesta torre";

        ComprarSabotadores(j, paraExecutores, jaNaOnda);
        // Sem executor NENHUM — nem comprado, nem já vindo na composição — a jogada seria só um
        // anúncio sem consequência, que é pior que não anunciar nada.
        if (j.itens.Count == 0 && jaNaOnda == 0) return null;
        j.custo += Mathf.RoundToInt(investido * premioDaMarcacao);
        return j;
    }

    private CommanderPlay MontarCerco(DefenseSnapshot d, int rodada, int paraExecutores, int jaNaOnda)
    {
        if (!SabotadorDisponivel(rodada)) return null;

        // Sem amontoado não há cerco: o dano em área não teria nada a mais para atingir, e a
        // jogada seria uma marcação pior e mais cara.
        if (d.densestCount < 2) return null;

        var j = new CommanderPlay();
        j.tipo = PlayKind.Cerco;
        j.nome = "CERCO";
        j.eficacia = Mathf.Clamp01((d.densestCount - 1) / 3f);
        j.centro = d.densestPoint;
        j.raio = raioDoCerco;
        j.manchete = "CERCOU o seu muro";
        j.razao = d.densestCount + " torres cobrem o mesmo ponto: ali a sabotagem pega todas de uma vez";

        ComprarSabotadores(j, paraExecutores, jaNaOnda);
        if (j.itens.Count == 0 && jaNaOnda == 0) return null;
        j.custo += Mathf.RoundToInt(d.investedTotal * premioDoCerco);
        return j;
    }

    private CommanderPlay MontarReforco(DefenseSnapshot d, List<PlanItem> composicao)
    {
        if (composicao == null || composicao.Count == 0) return null;

        var j = new CommanderPlay();
        j.tipo = PlayKind.Reforco;
        j.nome = "REFORÇO";
        // Vale mais contra defesa BOA. Uma defesa com o traçado inteiro coberto não é exigida por
        // volume — ela é exigida por atenção, que é justamente o que a leva do meio cobra.
        j.eficacia = 0.35f + 0.4f * d.pathCoverage;
        j.manchete = "GUARDOU reforço para o meio da rodada";
        j.razao = "sua cobertura é de " + Mathf.RoundToInt(d.pathCoverage * 100f)
            + "%: o que dói não é o volume, é chegar quando você já parou de olhar";

        // A leva é uma FATIA da própria composição, não um sorteio novo: assim ela não pode
        // introduzir um tipo que a regra de ouro não aprovou lá atrás.
        foreach (var it in composicao)
        {
            if (it.quantidade <= 0) continue;
            int unitario = Mathf.Max(1, it.custo / it.quantidade);
            int quantos = Mathf.Max(1, Mathf.RoundToInt(it.quantidade * fatiaDoReforco));
            for (int i = 0; i < quantos; i++) j.reforco.Add(it.prefabIndex);
            j.custo += quantos * unitario;
        }
        return j.reforco.Count > 0 ? j : null;
    }

    // Corta a leva do reforço até caber no orçamento e nas vagas que sobraram. Tira do fim, que é
    // onde estão os tipos de menor eficácia — a composição vem ordenada por ela.
    private static void Aparar(CommanderPlay j, int orcamento, int vagas)
    {
        var prefabs = EnemySpawner.main != null ? EnemySpawner.main.EnemyPrefabs : null;
        if (prefabs == null) { j.reforco.Clear(); j.custo = 0; return; }

        while (j.reforco.Count > 0 && (j.custo > orcamento || j.reforco.Count > vagas))
        {
            int ultimo = j.reforco.Count - 1;
            int idx = j.reforco[ultimo];
            j.reforco.RemoveAt(ultimo);

            int unitario = 1;
            if (idx >= 0 && idx < prefabs.Count && prefabs[idx] != null)
                unitario = CounterCommander.PrecoDeAmeaca(prefabs[idx].GetComponent<Health>());
            j.custo = Mathf.Max(0, j.custo - unitario);
        }
    }

    // ───────── ativação (aqui, sim, o jogo muda) ─────────

    public void Ativar(List<CommanderPlay> jogadas, int rodada)
    {
        Encerrar();
        rodadaDoResumo = rodada;
        resumoDaOnda = "";
        if (jogadas == null || jogadas.Count == 0) return;

        foreach (var j in jogadas)
        {
            resumoDaOnda += (resumoDaOnda.Length > 0 ? " + " : "") + j.nome;

            temUltimoTipo = true;
            ultimoTipo = j.tipo;

            switch (j.tipo)
            {
                case PlayKind.Marcacao:
                    if (j.alvo == null) break;
                    alvoMarcado = j.alvo;
                    marcadores.Add(MarcadorDeJogada.Criar(j.alvo.transform.position, j.alvo.transform,
                        1.2f, "ALVO", new Color(1f, 0.25f, 0.25f)));
                    break;

                case PlayKind.Cerco:
                    cercoAtivo = true;
                    centroDoCerco = j.centro;
                    raioAtivoDoCerco = j.raio;
                    marcadores.Add(MarcadorDeJogada.Criar(j.centro, null, j.raio,
                        "CERCO", new Color(1f, 0.45f, 0.15f)));
                    break;

                case PlayKind.Reforco:
                    reforcoPendente = new List<int>(j.reforco);
                    break;
            }
            if (logarNoConsole) Debug.Log("[jogada] " + j.nome + " — " + j.razao + "  (custo " + j.custo + ")");
        }
    }

    public void Encerrar()
    {
        alvoMarcado = null;
        cercoAtivo = false;
        reforcoPendente = null;
        foreach (var m in marcadores) if (m != null) m.Encerrar();
        marcadores.Clear();
    }

    private void Update()
    {
        if (reforcoPendente == null) return;

        EnemySpawner spawner = EnemySpawner.main;
        if (spawner == null || !spawner.IsWaveActive) return;
        if (spawner.ProgressoDeSpawn < gatilhoDoReforco) return;

        List<int> leva = reforcoPendente;
        reforcoPendente = null;   // zera ANTES de injetar: a injeção mexe no progresso da onda
        spawner.InjetarReforco(leva);

        // O anúncio na hora é metade da jogada. Sem ele o reforço é indistinguível de "a onda era
        // maior do que eu achava", e o jogador aprende a coisa errada.
        Vector3 onde = Vector3.zero;
        if (LevelManager.main != null && LevelManager.main.startPoint != null)
            onde = LevelManager.main.startPoint.position;
        FloatingText.Spawn(onde, "REFORÇO!  +" + leva.Count, new Color(1f, 0.55f, 0.2f), 2f);
        AudioManager.Cue(AudioManager.Sfx.Explosion);
        if (logarNoConsole) Debug.Log("[jogada] REFORÇO injetado: " + leva.Count + " inimigos");
    }

    // ───────── apoio ─────────

    private static int SabotadoresNa(List<PlanItem> itens)
    {
        if (itens == null) return 0;
        int idx = IndiceDoSabotador();
        if (idx < 0) return 0;
        int n = 0;
        foreach (var it in itens)
            if (it.prefabIndex == idx) n += it.quantidade;
        return n;
    }

    // Compra só o que FALTA de executor, contando o que a composição já pôs em campo.
    //
    // A jogada dirigida é uma ordem, não uma compra: se a onda já traz sabotadores, marcar uma
    // torre não precisa de mais nenhum — precisa que os que já vêm saibam para onde ir. Comprar
    // por cima era o que fazia a marcação estourar o teto e se autoeliminar.
    private void ComprarSabotadores(CommanderPlay j, int paraExecutores, int jaNaOnda)
    {
        int idx = IndiceDoSabotador();
        if (idx < 0 || EnemySpawner.main == null) return;

        int faltam = sabotadoresPorJogada - jaNaOnda;
        if (faltam <= 0) return;                                  // a onda já tem executor
        int cabe = maximoDeSabotadores - jaNaOnda;
        if (cabe <= 0) return;                                    // já passou do teto: só dirige

        Health h = EnemySpawner.main.EnemyPrefabs[idx].GetComponent<Health>();
        if (h == null) return;

        int unitario = CounterCommander.PrecoDeAmeaca(h);
        int quantos = Mathf.Min(Mathf.Max(faltam, paraExecutores / Mathf.Max(1, unitario)), cabe);
        if (quantos <= 0) return;

        var item = new PlanItem();
        item.prefabIndex = idx;
        item.quantidade = quantos;
        item.custo = quantos * unitario;
        item.nome = "Sabotador dirigido";
        item.razao = j.razao;

        j.itens.Add(item);
        j.custo += item.custo;
    }

    private bool SabotadorDisponivel(int rodada)
    {
        int idx = IndiceDoSabotador();
        if (idx < 0) return false;
        // Não dirige o que o roteiro ainda não apresentou: a primeira vez que o jogador vê um
        // Sabotador não pode ser a vez em que ele vem mirado numa torre específica.
        return CounterCommander.main != null && CounterCommander.main.PrimeiraAparicao(idx) <= rodada;
    }

    private static int cacheIndiceSabotador = -2;   // -2 = ainda não procurou, -1 = não existe
    private static int IndiceDoSabotador()
    {
        if (cacheIndiceSabotador != -2) return cacheIndiceSabotador;
        if (EnemySpawner.main == null) return -1;    // sem cache: o spawner ainda vai existir

        cacheIndiceSabotador = -1;
        var prefabs = EnemySpawner.main.EnemyPrefabs;
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] != null && prefabs[i].GetComponent<Saboteur>() != null)
            { cacheIndiceSabotador = i; break; }
        }
        return cacheIndiceSabotador;
    }

    private static TowerBase TorreMaisCara(out int investido)
    {
        TowerBase melhor = null;
        investido = 0;
        foreach (TowerBase t in TowerBase.Todas)
        {
            TowerValue v = t.GetComponentInParent<TowerValue>();
            int i = v != null ? v.Invested : 0;
            if (i > investido) { investido = i; melhor = t; }
        }
        return melhor;
    }

    // Alguma torre que ATIRA alcança este ponto? (A própria conta: torre ofensiva se defende.)
    private static bool CobertaPorAlgumaArma(TowerBase alvo)
    {
        foreach (TowerBase t in TowerBase.Todas)
        {
            if (t.CurrentRange <= 0f) continue;                          // Gerador não cobre nada
            if (DefenseReadout.KindOf(t) == DamageKind.Economy) continue;
            if (DefenseReadout.KindOf(t) == DamageKind.Support) continue; // Detector marca, não mata
            if (IsoGrid.CellDistance(t.transform.position, alvo.transform.position) <= t.CurrentRange)
                return true;
        }
        return false;
    }

    private static string NomeDe(TowerBase t)
        => t.gameObject.name.Replace("(Clone)", "").Replace(" Tower", "").Trim();
}
