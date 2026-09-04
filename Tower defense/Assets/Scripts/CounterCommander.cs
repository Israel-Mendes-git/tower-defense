using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Uma opção de compra do adversário: um tipo de inimigo, o que ele custa, contra que defesa
// ele funciona, e o que o jogador precisa ter acesso para poder respondê-lo.
public class ThreatOption
{
    public int prefabIndex;
    public string nome;
    public int preco;

    // Quanto este inimigo explora ESTA defesa, de 0 a 1. É o julgamento do adversário.
    public float eficacia;
    public string razao;

    // REGRA DE OURO: pelo menos um destes tipos precisa estar ao alcance do jogador (comprável
    // agora, ou já presente na defesa). Vazio = não exige nada de especial.
    public DamageKind[] respostasPossiveis = new DamageKind[0];
    public string exigencia = "";
}

// Uma compra fechada: tantos inimigos de um tipo, por tanto, por este motivo.
public class PlanItem
{
    public int prefabIndex;
    public int quantidade;
    public int custo;
    public string nome;
    public string razao;
}

// O que o adversário faria nesta rodada. Em modo seco isto é só um relatório; com poder, os
// itens viram a fila de spawn da rodada.
public class CounterPlan
{
    public int wave;
    public int orcamento;
    public int gasto;
    public readonly List<PlanItem> itens = new List<PlanItem>();
    public readonly List<string> compras = new List<string>();
    public readonly List<string> descartadas = new List<string>();
    public readonly List<string> bloqueadas = new List<string>();
    public string leitura = "";
    public bool seco;

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Contra-Comandante, rodada " + wave
            + (seco ? " (MODO SECO — nada foi aplicado) ===" : " (ONDA APLICADA) ==="));
        sb.AppendLine("leitura: " + leitura);
        sb.AppendLine("orçamento: $" + orcamento + "   gastaria: $" + gasto);
        sb.AppendLine("compraria:");
        if (compras.Count == 0) sb.AppendLine("   (nada — orçamento insuficiente ou sem alvo bom)");
        foreach (var c in compras) sb.AppendLine("   " + c);
        if (bloqueadas.Count > 0)
        {
            sb.AppendLine("barrado pela regra de ouro (o jogador não teria como responder):");
            foreach (var b in bloqueadas) sb.AppendLine("   " + b);
        }
        if (descartadas.Count > 0)
        {
            sb.AppendLine("descartado por baixa eficácia:");
            foreach (var d in descartadas) sb.AppendLine("   " + d);
        }
        return sb.ToString();
    }
}

// O CÉREBRO. Lê o retrato da defesa (DefenseReadout) e decide que composição compraria.
//
// MODO SECO: nesta fase ele NÃO gasta, NÃO altera a onda e NÃO tem poder nenhum — só relata.
// É de propósito: dá para julgar a qualidade das decisões dele contra partidas reais antes de
// deixá-lo mexer no jogo. Quando ganhar poder, o ponto de entrada é EnemySpawner.StartWave,
// onde WaveScript.BuildQueue devolve null (rodadas roteirizadas seguem intocadas).
public class CounterCommander : MonoBehaviour
{
    [Header("Orçamento")]
    [SerializeField] private int orcamentoBase = 40;
    [SerializeField] private int orcamentoPorRodada = 75;

    // Indexação ao poder do jogador. O EXPOENTE SUBLINEAR é o que impede rubber banding
    // punitivo: ficar mais forte sempre compensa, porque a ameaça cresce mais devagar que a
    // defesa. Com 0,7, dobrar o investimento aumenta a ameaça em ~62%, não em 100%.
    [SerializeField] private float fatorInvestimento = 1.0f;
    [SerializeField] private float expoenteInvestimento = 0.7f;

