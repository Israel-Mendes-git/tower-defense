using System.Collections.Generic;
using UnityEngine;

// Monta o tabuleiro isométrico da fase escolhida.
//
// A ideia central: as células LIVRES são os próprios Plot que já existem na cena, só que
// reposicionados na grade isométrica e usando o sprite de chão do pacote. Assim hover, clique,
// collider e construção continuam sendo o código que já funciona — o que muda é onde a célula
// fica e que forma ela tem. Só as células de CAMINHO viram objetos novos (os tiles de estrada),
// porque ali não se constrói.
//
// Roda antes do StageLoader, que lê PathPoints daqui em vez de espalhar o traçado sobre o
// retângulo dos plots.
[DefaultExecutionOrder(-150)]
public class IsoBoard : MonoBehaviour
{
    public static IsoBoard main;

    [Header("Grade")]
    [SerializeField] private int gridSize = 11;
    [SerializeField] private bool frameCamera = true;
    [SerializeField] private float cameraPadding = 1.12f;

    // A UI lateral cobre a esquerda da tela. Enquadrar pelo centro da tela joga metade do
    // tabuleiro atrás dos painéis; o enquadramento tem que ser feito na área que sobra.
    [Header("Enquadramento")]
    [SerializeField] private float uiMarginLeftPx = 600f;   // painel da loja + painel de upgrade
    [SerializeField] private float uiReferenceWidth = 1920f;
    [SerializeField] private Color backgroundColor = new Color(0.055f, 0.078f, 0.09f);

    [Header("Decoração")]
    [SerializeField, Range(0f, 1f)] private float decorChance = 0.5f;
    private Sprite[] decorSprites; // vem do conjunto do bioma, escolhido no Build

    // BIOMA. O "Isometric Nature Pack 2.0" traz estrada, chão e ambiente completos em três climas,
    // com a mesma geometria de tile do pacote original — então dar identidade visual a cada fase
    // custa trocar de pasta, não redesenhar nada. O pacote original entra como Original para as
    // fases que já estão afinadas continuarem exatamente como estão.
    //
    // O bioma NÃO é lido em runtime: ele escolhe de onde 'Carregar sprites do pacote' puxa os
    // arquivos, e o que vai para o build são as referências já serializadas nos campos abaixo.
    public enum Bioma { Original, Campo, Deserto, Inverno }

    // Um conjunto de arte fechado. Existe um por bioma, TODOS serializados na cena, porque quem
    // escolhe é a fase — e a fase só se conhece em runtime. Carregar por AssetDatabase na hora não
    // serviria: ela não existe em build.
    [System.Serializable]
    public class ConjuntoDoBioma
    {
        public Bioma bioma;
        public Sprite chao;
        public Sprite[] estradas = new Sprite[16]; // indexado pela máscara 1..15
        public Sprite[] decor;
    }

    [Header("Bioma")]
    // Usado só quando a fase não manda (cena aberta direto no editor, fora do fluxo de seleção).
    [SerializeField] private Bioma biomaPadrao = Bioma.Campo;

    [Header("Conjuntos por bioma (preencha com 'Carregar sprites do pacote')")]
    [SerializeField] private ConjuntoDoBioma[] conjuntos = new ConjuntoDoBioma[0];

    // O conjunto EM USO nesta partida. Não é serializado como fonte: é preenchido no Build a
    // partir do conjunto do bioma da fase. Os campos seguem existindo porque todo o resto do
    // arquivo lê deles.
    private Sprite groundSprite;
    private Sprite[] roadSprites = new Sprite[16];

    public Bioma BiomaAtual { get; private set; }

    // Centros das células do caminho, na ordem — é o que o StageLoader usa como waypoints.
    public Vector3[] PathPoints { get; private set; }
    public int GridSize => gridSize;
    public Vector3 Origin => transform.position;

    private readonly List<GameObject> generated = new List<GameObject>();

    private void Awake()
    {
        main = this;
        Build(StageCatalog.Selected);
    }

    public void Build(StageDefinition stage)
    {
        if (stage == null || stage.pathNormalized == null || stage.pathNormalized.Length < 2) return;

        // A ARTE SAI DA FASE. Tem de vir antes de qualquer coisa que desenhe: LayOutCells e
        // ScatterDecor leem groundSprite/roadSprites/decorSprites, e sem isto montariam o
        // tabuleiro com o conjunto da fase anterior.
        AplicarBioma(stage.bioma);

        ClearGenerated();

        List<Vector2Int> path = PathCells(stage);
        var pathSet = new HashSet<Vector2Int>(path);

        // O caminho vira waypoints no centro de cada célula.
        PathPoints = new Vector3[path.Count];
        for (int i = 0; i < path.Count; i++)
            PathPoints[i] = IsoGrid.CellToWorld(path[i].x, path[i].y, Origin);

        LayOutCells(pathSet);
        ScatterDecor(pathSet);
        if (frameCamera) FrameCamera();
    }

