using UnityEngine;

// Matemática do tabuleiro isométrico, num lugar só.
//
// Os tiles do pacote têm o losango do topo em 2:1 (1202x601 px do PNG de 1202x1159), e foram
// importados com PPU 601 e pivô no CENTRO desse losango. Logo uma célula ocupa 2 x 1 unidades
// de mundo, e é o pivô que cai no ponto devolvido por CellToWorld — ou seja, posicionar um
// tile ali já o encaixa, e uma torre colocada ali já "senta" no chão.
//
// Board, plots, caminho e torres precisam concordar sobre onde fica cada célula: divergir
// nessa conta é o que produz tabuleiro com buraco e torre flutuando.
public static class IsoGrid
{
    public const float TileWidth = 2f;   // largura do losango do topo, em unidades
    public const float TileHeight = 1f;  // altura do losango do topo

    public const float StepX = TileWidth * 0.5f;   // 1.0
    public const float StepY = TileHeight * 0.5f;  // 0.5

    // Célula -> mundo (centro do losango do topo).
    // col cresce para a direita-baixo da tela (SE); row cresce para a esquerda-baixo (SW).
    public static Vector3 CellToWorld(int col, int row, Vector3 origin)
    {
        return new Vector3(
            origin.x + (col - row) * StepX,
            origin.y - (col + row) * StepY,
            0f);
    }

    // Mundo -> célula. Inverso exato do de cima; usado para descobrir em que célula caiu um clique.
    public static void WorldToCell(Vector3 world, Vector3 origin, out int col, out int row)
    {
        float dx = (world.x - origin.x) / StepX;
        float dy = (origin.y - world.y) / StepY;
        col = Mathf.RoundToInt((dx + dy) * 0.5f);
        row = Mathf.RoundToInt((dy - dx) * 0.5f);
    }

    // Em isométrico a ordem de desenho É a profundidade: quem está mais à frente (Y menor)
    // precisa desenhar por cima.
    //
    // Isto vai em sortingOrder explícito, e não no transparencySortAxis da câmera, porque a URP
    // ignora Camera.transparencySortMode — tentar por lá deixa a ordem entre os tiles arbitrária
    // e o tabuleiro desmonta visualmente.
    //
    // O passo entre células é largo de propósito: um inimigo parado ENTRE duas células recebe
    // uma ordem intermediária, e com passo curto essa ordem empata com a do tile e pisca.
    public const int CellStep = 100;
    public const int ObjectOffset = 50;

    public static int SortingOrder(int col, int row) => (col + row) * CellStep;

    // Ordem de um objeto pela posição no mundo. É o que os inimigos usam a cada frame: eles
    // andam entre células, então a ordem tem que acompanhar em vez de ficar fixa no prefab.
    public static int SortingOrderAt(Vector3 world, Vector3 origin)
    {
        float diagonal = (origin.y - world.y) / StepY; // = (col + row), em fração
        return Mathf.RoundToInt(diagonal * CellStep) + ObjectOffset;
    }

    // Contorno do losango do topo, em coordenadas locais — a forma real da célula.
    // Um BoxCollider2D quadrado erra o clique nas quinas; este é o polígono certo.
    public static Vector2[] DiamondShape()
    {
        return new[]
        {
            new Vector2(0f, TileHeight * 0.5f),   // topo
            new Vector2(TileWidth * 0.5f, 0f),    // direita
            new Vector2(0f, -TileHeight * 0.5f),  // baixo
            new Vector2(-TileWidth * 0.5f, 0f),   // esquerda
        };
    }

    // ───────── Conectividade das estradas ─────────
    // Bitmask das saídas de uma célula de caminho. Casa com a direção do vizinho no grid:
    // row-1 = NE, col+1 = SE, row+1 = SW, col-1 = NW.
    public const int NE = 1, SE = 2, SW = 4, NW = 8;

    // O tileset de estrada do pacote é COMPLETO: as 15 combinações não-vazias existem, uma por
    // arquivo. Este mapa foi levantado medindo os pixels nas bordas do losango de cada tile
    // (laranja = estrada, verde = grama), não no olho.
    private static readonly int[] RoadFile =
    {
        0,   // 0 = sem saída: não é estrada, usa o tile de chão
        11,  // 1  NE
        12,  // 2  SE
        3,   // 3  NE+SE
        13,  // 4  SW
        15,  // 5  NE+SW      (reta)
        2,   // 6  SE+SW
        9,   // 7  NE+SE+SW
        10,  // 8  NW
        5,   // 9  NE+NW
        14,  // 10 SE+NW      (reta)
        6,   // 11 NE+SE+NW
        4,   // 12 SW+NW
        7,   // 13 NE+SW+NW
        8,   // 14 SE+SW+NW
        1,   // 15 todas      (cruzamento)
    };

    // Nome do arquivo de estrada para uma máscara de conectividade (1..15).
    public static string RoadSpriteName(int mask)
    {
        if (mask <= 0 || mask > 15) return null;
        return "road(" + RoadFile[mask] + ")";
    }

    // ───────── Alcance em células ─────────
    // Todo alcance de torre é medido por Physics2D no MUNDO — um círculo. Mas a célula é 2x1 (ver
    // topo do arquivo), então esse círculo cobre o DOBRO de células na vertical do que na
    // horizontal: um raio "redondo" no mundo é uma ELIPSE quando contado em células. O Israel
    // decidiu que alcance tem que valer o mesmo em qualquer direção, contado em CÉLULAS — "alcance
    // 4" é 4 células pra qualquer lado, pra dar pra contar no tabuleiro. Achatar cada eixo pelo
    // tamanho da célula antes de medir a distância desfaz a distorção.
    public static float CellDistance(Vector2 a, Vector2 b)
    {
        float dx = (a.x - b.x) / TileWidth;
        float dy = (a.y - b.y) / TileHeight;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // Raio em unidades de MUNDO que, usado como pré-filtro num OverlapCircle, garante conter todo
    // alcance de cellRange células — inclusive na vertical, que precisa de mais unidades de mundo
    // por causa do achatamento acima (TileWidth > TileHeight). Só serve de pré-filtro barato: quem
    // chama ainda precisa descartar com CellDistance(...) > cellRange, senão sobra célula demais
    // na diagonal (ver o padrão de uso em Targeting.FindTarget).
    public static float WorldRadiusFor(float cellRange) => cellRange * TileWidth;
}
