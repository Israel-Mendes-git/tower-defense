using System.Collections.Generic;
using UnityEngine;

// Roteiro das rodadas marcantes. Sorteio puro por peso produz ondas mornas e intercambiáveis;
// aqui as rodadas que importam têm composição fixa, para que a progressão tenha momentos que o
// jogador lembra ("a rodada 10 é a do chefe") e para que cada ameaça nova seja apresentada sozinha,
// em pouca quantidade, antes de aparecer misturada.
public static class WaveScript
{
    // Índices no array enemyPrefabs do EnemySpawner.
    public const int Enemy = 0, Fast = 1, Tank = 2, TwoTank = 3, Trojan = 4,
                     Camo = 5, Armored = 6, Regen = 7, Boss = 8,
                     Lead = 9, Ceramic = 10, Moab = 11,
                     Saboteur = 12, Thief = 13, Shielder = 14;

    // As unidades AUTORAIS: nunca acompanham o tamanho da onda, em nenhuma quantidade. O corte
    // não é arbitrário — é onde o valor delas salta na tabela de recompensa (Ceramic 20, Boss 34,
    // MOAB 68), e é o mesmo salto que faz cada cópia extra virar dinheiro no bolso do jogador.
    private static bool Marcante(int prefab) => prefab == Boss || prefab == Moab;

    // Teto de quanto um grupo escalável pode crescer.
    //
    // Sem teto, o fator é (tamanhoDaOnda − fixos) ÷ escaláveis, e como a curva de contagem é
    // LINEAR na rodada (8 × rodada, ver EnemySpawner), ele cresce sem limite: 10,2 na rodada 40.
    // Uma composição escrita com 15 cerâmicas passa a colocar 153 em campo, o que não é a mesma
    // rodada mais forte — é outra rodada. O teto preserva a intenção do roteiro; quem faz a
    // rodada crescer em ameaça é a qualidade da composição, não a fotocópia dela.
    private const float TetoDeEscalonamento = 6f;

    private struct Group
    {
        public int prefab, count;
        public Group(int prefab, int count) { this.prefab = prefab; this.count = count; }

        // Grupos pequenos são "apresentações" e chefes: a graça deles é a qualidade, não a quantidade.
        // Só os grupos grandes acompanham o crescimento da onda.
        //
        // A CONTAGEM SOZINHA NÃO BASTA para reconhecer um chefe. `Boss 6` na rodada final passava
        // por "grupo grande" e escalava junto com a onda: medido, fator 10,2 na rodada 40, ou
        // seja, os 6 chefes escritos aqui viravam 61. Isso fez a rodada 40 pagar $88 por inimigo
        // (contra ~$8 das rodadas comuns) e sozinha entregar +$28.173 ao jogador. Uma composição
        // autoral de chefes é qualidade; multiplicá-la é outra coisa.
        public bool Escalavel => count > 3 && !Marcante(prefab);
    }

    // Rodada → composição exata. As demais continuam no sorteio por peso.
    private static readonly Dictionary<int, Group[]> scripted = new Dictionary<int, Group[]>
    {
        // Abertura: só o básico, para o jogador entender o loop.
        { 1,  new[] { new Group(Enemy, 8) } },
        { 2,  new[] { new Group(Enemy, 12) } },
        { 3,  new[] { new Group(Enemy, 10), new Group(Fast, 4) } },

        // Apresentações: cada ameaça nova estreia isolada e em pouca quantidade, para o jogador
        // entender o que ela faz antes de encontrá-la no meio de uma onda cheia.
        { 5,  new[] { new Group(Enemy, 8), new Group(Camo, 3) } },            // camuflado
        { 7,  new[] { new Group(Fast, 10), new Group(Armored, 3) } },          // blindado
        { 9,  new[] { new Group(Tank, 6), new Group(Regen, 3) } },             // regenerador

        // Rodada 10: primeiro chefe. Marco de dificuldade.
        { 10, new[] { new Group(Enemy, 10), new Group(Tank, 4), new Group(Boss, 1) } },

        { 11, new[] { new Group(Fast, 8), new Group(Lead, 4) } },              // chumbo: imune a cortante
        { 12, new[] { new Group(Enemy, 10), new Group(Saboteur, 2) } },        // sabotador
        { 13, new[] { new Group(Fast, 20) } },                                 // enxame veloz
        { 14, new[] { new Group(Tank, 6), new Group(Thief, 3) } },             // ladrão
        { 15, new[] { new Group(TwoTank, 6), new Group(Armored, 6) } },        // parede blindada
        { 16, new[] { new Group(Fast, 12), new Group(Shielder, 2) } },         // escudeiro
        { 17, new[] { new Group(Ceramic, 6), new Group(Lead, 6) } },           // cerâmica

        // Rodada 20: onda camuflada em massa — pune quem nunca comprou detecção.
        { 20, new[] { new Group(Camo, 18), new Group(Boss, 1) } },

        { 22, new[] { new Group(Saboteur, 4), new Group(Thief, 4), new Group(Shielder, 3) } }, // onda anti-torre
        { 23, new[] { new Group(Regen, 10), new Group(Trojan, 3) } },
        { 25, new[] { new Group(Boss, 2), new Group(Armored, 10) } },

        // Rodada 27: o MOAB. A ameaça que exige uma resposta dedicada.
        { 27, new[] { new Group(Ceramic, 8), new Group(Moab, 1) } },

        { 30, new[] { new Group(Trojan, 6), new Group(Boss, 3), new Group(Moab, 1) } },
        { 33, new[] { new Group(Lead, 12), new Group(Ceramic, 10), new Group(Shielder, 4) } },
        { 35, new[] { new Group(Camo, 15), new Group(Armored, 15), new Group(Boss, 2) } },
        { 38, new[] { new Group(Moab, 2), new Group(Saboteur, 6), new Group(Ceramic, 12) } },

        // Rodada 40: final.
        { 40, new[] { new Group(Boss, 6), new Group(Moab, 3), new Group(Ceramic, 15), new Group(TwoTank, 10) } },
    };

