using System.Collections.Generic;
using UnityEngine;

// Deixa semitransparente a torre que está tapando outra torre.
//
// POR QUE ISTO EXISTE. Plots vizinhos em profundidade ficam a 1,0 unidade de distância e uma
// torre de tier alto tem ~2,9 de altura: três vezes o espaçamento. Quando duas caem alinhadas,
// a da frente engole dois terços da de trás e as duas viram uma coluna só com duas armas no
// meio — medido em 2026-09-04: 14% de silhueta tapada em média, e 66% no pior caso.
//
// A ordenação NÃO está errada nisso: a da frente tem ordem maior e desenha por cima, como deve.
// O problema é geométrico. E reduzir altura não resolve — encolher a torre em 16% tirou só 8
// pontos do pior caso (66% -> 58%); para zerar, a torre teria que caber em 1,0 unidade, o que
// apaga a leitura de poder por altura. Por isso a saída é de apresentação, não de geometria.
//
// É o padrão de jogo isométrico (Sims, Diablo, Age of Empires): o que está na frente cede
// passagem para o olho em vez de o mundo ser remodelado para nunca se sobrepor.
[DefaultExecutionOrder(200)] // depois do LateUpdate do TowerBase, que repinta a torre inteira
public class TowerOverlapFade : MonoBehaviour
{
    [SerializeField, Range(0.15f, 1f)] private float alphaQuandoCobre = 0.72f;

    // Só faz fade se a torre da frente cobrir pelo menos esta fração da de trás. Sem um piso,
    // qualquer encosto de quina deixaria meia defesa translúcida sem motivo.
    [SerializeField, Range(0.02f, 0.6f)] private float coberturaMinima = 0.25f;

    // E só se elas estiverem mesmo ALINHADAS: esta é a fração da largura da torre de trás que a
    // da frente precisa cobrir.
    //
    // Sem este segundo teste, a primeira versão deixou 5 de 10 torres translúcidas. A causa é que
    // a torre é alta e fina: duas torres lado a lado, sem nenhuma tapar a outra de verdade, ainda
    // têm caixas que se cruzam o bastante em ÁREA para passar do limiar. Área sozinha não
    // distingue "está na minha frente" de "está do meu lado".
    [SerializeField, Range(0.2f, 1f)] private float alinhamentoMinimo = 0.55f;

    // Torres não se movem: só é preciso recalcular quando a defesa muda (construção, venda,
    // queda, upgrade). Varrer a cada quarto de segundo é mais simples e mais robusto que
    // pendurar em cinco eventos diferentes, e o custo é irrisório com poucas dezenas de torres.
    [SerializeField] private float intervaloDeChecagem = 0.25f;

    private float proximaChecagem;
    private readonly List<Transform> raizes = new List<Transform>();
    private readonly HashSet<Transform> comFade = new HashSet<Transform>();
    private readonly Dictionary<Transform, SpriteRenderer[]> spritesPorRaiz =
        new Dictionary<Transform, SpriteRenderer[]>();

    private void LateUpdate()
    {
        if (Time.time >= proximaChecagem)
        {
            proximaChecagem = Time.time + Mathf.Max(0.05f, intervaloDeChecagem);
            Recalcular();
        }

        // O alpha é reaplicado TODO frame, e isso é de propósito: TowerBase repinta a torre
        // inteira quando ela é sabotada ou perde integridade (ver SetIntegrityTint), e essas
        // repinturas escrevem a cor completa. Sem reaplicar, o fade sumiria justamente na torre
        // que está sofrendo — que é quando o jogador mais precisa enxergá-la.
        for (int i = 0; i < raizes.Count; i++)
        {
            Transform r = raizes[i];
            if (r == null) continue;
            AplicarAlpha(r, comFade.Contains(r) ? alphaQuandoCobre : 1f);
        }
    }

    private void Recalcular()
    {
        raizes.Clear();
        spritesPorRaiz.Clear();
        comFade.Clear();

        foreach (TowerBase t in TowerBase.Todas)
        {
            Transform r = t.transform.root;
            if (raizes.Contains(r)) continue;
            raizes.Add(r);
            spritesPorRaiz[r] = SpritesDe(r);
        }

        Vector3 origem = IsoBoard.main != null ? IsoBoard.main.Origin : Vector3.zero;

        for (int i = 0; i < raizes.Count; i++)
        {
            Transform atras = raizes[i];
            Bounds bAtras;
            if (!Silhueta(atras, out bAtras)) continue;
            int ordemAtras = IsoGrid.SortingOrderAt(atras.position, origem);

            for (int j = 0; j < raizes.Count; j++)
            {
                if (i == j) continue;
                Transform frente = raizes[j];
                if (IsoGrid.SortingOrderAt(frente.position, origem) <= ordemAtras) continue;

                Bounds bFrente;
                if (!Silhueta(frente, out bFrente)) continue;
                if (Tapa(bAtras, bFrente)) comFade.Add(frente);
            }
        }
    }

    // A da frente tapa a de trás a ponto de atrapalhar?
    //
    // Dois testes, e são necessários os dois. ÁREA responde "cobre bastante"; ALINHAMENTO
    // responde "está na frente, não do lado" — que a área sozinha confunde, porque a torre é alta
    // e fina e duas vizinhas cruzam muita área só por estarem próximas.
    //
    // A interseção usa caixas, não o alpha dos sprites: é aproximação deliberada, já que a
    // decisão é binária e amostrar pixel para isso custaria caro sem mudar a resposta.
    private bool Tapa(Bounds atras, Bounds frente)
    {
        float larg = Mathf.Min(atras.max.x, frente.max.x) - Mathf.Max(atras.min.x, frente.min.x);
        float alt = Mathf.Min(atras.max.y, frente.max.y) - Mathf.Max(atras.min.y, frente.min.y);
        if (larg <= 0f || alt <= 0f) return false;

        float areaAtras = atras.size.x * atras.size.y;
        if (areaAtras <= 0f || atras.size.x <= 0f) return false;

        float area = (larg * alt) / areaAtras;
        float alinhamento = larg / atras.size.x;
        return area >= coberturaMinima && alinhamento >= alinhamentoMinimo;
    }

    private bool Silhueta(Transform raiz, out Bounds b)
    {
        b = new Bounds();
        SpriteRenderer[] sprites;
        if (!spritesPorRaiz.TryGetValue(raiz, out sprites)) return false;

        bool primeiro = true;
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sr = sprites[i];
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            if (primeiro) { b = sr.bounds; primeiro = false; } else b.Encapsulate(sr.bounds);
        }
        return !primeiro;
    }

    private void AplicarAlpha(Transform raiz, float alpha)
    {
        SpriteRenderer[] sprites;
        if (!spritesPorRaiz.TryGetValue(raiz, out sprites)) return;

        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sr = sprites[i];
            if (sr == null) continue;
            Color c = sr.color;
            if (Mathf.Approximately(c.a, alpha)) continue; // não escreve à toa
            c.a = alpha;
            sr.color = c;
        }
    }

    // O anel de alcance fica de fora: ele já tem alpha próprio e é informação de UI, não parte
    // da silhueta da torre.
    private static SpriteRenderer[] SpritesDe(Transform raiz)
    {
        var lista = new List<SpriteRenderer>();
        foreach (SpriteRenderer sr in raiz.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.GetComponent<RangeIndicator>() != null) continue;
            lista.Add(sr);
        }
        return lista.ToArray();
    }
}
