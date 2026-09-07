using UnityEditor;
using UnityEngine;

// Aplica sozinho as convenções de import dos pacotes isométricos.
//
// POR QUE EXISTE. Os três pacotes de arte usam escalas diferentes — o tile tem 1202 px no pacote
// original, 291 no Nature 2.0 e 95 no Medieval —, e um sprite importado com o PPU errado
// simplesmente não encaixa na grade. São ~920 arquivos: configurar à mão é garantia de esquecer
// alguns, e sprite com pivô errado não dá erro, só desalinha de um jeito que se descobre tarde.
//
// AS DUAS REGRAS FORAM MEDIDAS, não supostas (2026-09-07):
//
// PPU. O pacote original usa PPU 601 para um tile de 1202 px, ou seja PPU = largura/2, o que faz
// uma célula ocupar exatamente 2 x 1 unidades — que é o que IsoGrid.TileWidth/TileHeight assumem.
// O PPU é uma constante POR PACOTE (derivada do tile padrão dele), e não por arquivo: uma árvore
// precisa da mesma escala do chão em que ela se apoia.
//
// PIVÔ. Confirmado contra o pacote original: pivô_y = 1 - (largura/4)/altura, que põe o pivô no
// centro do losango do topo. Para o tile de 1202x1159 a fórmula dá 0,740725 e o arquivo tem
// 0,74072474. Aqui ela é aplicada POR ARQUIVO, e não com um valor único como no pacote antigo:
// lá todas as alturas eram 1159 ou 1160 e um valor só custava 0,25 px, mas no Nature 2.0 as
// alturas vão de 172 a 282 e um pivô fixo desalinharia visivelmente.
//
// DECORAÇÃO É OUTRA CONVENÇÃO. Árvore, pedra e arbusto não são chão: eles se APOIAM no chão, e no
// pacote original têm pivô y = 0,12 — perto da base, não no centro do losango. Usar a fórmula do
// tile faria a árvore flutuar.
public class IsoPackImporter : AssetPostprocessor
{
    // Pacote -> largura do tile padrão dele. O PPU sai daqui (largura/2).
    private struct Pacote
    {
        public string raiz;
        public float larguraDoTile;
        public Pacote(string raiz, float larguraDoTile) { this.raiz = raiz; this.larguraDoTile = larguraDoTile; }
    }

    private static readonly Pacote[] Pacotes =
    {
        new Pacote("Assets/Isometric Nature Pack 2.0/", 291f),
        new Pacote("Assets/Isometric Medieval Pack/", 95f),
    };

    // O pacote original NÃO entra: ele já está configurado e funcionando, e reimportar 118 sprites
    // para ganhar 0,25 px de precisão é risco sem retorno.

    private void OnPreprocessTexture()
    {
        if (!TentaPacote(assetPath, out Pacote p)) return;

        var im = (TextureImporter)assetImporter;

        // Só age no import inicial. Depois disso o arquivo .meta manda, e reescrever a cada
        // reimport apagaria qualquer ajuste manual que alguém tenha feito de propósito.
        if (!im.importSettingsMissing) return;

        im.textureType = TextureImporterType.Sprite;
        im.spriteImportMode = SpriteImportMode.Single;
        im.spritePixelsPerUnit = p.larguraDoTile * 0.5f;
        im.filterMode = FilterMode.Bilinear;
        im.alphaIsTransparency = true;
        im.mipmapEnabled = false;
        im.maxTextureSize = 2048;

        // O pivô depende das dimensões, e o caminho óbvio (ajustar em OnPostprocessTexture, com a
        // Texture2D já pronta) NÃO funciona: naquele ponto o import corrente já resolveu o sprite,
        // e mexer no importer só teria efeito num reimport seguinte. Ler o cabeçalho do PNG aqui
        // resolve em uma passada — largura e altura moram nos bytes 16..23, big-endian.
        if (LeTamanhoPNG(assetPath, out int largura, out int altura))
            AplicaPivo(im, PivoY(assetPath, largura, altura));
    }

    // spriteAlignment, spritePivot, spriteExtrude e spriteMeshType NÃO são campos do
    // TextureImporter: eles vivem em TextureImporterSettings, e o único caminho é ler o bloco
    // inteiro, mexer e devolver. Escrever direto no importer compila em nada e falha silencioso.
    private static void AplicaPivo(TextureImporter im, float pivoY)
    {
        var s = new TextureImporterSettings();
        im.ReadTextureSettings(s);

        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = new Vector2(0.5f, pivoY);
        s.spriteExtrude = 1;
        s.spriteMeshType = SpriteMeshType.FullRect; // tile é retangular; Tight cortaria o losango

        im.SetTextureSettings(s);
    }