    // ───────── bioma -> conjunto de arte ─────────

    // Escolhe o conjunto do bioma pedido e o coloca em uso. Se ele não estiver preenchido, cai
    // para o primeiro que tenha chão — um tabuleiro com a arte "errada" ainda é jogável, um
    // tabuleiro sem sprite nenhum é uma tela vazia, e tela vazia sem erro é o pior dos dois.
    private void AplicarBioma(Bioma alvo)
    {
        ConjuntoDoBioma c = Conjunto(alvo);

        if (c == null)
        {
            Debug.LogWarning("IsoBoard: bioma " + alvo + " não tem conjunto preenchido. "
                + "Rode 'Carregar sprites do pacote' no IsoBoard.", this);

            // Comparação explícita, e não `??`: ConjuntoDoBioma é classe C# comum e o operador
            // seria seguro aqui, mas neste projeto `??` em referência é sinal de bug (ele não
            // respeita o "fake null" dos tipos do Unity), e vale não escrever o padrão perigoso.
            c = Conjunto(biomaPadrao);
            if (c == null) c = PrimeiroValido();
            if (c == null) return;
        }

        BiomaAtual = c.bioma;
        groundSprite = c.chao;
        roadSprites = c.estradas != null && c.estradas.Length >= 16 ? c.estradas : new Sprite[16];
        decorSprites = c.decor;
    }

    private ConjuntoDoBioma Conjunto(Bioma alvo)
    {
        if (conjuntos == null) return null;
        foreach (ConjuntoDoBioma c in conjuntos)
            if (c != null && c.bioma == alvo && c.chao != null) return c;
        return null;
    }

    private ConjuntoDoBioma PrimeiroValido()
    {
        if (conjuntos == null) return null;
        foreach (ConjuntoDoBioma c in conjuntos)
            if (c != null && c.chao != null) return c;
        return null;
    }

    // ───────── traçado -> células ─────────

    // Os traçados do StageCatalog são ortogonais (só mudam em X ou em Y, com curvas de 90°),
    // que é exatamente o que o tileset de estrada cobre. Cada trecho vira uma sequência de
    // células adjacentes, andando primeiro na coluna e depois na linha.
    private List<Vector2Int> PathCells(StageDefinition stage)
    {
        int n = gridSize - 1;
        var cells = new List<Vector2Int>();
        var seen = new HashSet<Vector2Int>();

        Vector2Int cur = ToCell(stage.pathNormalized[0], n);
        cells.Add(cur); seen.Add(cur);

        for (int i = 1; i < stage.pathNormalized.Length; i++)
        {
            Vector2Int dest = ToCell(stage.pathNormalized[i], n);

            while (cur.x != dest.x)
            {
                cur.x += cur.x < dest.x ? 1 : -1;
                if (seen.Add(cur)) cells.Add(cur);
            }
            while (cur.y != dest.y)
            {
                cur.y += cur.y < dest.y ? 1 : -1;
                if (seen.Add(cur)) cells.Add(cur);
            }
        }
        return cells;
    }

