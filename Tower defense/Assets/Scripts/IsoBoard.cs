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
    [SerializeField] private Sprite[] decorSprites;
    [SerializeField, Range(0f, 1f)] private float decorChance = 0.5f;

    [Header("Sprites do pacote (preencha com 'Carregar sprites do pacote')")]
    [SerializeField] private Sprite groundSprite;
    [SerializeField] private Sprite[] roadSprites = new Sprite[16]; // indexado pela máscara 1..15

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
        const string BASE = "Assets/Isometric Tower defence pack/Isometric Tower defence pack/Sprites/";

        groundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BASE + "Enviroument tiles/ground.png");

        roadSprites = new Sprite[16];
        for (int mask = 1; mask <= 15; mask++)
        {
            string nome = IsoGrid.RoadSpriteName(mask);
            roadSprites[mask] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BASE + "Road tiles/" + nome + ".png");
        }

        var decor = new List<Sprite>();
        for (int i = 1; i <= 4; i++)
        {
            Sprite arv = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BASE + "Enviroument tiles/trees/tree(" + i + ").png");
            if (arv != null) decor.Add(arv);
            Sprite ped = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BASE + "Enviroument tiles/Stones/stone(" + i + ").png");
            if (ped != null) decor.Add(ped);
        }
        decorSprites = decor.ToArray();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("IsoBoard: sprites carregados (ground=" + (groundSprite != null) + ")");
    }
#endif
}