    [Header("Travas")]
    // Nenhum tipo pode passar desta fatia do orçamento. Sem isto ele compraria 100% do melhor
    // counter e a rodada viraria um muro de um tipo só.
    [SerializeField, Range(0.1f, 1f)] private float tetoPorTipo = 0.4f;
    [SerializeField, Range(0f, 1f)] private float eficaciaMinima = 0.15f;

    // Teto de CONTAGEM, como múltiplo do que a curva normal pediria naquela rodada.
    // Sem ele, as rodadas iniciais viram enxames absurdos: só existem tipos de 1 a 2 de vida
    // por lá, então um orçamento medido em vida compra centenas de unidades (medido: 484
    // inimigos na rodada 10). Vida é a moeda certa para medir ameaça, mas contagem é o que
    // decide se a rodada é jogável — e desenhável.
    [SerializeField, Range(0.5f, 3f)] private float tetoDeContagem = 1.25f;

    // Quantas rodadas atrás ele lê a defesa. Trava anti-frustração: com 0 ele reagiria à compra
    // que o jogador acabou de fazer, transformando cada acerto em desperdício.
    [SerializeField, Range(0, 4)] private int memoriaEmRodadas = 2;

    [Header("Modo")]
    // Modo seco: ele decide e relata, mas NÃO monta a onda. Desligue para dar poder a ele.
    [SerializeField] private bool modoSeco = false;
    [SerializeField] private bool logarNoConsole = true;

    public bool ModoSeco => modoSeco;

    public static CounterCommander main;
    private CounterPlan ultimo;
    public CounterPlan Ultimo => ultimo;

    private void Awake() => main = this;

    private void OnEnable() => EnemySpawner.onWaveComplete.AddListener(Planejar);
    private void OnDisable() => EnemySpawner.onWaveComplete.RemoveListener(Planejar);

    // Relatório de fim de rodada: planeja a PRÓXIMA, que é a que ele montaria em seguida.
    public void Planejar()
    {
        if (EnemySpawner.main == null) return;
        ultimo = Planear(EnemySpawner.main.CurrentWave + 1);
        if (logarNoConsole && ultimo != null) Debug.Log(ultimo.Describe());
    }

    // Fila de spawn da rodada, montada a partir do plano. Devolve null quando ele não deve (ou
    // não tem o que) montar — nesse caso o EnemySpawner segue com o sorteio por peso de sempre.
    //
    // Chamado pelo EnemySpawner APENAS onde WaveScript.BuildQueue devolveu null: as rodadas
    // roteirizadas continuam intocadas, servindo de âncora de ritmo e de apresentação de tipos.
    public List<int> BuildQueue(int wave)
    {
        if (modoSeco) return null;

        // A rodada vem de fora: o EnemySpawner chama isto DENTRO do StartWave, quando
        // currentWave já é a rodada que vai começar. Calcular "CurrentWave + 1" aqui dava um
        // off-by-one e orçava a onda errada.
        ultimo = Planear(wave);
        if (logarNoConsole) Debug.Log(ultimo.Describe());
        if (ultimo.itens.Count == 0) return null;

        int total = 0;
        foreach (var it in ultimo.itens) total += it.quantidade;
        if (total <= 0) return null;

        // Intercala proporcionalmente em vez de despejar tipo por tipo: 20 camuflados seguidos
        // e depois 15 chumbos seriam dois muros em sequência, não uma rodada. A cada passo entra
        // o tipo que está mais "atrasado" em relação à própria cota — espalha os raros ao longo
        // da onda inteira sem amontoar os comuns no fim.
        var fila = new List<int>(total);
        var colocados = new int[ultimo.itens.Count];
        for (int passo = 0; passo < total; passo++)
        {
            int melhor = -1;
            float piorRazao = float.MaxValue;
            for (int i = 0; i < ultimo.itens.Count; i++)
            {
                if (colocados[i] >= ultimo.itens[i].quantidade) continue;
                float razao = (float)colocados[i] / ultimo.itens[i].quantidade;
                if (razao < piorRazao) { piorRazao = razao; melhor = i; }
            }
            if (melhor < 0) break;
            fila.Add(ultimo.itens[melhor].prefabIndex);
            colocados[melhor]++;
        }
        return fila;
    }