    // O traçado vem em 0..1 com Y para cima (convenção do StageCatalog); a linha do grid cresce
    // para baixo na tela, daí o (1 - y).
    private static Vector2Int ToCell(Vector2 normalized, int n)
    {
        return new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(normalized.x * n), 0, n),
            Mathf.Clamp(Mathf.RoundToInt((1f - normalized.y) * n), 0, n));
    }

    // ───────── montagem ─────────

    private void LayOutCells(HashSet<Vector2Int> pathSet)
    {
        var plots = new List<Plot>(FindObjectsOfType<Plot>(true));
        int usados = 0;

        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                var cell = new Vector2Int(col, row);
                Vector3 pos = IsoGrid.CellToWorld(col, row, Origin);
                int order = IsoGrid.SortingOrder(col, row);

                if (pathSet.Contains(cell))
                {
                    SpawnRoad(col, row, pos, order, MaskAt(pathSet, col, row));
                }
                else if (usados < plots.Count)
                {
                    PlaceFreeCell(plots[usados], pos, order);
                    usados++;
                }
            }
        }

        // Plots que sobraram (tabuleiro menor que a cena) saem de cena.
        for (int i = usados; i < plots.Count; i++)
            plots[i].gameObject.SetActive(false);
    }

    private int MaskAt(HashSet<Vector2Int> set, int col, int row)
    {
        int m = 0;
        if (set.Contains(new Vector2Int(col, row - 1))) m |= IsoGrid.NE;
        if (set.Contains(new Vector2Int(col + 1, row))) m |= IsoGrid.SE;
        if (set.Contains(new Vector2Int(col, row + 1))) m |= IsoGrid.SW;
        if (set.Contains(new Vector2Int(col - 1, row))) m |= IsoGrid.NW;
        return m;
    }

    private void SpawnRoad(int col, int row, Vector3 pos, int order, int mask)
    {
        Sprite s = null;
        if (mask > 0 && mask < roadSprites.Length) s = roadSprites[mask];
        if (s == null) s = roadSprites.Length > 15 ? roadSprites[15] : null; // fallback: cruzamento
        if (s == null) s = groundSprite;

        var go = new GameObject("Road " + col + "," + row);
        go.transform.SetParent(transform, false);
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;

        generated.Add(go);
    }

    // Uma célula livre é um Plot que já existia: só muda de lugar, de forma e de sprite.
    private void PlaceFreeCell(Plot plot, Vector3 pos, int order)
    {
        plot.gameObject.SetActive(true);
        plot.transform.position = pos;
        plot.transform.localScale = Vector3.one; // o pivô do tile já resolve o encaixe

        var sr = plot.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = groundSprite;
            sr.color = Color.white; // o Plot guarda esta cor como "normal" no Start dele
            sr.sortingOrder = order;
        }

        // O clique tem que respeitar a forma da célula: um box quadrado erra as quinas e
        // rouba o clique da célula vizinha.
        var box = plot.GetComponent<BoxCollider2D>();
        if (box != null) Destroy(box);

        var poly = plot.GetComponent<PolygonCollider2D>();
        if (poly == null) poly = plot.gameObject.AddComponent<PolygonCollider2D>();
        poly.pathCount = 1;
        poly.SetPath(0, IsoGrid.DiamondShape());
    }

    private void ClearGenerated()
    {
        foreach (GameObject go in generated)
            if (go != null) Destroy(go);
        generated.Clear();
    }

    // ───────── câmera ─────────

    // Enquadra o losango inteiro do tabuleiro NA ÁREA QUE A UI DEIXA LIVRE. Sem isto, trocar o
    // tamanho da grade deixa metade do mapa fora da tela — e enquadrar pelo centro da tela
    // esconde o lado esquerdo do tabuleiro atrás dos painéis.
    private void FrameCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        cam.backgroundColor = backgroundColor;

        int n = gridSize - 1;
        float largura = 2f * n * IsoGrid.StepX + IsoGrid.TileWidth;
        float altura = 2f * n * IsoGrid.StepY + IsoGrid.TileHeight;

        // Fração da largura da tela comida pela UI.
        float fracUI = Mathf.Clamp01(uiMarginLeftPx / Mathf.Max(1f, uiReferenceWidth));
        float fracUtil = Mathf.Max(0.2f, 1f - fracUI);

        // O tabuleiro precisa caber na área útil, que é mais estreita: o zoom compensa.
        float porAltura = altura * 0.5f;
        float porLargura = (largura * 0.5f) / Mathf.Max(0.0001f, cam.aspect * fracUtil);
        cam.orthographicSize = Mathf.Max(porAltura, porLargura) * cameraPadding;

        // E o centro do tabuleiro vai para o centro da área útil, não da tela.
        float larguraVisivel = 2f * cam.orthographicSize * cam.aspect;
        float deslocX = larguraVisivel * fracUI * 0.5f;

        float centroY = Origin.y - n * IsoGrid.StepY;
        cam.transform.position = new Vector3(Origin.x - deslocX, centroY, cam.transform.position.z);
    }

    // Anel de terreno decorativo em volta do tabuleiro.
    //
    // Não dá para decorar por dentro: toda célula livre vira um plot construível, e uma árvore
    // ali só confundiria a leitura de onde se pode construir. O anel resolve os dois problemas de
    // uma vez — dá silhueta ao mapa e evita que o tabuleiro pareça uma placa recortada no vazio.
    private void ScatterDecor(HashSet<Vector2Int> pathSet)
    {
        if (groundSprite == null) return;

        // Semente fixa pela fase: a moldura é sempre a mesma, então o mapa fica reconhecível.
        StageDefinition stage = StageCatalog.Selected;
        Random.State estado = Random.state;
        Random.InitState(stage != null && stage.id != null ? stage.id.GetHashCode() : 12345);

        int n = gridSize - 1;
        for (int row = -1; row <= n + 1; row++)
        {
            for (int col = -1; col <= n + 1; col++)
            {
                bool dentro = col >= 0 && col <= n && row >= 0 && row <= n;
                if (dentro) continue; // só o anel externo

                Vector3 pos = IsoGrid.CellToWorld(col, row, Origin);
                int order = IsoGrid.SortingOrder(col, row);

                var chao = new GameObject("Borda " + col + "," + row);
                chao.transform.SetParent(transform, false);
                chao.transform.position = pos;
                var csr = chao.AddComponent<SpriteRenderer>();
                csr.sprite = groundSprite;
                csr.color = new Color(0.72f, 0.78f, 0.72f); // levemente dessaturado: é cenário, não tabuleiro
                csr.sortingOrder = order;
                generated.Add(chao);

                if (decorSprites == null || decorSprites.Length == 0) continue;
                if (Random.value > decorChance) continue;

                Sprite s = decorSprites[Random.Range(0, decorSprites.Length)];
                if (s == null) continue;

                var go = new GameObject("Decor " + col + "," + row);
                go.transform.SetParent(transform, false);
                go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = s;
                sr.sortingOrder = order + IsoGrid.ObjectOffset;
                generated.Add(go);
            }
        }
        Random.state = estado;
    }

