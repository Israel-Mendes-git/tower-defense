using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Conjunto de sprites empilháveis do "Isometric Tower defence pack" (4 cores x até 11 blocos),
// um array por cor. Vive em Resources porque TowerStack precisa carregar isto em RUNTIME (inclusive
// em build), onde AssetDatabase não existe — mesma convenção que o projeto já usa (ver Range.png
// em Assets/Resources). Populado uma vez pelo editor; em jogo é só leitura.
[CreateAssetMenu(fileName = "TowerBlockPalette", menuName = "Tower Defense/Tower Block Palette")]
public class TowerBlockPalette : ScriptableObject
{
    [System.Serializable]
    public class ColorSet
    {
        // Índice 0 = block(1).png ... índice 10 = block(11).png.
        // A cor Yellow do pacote só tem 10 arquivos: o índice 10 fica null de propósito.
        public Sprite[] blocks = new Sprite[11];
    }

    public ColorSet green;
    public ColorSet purple;
    public ColorSet red;
    public ColorSet yellow;

    // blockNumber é 1-based (o nome do arquivo: block(1)..block(11)). Devolve null se a cor não
    // tiver aquele bloco (caso do Yellow com block(11)) — quem monta a pilha tem que tolerar isso.
    public Sprite Block(TowerStack.BlockColor color, int blockNumber)
    {
        ColorSet set = color == TowerStack.BlockColor.Green ? green
                     : color == TowerStack.BlockColor.Purple ? purple
                     : color == TowerStack.BlockColor.Red ? red
                     : yellow;
        if (set == null || set.blocks == null) return null;
        int i = blockNumber - 1;
        return (i >= 0 && i < set.blocks.Length) ? set.blocks[i] : null;
    }

#if UNITY_EDITOR
    // Mesmo padrão do IsoBoard.LoadSpritesFromPackage: fica no editor de propósito, os sprites
    // ficam serializados no .asset depois de rodar isto uma vez.
    [ContextMenu("Carregar blocos do pacote")]
    public void LoadFromPackage()
    {
        const string BASE = "Assets/Isometric Tower defence pack/Isometric Tower defence pack/Sprites/Tower tiles/";
        green = LoadSet(BASE + "Green tower/", 11);
        purple = LoadSet(BASE + "Purple tower/", 11);
        red = LoadSet(BASE + "Red tower/", 11);
        yellow = LoadSet(BASE + "Yellow tower/", 10); // pack não tem block(11) amarelo

        EditorUtility.SetDirty(this);
        Debug.Log("TowerBlockPalette: blocos carregados (green=" + Count(green) + " purple=" + Count(purple)
            + " red=" + Count(red) + " yellow=" + Count(yellow) + ")");
    }

    private static int Count(ColorSet set)
    {
        if (set == null || set.blocks == null) return 0;
        int n = 0;
        for (int i = 0; i < set.blocks.Length; i++) if (set.blocks[i] != null) n++;
        return n;
    }

    private static ColorSet LoadSet(string folder, int count)
    {
        var set = new ColorSet { blocks = new Sprite[11] };
        for (int i = 1; i <= count; i++)
            set.blocks[i - 1] = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "block(" + i + ").png");
        return set;
    }
#endif
}