    // Transparência mínima, na manchete que a HUD já mostra: o jogador precisa saber que a onda
    // foi montada CONTRA ele e por quê. Um adversário que reage em segredo é indistinguível de
    // dificuldade injusta — a diferença entre aprender e se sentir roubado é ver o motivo.
    public string MancheteDaOnda()
    {
        if (ultimo == null || ultimo.itens.Count == 0) return null;
        PlanItem principal = ultimo.itens[0]; // a lista é preenchida em ordem de eficácia
        return "ele mandou " + principal.nome + " — " + principal.razao;
    }

    public CounterPlan Planear(int rodadaAlvo)
    {
        var plano = new CounterPlan();
        plano.seco = modoSeco;
        if (DefenseReadout.main == null || EnemySpawner.main == null) return plano;

        // Duas leituras diferentes, de propósito:
        //
        // `d` é a defesa de ALGUMAS RODADAS ATRÁS e decide o que explorar. Reagir ao que o
        // jogador acabou de comprar puniria justamente quem acertou a resposta — ele compra o
        // Detector e na mesma rodada o adversário para de mandar camuflado, o que faz o dinheiro
        // gasto parecer desperdício.
        //
        // `atual` é o estado de agora e vale só para a regra de ouro: se ele PODE responder,
        // isso tem que ser medido no presente, senão barra (ou libera) por uma foto vencida.
        DefenseSnapshot d = DefenseReadout.main.Antigo(memoriaEmRodadas);
        DefenseSnapshot atual = DefenseReadout.main.Ler();
        int proximaRodada = Mathf.Max(1, rodadaAlvo);
        plano.wave = proximaRodada;
        plano.leitura = Resumir(d);
        plano.orcamento = Orcamento(d, proximaRodada);

        var opcoes = Avaliar(d, proximaRodada);

        // Ordena por EFICÁCIA pura, não por custo-benefício.
        //
        // Custo-benefício (eficácia ÷ preço) parece a conta certa e não é: os inimigos baratos
        // ganham sempre, e o adversário enche a rodada de lixo barato em vez de trazer a resposta
        // que realmente dói. Medido: contra uma defesa 69% explosiva ele comprava 23 Ladrões e
        // gastava o orçamento antes de chegar na Cerâmica, que era a compra óbvia. Prioridade é
        // "o que explora melhor esta defesa"; o teto por tipo já impede a monocultura.
        opcoes.Sort(delegate (ThreatOption a, ThreatOption b)
        {
            return b.eficacia.CompareTo(a.eficacia);
        });

        var gastoPorTipo = new Dictionary<int, int>();
        var contagemPorTipo = new Dictionary<int, int>();
        int restante = plano.orcamento;
        int contagemTotal = Mathf.Max(1, Mathf.RoundToInt(
            EnemySpawner.main.EnemiesPerWaveFor(proximaRodada) * tetoDeContagem));
        int contagemRestante = contagemTotal;

        // O teto por tipo precisa valer em CONTAGEM também, não só em orçamento.
        // Medido: contra defesa sem detecção, o Camo (que custa 4 de vida) cabia inteiro dentro
        // dos 40% de orçamento e sozinho esgotava a onda — 180 camuflados e mais nada. A trava
        // existia, mas na moeda errada.
        int tetoContagemDoTipo = Mathf.Max(1, Mathf.RoundToInt(contagemTotal * tetoPorTipo));

        foreach (var o in opcoes)
        {
            if (o.eficacia < eficaciaMinima)
            {
                plano.descartadas.Add(o.nome + " — " + o.razao);
                continue;
            }
            if (!RespostaAoAlcance(o, atual))
            {
                plano.bloqueadas.Add(o.nome + " — exige " + o.exigencia + ", que o jogador não tem como comprar agora");
                continue;
            }

            int tetoDoTipo = Mathf.RoundToInt(plano.orcamento * tetoPorTipo);
            int jaGasto = gastoPorTipo.ContainsKey(o.prefabIndex) ? gastoPorTipo[o.prefabIndex] : 0;
            int podeGastar = Mathf.Min(restante, tetoDoTipo - jaGasto);
            int jaColocado = contagemPorTipo.ContainsKey(o.prefabIndex) ? contagemPorTipo[o.prefabIndex] : 0;
            int cabeDesteTipo = Mathf.Min(contagemRestante, tetoContagemDoTipo - jaColocado);
            int quantidade = Mathf.Min(podeGastar / Mathf.Max(1, o.preco), cabeDesteTipo);
            if (quantidade <= 0)
            {
                // Registra em vez de sumir: uma escolha que o adversário QUERIA fazer e não
                // coube é informação de balanceamento, não ruído. Foi assim que a Cerâmica
                // desapareceu do relatório contra uma defesa 69% explosiva. E distingue os dois
                // motivos, que pedem correções opostas: falta de orçamento é calibragem de
                // preço; falta de espaço é a onda já estar cheia.
                string motivo = cabeDesteTipo <= 0
                    ? "a onda já está cheia (" + contagemTotal + " inimigos)"
                    : "sobraram só $" + restante + " e cada um custa $" + o.preco;
                plano.descartadas.Add(o.nome + " — queria comprar (" + Mathf.RoundToInt(o.eficacia * 100f)
                    + "% de eficácia) mas " + motivo);
                continue;
            }

            int custo = quantidade * o.preco;
            restante -= custo;
            contagemRestante -= quantidade;
            gastoPorTipo[o.prefabIndex] = jaGasto + custo;
            contagemPorTipo[o.prefabIndex] = jaColocado + quantidade;
            plano.gasto += custo;
            plano.compras.Add(quantidade + "x " + o.nome + "  $" + custo + "  — " + o.razao);

            var item = new PlanItem();
            item.prefabIndex = o.prefabIndex;
            item.quantidade = quantidade;
            item.custo = custo;
            item.nome = o.nome;
            item.razao = o.razao;
            plano.itens.Add(item);
        }

        return plano;
    }