#if UNITY_EDITOR
    // Preenche as referências a partir do pacote. Fica no editor de propósito: em runtime os
    // sprites vêm serializados, sem depender de AssetDatabase nem de pasta Resources.
    [ContextMenu("Carregar sprites do pacote")]
    public void LoadSpritesFromPackage()
    {
        // Preenche TODOS os biomas de uma vez. A fase escolhe o conjunto em runtime, então deixar
        // só o bioma "atual" carregado faria as outras fases nascerem sem arte — e o carregamento
        // depende de AssetDatabase, que não existe em build: é agora ou nunca.
        var lista = new List<ConjuntoDoBioma>();
        foreach (Bioma b in System.Enum.GetValues(typeof(Bioma)))
            lista.Add(CarregarConjunto(b));

        conjuntos = lista.ToArray();

        var relatorio = new System.Text.StringBuilder("IsoBoard — conjuntos carregados:");
        bool algumIncompleto = false;

        foreach (ConjuntoDoBioma c in conjuntos)
        {
            int faltando = 0;
            for (int mask = 1; mask <= 15; mask++)
                if (c.estradas[mask] == null) faltando++;

            bool incompleto = faltando > 0 || c.chao == null;
            algumIncompleto |= incompleto;

            relatorio.Append("  [").Append(c.bioma).Append(" chão=")
                     .Append(c.chao != null ? c.chao.name : "NULO")
                     .Append(" estradas=").Append(15 - faltando).Append("/15")
                     .Append(" decor=").Append(c.decor != null ? c.decor.Length : 0)
                     .Append(incompleto ? " <<< INCOMPLETO" : "").Append("]");
        }

        UnityEditor.EditorUtility.SetDirty(this);

        if (algumIncompleto) Debug.LogWarning(relatorio.ToString(), this);
        else Debug.Log(relatorio.ToString(), this);
    }

    private ConjuntoDoBioma CarregarConjunto(Bioma bioma)
    {
        const string ORIGINAL = "Assets/Isometric Tower defence pack/Isometric Tower defence pack/Sprites/";
        const string NATURE = "Assets/Isometric Nature Pack 2.0/Isometric Nature Pack 2.0/Sprites/";

        var c = new ConjuntoDoBioma();
        c.bioma = bioma;
        c.estradas = new Sprite[16];

        if (bioma == Bioma.Original)
        {
            c.chao = Carrega(ORIGINAL + "Enviroument tiles/ground.png");

            for (int mask = 1; mask <= 15; mask++)
                c.estradas[mask] = Carrega(ORIGINAL + "Road tiles/"
                    + IsoGrid.RoadSpriteName(mask, IsoGrid.Tileset.Original) + ".png");

            var decorO = new List<Sprite>();
            for (int i = 1; i <= 4; i++)
            {
                Adiciona(decorO, ORIGINAL + "Enviroument tiles/trees/tree(" + i + ").png");
                Adiciona(decorO, ORIGINAL + "Enviroument tiles/Stones/stone(" + i + ").png");
            }
            c.decor = decorO.ToArray();
        }
        else
        {
            // No Nature 2.0 o bioma é uma SUBPASTA e um SUFIXO no nome do arquivo ao mesmo tempo:
            // o clima padrão fica na raiz com nome road(N), e os outros dois em pastas próprias
            // com nome road_desert(N) / road_winter(N).
            string subRoad, subLand, subEnv, prefixo, chao;
            switch (bioma)
            {
                // O CHÃO LISO TEM NOME PRÓPRIO EM CADA BIOMA — grass, sand, snow —, e não segue o
                // padrão landscape_*(N). Os numerados são variações com RELEVO: escolher
                // "landscape_desert (1)" por analogia cobriu o tabuleiro inteiro de dunas
                // pontiagudas, e o tabuleiro de um tower defense precisa ser plano para a leitura
                // de posição funcionar. Verificado na tela antes de fechar.
                case Bioma.Deserto:
                    subRoad = "Road tiles/Desert tiles/"; subLand = "Landscape tiles/Desert tiles/";
                    subEnv = "Enviroument tiles/Desert tiles/"; prefixo = "road_desert";
                    chao = "sand";
                    break;
                case Bioma.Inverno:
                    subRoad = "Road tiles/Winter tiles/"; subLand = "Landscape tiles/Winter tiles/";
                    subEnv = "Enviroument tiles/Winter tiles/"; prefixo = "road_winter";
                    chao = "snow";
                    break;
                default: // Campo
                    subRoad = "Road tiles/"; subLand = "Landscape tiles/";
                    subEnv = "Enviroument tiles/"; prefixo = "road";
                    chao = "grass";
                    break;
            }

            c.chao = Carrega(NATURE + subLand + chao + ".png");

            for (int mask = 1; mask <= 15; mask++)
                c.estradas[mask] = Carrega(NATURE + subRoad
                    + IsoGrid.RoadSpriteName(mask, IsoGrid.Tileset.Nature, prefixo) + ".png");

            // Cada bioma tem um conjunto DIFERENTE de elementos — o deserto não tem árvore nem
            // arbusto, o inverno não tem pedra —, e os nomes também mudam (tree / tree_winter /
            // stone / stone_desert). Por isso cada tentativa é opcional: o que não existe é
            // ignorado e a lista final é o que sobrou. As nuvens ficam de fora de propósito: elas
            // não se apoiam no chão e o pivô de decoração as enterraria no tabuleiro.
            //
            // ATENÇÃO ao nome da pasta de cactos: o pacote a escreveu com um "с" CIRÍLICO
            // (U+0441), não o "c" latino — ver PastaDeCactos, montada a partir do código do caractere.
            var decorN = new List<Sprite>();
            for (int i = 1; i <= 8; i++)
            {
                Adiciona(decorN, NATURE + subEnv + "trees/tree(" + i + ").png");
                Adiciona(decorN, NATURE + subEnv + "trees/tree_winter(" + i + ").png");
                Adiciona(decorN, NATURE + subEnv + "stones/stone(" + i + ").png");
                Adiciona(decorN, NATURE + subEnv + "stones/stone_desert(" + i + ").png");
                Adiciona(decorN, NATURE + subEnv + "bushes/bush(" + i + ").png");
                Adiciona(decorN, NATURE + subEnv + PastaDeCactos + "/cactus(" + i + ").png");
            }
            c.decor = decorN.ToArray();
        }

        return c;
    }

    // O pacote escreveu a pasta de cactos com um "с" CIRÍLICO (U+0441 CYRILLIC SMALL LETTER ES) no
    // lugar do "c" latino. Montado a partir do código do caractere de propósito: escrito direto,
    // o nome fica indistinguível do latino na leitura e some em qualquer passagem por ferramenta
    // que normalize texto — e com a letra errada o caminho simplesmente não resolve, em silêncio.
    private static readonly string PastaDeCactos = "ca" + (char)0x0441 + "tuses";

    private static Sprite Carrega(string caminho)
    {
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
    }

    private static void Adiciona(List<Sprite> lista, string caminho)
    {
        Sprite s = Carrega(caminho);
        if (s != null) lista.Add(s);
    }
#endif
}
