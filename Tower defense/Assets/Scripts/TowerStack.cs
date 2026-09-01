using System.Collections.Generic;
using UnityEngine;

// Marca um SpriteRenderer como parte da pilha de blocos. TowerBase.ApplyTint usa isto pra NÃO
// lavar a cor dos blocos com o tint de tier — ver o comentário lá.
public class TowerStackBlock : MonoBehaviour { }

// Evolução visual por EMPILHAMENTO: cada tier comprado numa trilha ACRESCENTA um bloco isométrico
// do pacote, e o poder da torre vira ALTURA — legível do outro lado da tela, em vez de só
// transform.localScale + tint (ver TowerBase.Recalculate).
//
// Fica no mesmo objeto que a subclasse de TowerBase ("Base" nos prefabs existentes), lê
// PathLevel(0)/PathLevel(1) e remonta a pilha sempre que algum dos dois muda. Os blocos entram
// como filhos deste objeto, empilhados a partir de Y local 0 (onde a torre já senta no
// CellToWorld do plot — ver IsoGrid); o RotatePoint (a arma que mira) é reancorado no topo da
// pilha, então o canhão sobe fisicamente junto com a torre.
//
// Não precisa de nenhum sprite arrastado no Inspector: os blocos vêm de TowerBlockPalette
// (Assets/Resources/TowerBlockPalette.asset), carregado em runtime via Resources.Load. Uma torre
// nova só precisa deste componente + 3 enums (cor, tema da trilha A, tema da trilha B).
public class TowerStack : MonoBehaviour
{
    public enum BlockColor { Green, Purple, Red, Yellow }
    public enum BlockTheme { Heavy, Spire, Frame, Lean }

    // Número dos arquivos block(N).png de cada tema, na ordem em que aparecem (tier 1 -> tier 3
    // da trilha). Escolhidos medindo as silhuetas do pacote (ver relatório):
    //   Heavy = reforço -> muralha com torreões -> coroa ornamentada: fortaleza larga e maciça.
    //   Spire = espinhos de canto -> torreão com pináculo -> pináculo simples: torre alta e fina.
    //   Frame = entalhe em cruz -> moldura vazada -> muralha com torreões: leve, aberto, técnico.
    //   Lean  = apoio liso -> entalhe em cruz -> reforço: crescimento discreto, quase utilitário.
    // Nenhum tema usa block(11): é o único ausente no set Yellow (10 blocos), e ficar fora
    // evita ter que tratar a cor Yellow como caso especial em todo o resto do código.
    private static readonly int[] Foundation = { 3, 1 }; // pedestal alargado + apoio — sempre presente, mesmo em 0-0
    private static readonly int[] Heavy = { 4, 6, 8 };
    private static readonly int[] Spire = { 5, 9, 10 };
    private static readonly int[] Frame = { 2, 7, 6 };
    private static readonly int[] Lean = { 1, 2, 4 };

    [Header("Identidade visual (ver TowerStack para o mapeamento torre -> cor -> tema)")]
    [SerializeField] private BlockColor color = BlockColor.Green;
    [SerializeField] private BlockTheme pathATheme = BlockTheme.Heavy;
    [SerializeField] private BlockTheme pathBTheme = BlockTheme.Spire;

    private static TowerBlockPalette paletteCache;
    private static bool paletteLoadAttempted;

    private TowerBase tower;
    private Transform rotatePoint;
    private Vector3 rotatePointRestPos;
    private Material blockMaterial;
    private int blockSortingLayer;
    private SpriteRenderer rootPlaceholder; // sprite antigo (pré-isométrico) na raiz do prefab
    private Sprite topSprite; // bloco mais alto da pilha atual — usado como ícone (ver TopBlockSprite)

    private readonly List<GameObject> spawned = new List<GameObject>();
    private int lastLevelA = -1, lastLevelB = -1;

    // Sprite do bloco mais alto da pilha (o mais "representativo"). TowerBase.GetIcon() prefere
    // isto ao placeholder antigo (Triangle/Square/Capsule sobrando de antes da arte isométrica).
    public Sprite TopBlockSprite() => topSprite;

    private static TowerBlockPalette Palette
    {
        get
        {
            if (paletteCache == null && !paletteLoadAttempted)
            {
                paletteLoadAttempted = true;
                paletteCache = Resources.Load<TowerBlockPalette>("TowerBlockPalette");
                if (paletteCache == null)
                    Debug.LogWarning("TowerStack: Assets/Resources/TowerBlockPalette.asset não encontrado — torres ficam sem a pilha de blocos.");
            }
            return paletteCache;
        }
    }