    public static bool IsScripted(int wave) => scripted.ContainsKey(wave);

    // Nome curto para a HUD anunciar o que vem por aí.
    public static string Headline(int wave)
    {
        switch (wave)
        {
            case 5:  return "Camuflados!";
            case 7:  return "Blindados!";
            case 9:  return "Regeneradores!";
            case 10: return "CHEFE";
            case 11: return "Chumbo! (dardos não furam)";
            case 12: return "Sabotadores! (desligam torres)";
            case 13: return "Enxame";
            case 14: return "Ladrões! (roubam dinheiro)";
            case 15: return "Parede blindada";
            case 16: return "Escudeiros! (protegem o grupo)";
            case 17: return "Cerâmicas! (imunes a explosão)";
            case 20: return "ONDA CAMUFLADA";
            case 22: return "INVESTIDA ANTI-TORRE";
            case 25: return "Chefes duplos";
            case 27: return "M.O.A.B.!";
            case 30: return "Investida";
            case 33: return "Muralha";
            case 35: return "Cerco";
            case 38: return "Ofensiva final";
            case 40: return "ONDA FINAL";
            default: return null;
        }
    }

    // Fila de spawn da rodada. `targetCount` é o tamanho que a curva de dificuldade pediria para
    // esta rodada: os grupos grandes são escalados até chegar lá, para que uma rodada roteirizada
    // nunca seja MAIS FRACA que uma rodada sorteada do mesmo número.
    // Retorna null se a rodada não é roteirizada (aí vale o sorteio por peso).
    public static List<int> BuildQueue(int wave, int prefabCount, int targetCount = 0)
    {
        if (!scripted.TryGetValue(wave, out Group[] groups)) return null;

        // Quanto os grupos escaláveis precisam crescer para a onda ter o tamanho esperado.
        float fator = 1f;
        if (targetCount > 0)
        {
            int escalavel = 0, fixo = 0;
            foreach (Group g in groups)
            {
                if (g.prefab >= prefabCount) continue;
                if (g.Escalavel) escalavel += g.count; else fixo += g.count;
            }
            if (escalavel > 0)
                fator = Mathf.Clamp((targetCount - fixo) / (float)escalavel, 1f, TetoDeEscalonamento);
        }

        var queue = new List<int>();
        foreach (Group g in groups)
        {
            if (g.prefab >= prefabCount) continue; // fase/projeto com menos prefabs: ignora o grupo
            int quantos = g.Escalavel ? Mathf.RoundToInt(g.count * fator) : g.count;
            for (int i = 0; i < quantos; i++) queue.Add(g.prefab);
        }
        if (queue.Count == 0) return null;

        // Embaralha, mas mantém os mais fortes no fim: a onda deve escalar, não abrir com o chefe.
        for (int i = 0; i < queue.Count - 1; i++)
        {
            int j = Random.Range(i, queue.Count);
            int tmp = queue[i]; queue[i] = queue[j]; queue[j] = tmp;
        }
        queue.Sort((a, b) => a.CompareTo(b));

        return queue;
    }
}