    private static float PivoY(string caminho, int largura, int altura)
    {
        if (EhDecoracao(caminho)) return PivoDaDecoracao;
        return 1f - (largura * 0.25f) / Mathf.Max(1, altura);
    }

    private static bool LeTamanhoPNG(string caminho, out int largura, out int altura)
    {
        largura = 0; altura = 0;
        try
        {
            using (var fs = System.IO.File.OpenRead(caminho))
            {
                var cab = new byte[24];
                if (fs.Read(cab, 0, 24) < 24) return false;
                // assinatura PNG: 89 50 4E 47
                if (cab[0] != 0x89 || cab[1] != 0x50 || cab[2] != 0x4E || cab[3] != 0x47) return false;

                largura = (cab[16] << 24) | (cab[17] << 16) | (cab[18] << 8) | cab[19];
                altura = (cab[20] << 24) | (cab[21] << 16) | (cab[22] << 8) | cab[23];
                return largura > 0 && altura > 0;
            }
        }
        catch { return false; }
    }

    // Medido no pacote original: tree(1) e stone(1) usam 0,12.
    private const float PivoDaDecoracao = 0.12f;

    private static bool TentaPacote(string caminho, out Pacote achado)
    {
        foreach (Pacote p in Pacotes)
        {
            if (caminho.StartsWith(p.raiz, System.StringComparison.OrdinalIgnoreCase))
            {
                achado = p;
                return true;
            }
        }
        achado = default;
        return false;
    }

    // Pelo NOME DA PASTA, não do arquivo: o pacote nomeia os arquivos de forma irregular
    // (cactus, stone_desert, tree_winter), mas as pastas são consistentes. "Enviroument" está
    // escrito errado no pacote, e é assim que tem de ser procurado.
    private static readonly string[] PastasDeDecoracao =
    {
        "/trees", "/bushes", "/stones", "/clouds", "/cactuses", "/caсtuses", "/Ground stones",
        "/Buildings", "/Castle walls", "/Towers",
    };

    private static bool EhDecoracao(string caminho)
    {
        foreach (string pasta in PastasDeDecoracao)
            if (caminho.IndexOf(pasta, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    // ───────── reaplicar nos que JÁ entraram ─────────

    // O OnPreprocess acima só vale no primeiro import, e os dois pacotes já entraram no projeto
    // com o PPU padrão de 100 — ou seja, na escala errada. Sem este comando o importador seria
    // código que existe e nunca chega ao jogo, que é o defeito recorrente desta base.
    [MenuItem("Tools/Isométrico/Reaplicar import dos pacotes")]
    public static void ReaplicarTudo()
    {
        int tocados = 0, tiles = 0, decor = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (Pacote p in Pacotes)
            {
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { p.raiz.TrimEnd('/') });
                foreach (string g in guids)
                {
                    string caminho = AssetDatabase.GUIDToAssetPath(g);
                    var im = AssetImporter.GetAtPath(caminho) as TextureImporter;
                    if (im == null) continue;

                    // Tamanho pelo cabeçalho do arquivo, e não pela Texture2D carregada: a textura
                    // importada pode ter vindo redimensionada por maxTextureSize, e o pivô tem de
                    // sair da geometria ORIGINAL do desenho.
                    if (!LeTamanhoPNG(caminho, out int largura, out int altura)) continue;

                    bool ehDecor = EhDecoracao(caminho);
                    float y = PivoY(caminho, largura, altura);

                    im.textureType = TextureImporterType.Sprite;
                    im.spriteImportMode = SpriteImportMode.Single;
                    im.spritePixelsPerUnit = p.larguraDoTile * 0.5f;
                    im.filterMode = FilterMode.Bilinear;
                    im.alphaIsTransparency = true;
                    im.mipmapEnabled = false;
                    AplicaPivo(im, y);

                    EditorUtility.SetDirty(im);
                    im.SaveAndReimport();

                    tocados++;
                    if (ehDecor) decor++; else tiles++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log("[IsoPackImporter] reaplicado em " + tocados + " texturas ("
            + tiles + " com pivô de tile, " + decor + " com pivô de decoração). "
            + "PPU: Nature 2.0 = 145,5 | Medieval = 47,5");
    }
}