    private void Awake()
    {
        tower = GetComponent<TowerBase>();

        // Duas topologias convivem nos prefabs existentes: torres que miram (Turret, Farm,
        // Detector, Sniper, MachineGun, Ice, Tesla apesar de não mirar) têm Base/RotatePoint/Gun;
        // Tachinha e Bomb têm Base/RotatePoint mas sem Gun (não giram); Ice e Tesla nem têm Base
        // (script direto na raiz, sem filho nenhum). Tenta os dois caminhos possíveis.
        rotatePoint = transform.Find("RotatePoint");
        if (rotatePoint == null) rotatePoint = transform.Find("Base/RotatePoint");
        if (rotatePoint != null) rotatePointRestPos = rotatePoint.localPosition;

        // Copia material/camada de um SpriteRenderer já existente pros blocos novos, pra não
        // caírem no material padrão do Unity e destoarem da iluminação URP do resto do tabuleiro
        // (ver armadilha "URP ignora transparencySortMode" — aqui o risco irmão é material
        // errado, não sorting). Prefere o da arma (Gun); na falta dela, qualquer SpriteRenderer
        // da torre serve de referência (o placeholder da raiz, ver rootPlaceholder abaixo).
        SpriteRenderer reference = rotatePoint != null ? rotatePoint.GetComponentInChildren<SpriteRenderer>() : null;
        if (reference == null) reference = GetComponentInChildren<SpriteRenderer>();
        if (reference != null)
        {
            blockMaterial = reference.sharedMaterial;
            blockSortingLayer = reference.sortingLayerID;
        }

        // O sprite da raiz, em TODOS os 9 prefabs, é sobra de antes da arte isométrica entrar
        // (nomes tipo "Triangle"/"Square"/"Capsule"/"Turret_0" — primitivas de debug do Unity, não
        // arte do pacote). Uma vez que a pilha nasce (ver Rebuild), ele só ficaria atravessado
        // embaixo/atrás do bloco de fundação — escondido, não destruído, pra continuar existindo
        // como fallback caso a paleta não carregue (ver Rebuild) e pra GetComponentInChildren
        // continuar achando alguma coisa em GetIcon() se TopBlockSprite() vier vazio.
        rootPlaceholder = transform.root.GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (tower == null) return;
        int a = tower.PathLevel(0);
        int b = tower.PathLevel(1);
        if (a == lastLevelA && b == lastLevelB) return;
        lastLevelA = a; lastLevelB = b;
        Rebuild(a, b);
    }

    private static int[] BlocksFor(BlockTheme theme)
    {
        switch (theme)
        {
            case BlockTheme.Heavy: return Heavy;
            case BlockTheme.Spire: return Spire;
            case BlockTheme.Frame: return Frame;
            default: return Lean;
        }
    }

    private void Rebuild(int levelA, int levelB)
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i]);
        spawned.Clear();

        TowerBlockPalette palette = Palette;
        if (palette == null) return; // sem paleta: a torre fica só com a arma antiga, não quebra nada

        List<int> numbers = new List<int>(Foundation);
        int[] aBlocks = BlocksFor(pathATheme);
        for (int i = 0; i < levelA && i < aBlocks.Length; i++) numbers.Add(aBlocks[i]);
        int[] bBlocks = BlocksFor(pathBTheme);
        for (int i = 0; i < levelB && i < bBlocks.Length; i++) numbers.Add(bBlocks[i]);

        float y = 0f, topY = 0f;
        Sprite prev = null;

        for (int i = 0; i < numbers.Count; i++)
        {
            Sprite s = palette.Block(color, numbers[i]);
            if (s == null) continue; // ex.: tema pediu um número que essa cor não tem

            float h = s.rect.height, w = s.rect.width, ppu = s.pixelsPerUnit;
            // O topo de cada bloco é um losango 2:1, igual ao chão (ver IsoGrid) — é a partir
            // dessa proporção que dá pra separar, no PNG, "topo" (losango) de "corpo" (parede),
            // sem precisar de nenhum número mágico por bloco.
            float diamondH = w * (IsoGrid.TileHeight / IsoGrid.TileWidth);

            if (prev == null)
            {
                // Primeiro bloco: a base dele encosta no chão, ou seja, no Y local 0 — o mesmo
                // ponto onde CellToWorld encaixa o tile e a torre "senta".
                y = h / (2f * ppu);
            }
            else
            {
                float hp = prev.rect.height, wp = prev.rect.width, ppup = prev.pixelsPerUnit;
                float dp = wp * (IsoGrid.TileHeight / IsoGrid.TileWidth);
                // Sobe o suficiente pra base do bloco novo encostar exatamente onde o corpo do
                // bloco de baixo termina (a parede dele, não o losango do topo).
                y += (hp - 2f * dp + h) / (2f * ppu);
            }
            topY = y + (h / 2f - diamondH) / ppu; // onde o PRÓXIMO bloco (ou a arma) assenta

            GameObject go = new GameObject("StackBlock_" + numbers[i]);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            if (blockMaterial != null) sr.sharedMaterial = blockMaterial;
            sr.sortingLayerID = blockSortingLayer;
            go.AddComponent<TowerStackBlock>();

            spawned.Add(go);
            prev = s;
        }

        topSprite = prev;

        if (rotatePoint != null)
        {
            rotatePoint.localPosition = prev == null
                ? rotatePointRestPos
                : new Vector3(rotatePointRestPos.x, topY, rotatePointRestPos.z);
        }

        // A pilha nasceu: esconde o placeholder antigo da raiz (ver Awake). Só chega aqui com
        // prev != null (pelo menos um bloco real foi montado), então nunca some o placeholder à
        // toa se por algum motivo a paleta não render nenhum sprite.
        if (rootPlaceholder != null && prev != null) rootPlaceholder.enabled = false;

        // IsoSorter captura os SpriteRenderers no Init() dele, que já rodou quando o Plot
        // construiu a torre — ANTES desses blocos existirem. Sem reanexar aqui, os blocos nunca
        // entram na ordenação de profundidade e desenham na camada errada (ver IsoSorter).
        IsoSorter sorter = GetComponentInParent<IsoSorter>();
        if (sorter != null) IsoSorter.Attach(sorter.gameObject, false); // moves:false — torre não se move
    }
}