    private int Orcamento(DefenseSnapshot d, int rodada)
    {
        float porPoder = fatorInvestimento * Mathf.Pow(Mathf.Max(0, d.investedTotal), expoenteInvestimento);
        return Mathf.RoundToInt(orcamentoBase + orcamentoPorRodada * rodada + porPoder);
    }

    private static string Resumir(DefenseSnapshot d)
    {
        var sb = new StringBuilder();
        sb.Append("investido $" + d.investedTotal + " em " + d.towerCount + " torres");
        foreach (var kv in d.investShare)
            if (kv.Value >= 0.10f) sb.Append(", " + Mathf.RoundToInt(kv.Value * 100f) + "% " + kv.Key);
        sb.Append("; cobertura " + Mathf.RoundToInt(d.pathCoverage * 100f) + "%");
        sb.Append(", camo " + Mathf.RoundToInt(d.camoCoverage * 100f) + "%");
        sb.Append(", sobreposição " + d.averageOverlap.ToString("0.0"));
        sb.Append(", " + d.uncoveredStretches + " buraco(s)");
        return sb.ToString();
    }

    private float Share(DefenseSnapshot d, DamageKind k)
    {
        float v;
        return d.investShare.TryGetValue(k, out v) ? v : 0f;
    }

    // O julgamento. Cada regra olha um traço REAL do inimigo (não o nome do prefab, para que
    // inimigo novo entre no raciocínio sozinho) e mede o quanto ele explora esta defesa.
    private List<ThreatOption> Avaliar(DefenseSnapshot d, int rodada)
    {
        var saida = new List<ThreatOption>();
        var prefabs = EnemySpawner.main.EnemyPrefabs;
        if (prefabs == null) return saida;

        // Dano que atravessa armadura/blindagem: quem tem pouco disso sofre com alvo duro.
        float perfurante = Share(d, DamageKind.Piercing) + Share(d, DamageKind.Explosive);
        float amontoado = Mathf.Clamp01((d.averageOverlap - 1f) / 3f);   // 1 torre/ponto = 0, 4+ = 1
        float descoberto = 1f - d.pathCoverage;

        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject pf = prefabs[i];
            if (pf == null) continue;

            // Não compra o que o roteiro ainda não apresentou: a primeira vez que o jogador vê
            // um tipo já é dificuldade suficiente sem vir em massa.
            if (PrimeiraAparicao(i) > rodada) continue;

            Health h = pf.GetComponent<Health>();
            if (h == null) continue;

            var o = new ThreatOption();
            o.prefabIndex = i;
            o.nome = pf.name.Replace(" Enemy", "").Replace("(Clone)", "");

            // PREÇO ∝ AMEAÇA (vida), não a recompensa.
            //
            // A primeira versão usava CurrencyWorth, pela simetria bonita de "o que você ganha
            // é o que ele pagou". Medido, não funciona: a recompensa é SUBLINEAR em vida de
            // propósito (ver ROADMAP 2.1, para a economia do jogador não inflar), então 200
            // Enemies de 1 de vida custavam o mesmo que 12 MOABs e entregavam 12x menos ameaça.
            // Ele enchia a onda de lixo barato por construção. Com preço proporcional à vida, o
            // orçamento passa a ser, literalmente, quanta vida ele coloca em campo.
            //
            // O acréscimo de 40% no que exige resposta específica é o custo da qualidade: trazer
            // counter em vez de volume significa trazer MENOS inimigos, o que é a troca certa.
            bool exigeResposta = h.IsCamo || h.LeadArmor || h.BlastArmor;
            o.preco = Mathf.Max(1, Mathf.RoundToInt(h.GetHitPoints() * (exigeResposta ? 1.4f : 1f)));

            // --- as regras, da mais específica para a mais genérica ---
            if (h.IsCamo)
            {
                o.eficacia = 1f - d.camoCoverage;
                o.razao = "sua detecção cobre " + Mathf.RoundToInt(d.camoCoverage * 100f) + "% do traçado";
                o.respostasPossiveis = new DamageKind[] { DamageKind.Support };
                o.exigencia = "detecção de camuflado";
            }
            else if (h.LeadArmor)
            {
                o.eficacia = Share(d, DamageKind.Sharp);
                o.razao = Mathf.RoundToInt(Share(d, DamageKind.Sharp) * 100f) + "% do seu dano é cortante e não fura chumbo";
                o.respostasPossiveis = new DamageKind[] { DamageKind.Explosive, DamageKind.Energy, DamageKind.Piercing, DamageKind.Fire };
                o.exigencia = "dano não-cortante";
            }
            else if (h.BlastArmor)
            {
                o.eficacia = Share(d, DamageKind.Explosive);
                o.razao = Mathf.RoundToInt(Share(d, DamageKind.Explosive) * 100f) + "% do seu dano é explosivo e a cerâmica ignora explosão";
                o.respostasPossiveis = new DamageKind[] { DamageKind.Sharp, DamageKind.Energy, DamageKind.Piercing, DamageKind.Fire };
                o.exigencia = "dano não-explosivo";
            }
            // Os anti-torre têm teto de eficácia abaixo de 1 de propósito: desligar uma torre por
            // alguns segundos ou roubar caixa INCOMODA, enquanto uma imunidade ANULA parte da
            // defesa. Sem esse teto os dois dominam a ordenação e o adversário deixa de trazer
            // as respostas que realmente mudam a rodada.
            else if (pf.GetComponent<Saboteur>() != null)
            {
                o.eficacia = 0.85f * amontoado;
                o.razao = "suas torres estão amontoadas (" + d.averageOverlap.ToString("0.0") + " por ponto): desligar uma dói";
            }
            else if (pf.GetComponent<Thief>() != null)
            {
                o.eficacia = 0.7f * Mathf.Clamp01(d.currency / 3000f);
                o.razao = "você está sentado em $" + d.currency;
            }
            else if (pf.GetComponent<Shielder>() != null)
            {
                o.eficacia = 1f - Mathf.Clamp01(perfurante);
                o.razao = "apenas " + Mathf.RoundToInt(perfurante * 100f) + "% do seu dano atravessa armadura"
                    + (perfurante >= 0.5f ? " — escudo atrapalharia pouco" : ": um escudo segura o resto");
                o.respostasPossiveis = new DamageKind[] { DamageKind.Piercing, DamageKind.Explosive };
                o.exigencia = "dano que atravessa armadura";
            }
            else if (h.GetHitPoints() >= 40)
            {
                // Alvo gordo: exige foco concentrado. Defesa espalhada sofre.
                o.eficacia = 1f - amontoado;
                o.razao = "alvo duro (" + h.GetHitPoints() + " de vida) contra defesa espalhada";
                o.respostasPossiveis = new DamageKind[] { DamageKind.Piercing, DamageKind.Explosive, DamageKind.Energy };
                o.exigencia = "dano concentrado";
            }
            else
            {
                o.eficacia = descoberto;
                o.razao = Mathf.RoundToInt(descoberto * 100f) + "% do traçado está fora de alcance";
            }

            saida.Add(o);
        }
        return saida;
    }

    // Em que rodada o jogador VÊ este tipo pela primeira vez.
    //
    // Não basta olhar enemyUnlocks: Lead, Ceramic e MOAB não estão na lista de sorteio e só
    // existem dentro de rodadas roteirizadas do WaveScript (chumbo na 11, cerâmica na 17, MOAB
    // na 27). Ignorar o roteiro deixaria o adversário cego justamente para os três tipos que
    // carregam imunidade — os mais interessantes de comprar.
    private Dictionary<int, int> cacheAparicao;
    private int PrimeiraAparicao(int prefabIndex)
    {
        if (cacheAparicao == null)
        {
            cacheAparicao = new Dictionary<int, int>();
            int n = EnemySpawner.main.EnemyPrefabs.Count;
            int maxR = Mathf.Max(1, EnemySpawner.main.MaxRounds);

            for (int i = 0; i < n; i++)
            {
                int viaUnlock = EnemySpawner.main.FirstWaveOf(i);
                cacheAparicao[i] = viaUnlock;
            }
            for (int w = 1; w <= maxR; w++)
            {
                var fila = WaveScript.BuildQueue(w, n, 8 * w);
                if (fila == null) continue;
                foreach (int idx in fila)
                    if (idx >= 0 && idx < n && w < cacheAparicao[idx]) cacheAparicao[idx] = w;
            }
        }
        int r;
        return cacheAparicao.TryGetValue(prefabIndex, out r) ? r : int.MaxValue;
    }

    // A REGRA DE OURO. Só é justo cobrar uma resposta que o jogador consegue dar: ou ele já tem
    // aquele tipo na defesa, ou consegue comprá-lo agora. Sem isto, "você não tem resposta a
    // chumbo" pode significar "o jogo não te deu a peça" — o que é armadilha, não dificuldade.
    private bool RespostaAoAlcance(ThreatOption o, DefenseSnapshot d)
    {
        if (o.respostasPossiveis == null || o.respostasPossiveis.Length == 0) return true;
        foreach (var k in o.respostasPossiveis)
        {
            if (d.affordableKinds.Contains(k)) return true;
            if (Share(d, k) > 0.01f) return true; // já tem em campo
        }
        return false;
    }
}
