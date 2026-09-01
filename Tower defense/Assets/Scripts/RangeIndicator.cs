using UnityEngine;

// Indicador de alcance visual, compartilhado por todas as torres.
// Usa o sprite Assets/Resources/Range.png (tingido pela cor da torre); tem fallback procedural.
public class RangeIndicator : MonoBehaviour
{
    private static Sprite cachedSprite;
    private SpriteRenderer sr;

    public static RangeIndicator Create(Transform parent)
    {
        var go = new GameObject("RangeIndicator");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        var ri = go.AddComponent<RangeIndicator>();
        ri.sr = go.AddComponent<SpriteRenderer>();
        ri.sr.sprite = GetSprite();
        ri.sr.sortingOrder = 100; // translúcido, visível por cima
        go.SetActive(false);
        return ri;
    }

    // range = alcance em CÉLULAS (ver IsoGrid). O sprite é um anel circular; escalado igual nos
    // dois eixos ele desenharia um CÍRCULO no mundo, que é justamente a elipse que o alcance real
    // NÃO é mais (a decisão foi "mesmo alcance em células em qualquer direção" — ver IsoGrid).
    // Escalar X por TileWidth e Y por TileHeight faz o anel virar a elipse 2:1 certa: no chão
    // isométrico ela lê como um círculo de células, do jeito que o jogador consegue contar.
    public void Show(float range, Color color)
    {
        sr.color = color;
        Vector2 spriteSize = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;

        float diameterX = range * 2f * IsoGrid.TileWidth;
        float diameterY = range * 2f * IsoGrid.TileHeight;
        float scaleX = diameterX / Mathf.Max(0.0001f, spriteSize.x);
        float scaleY = diameterY / Mathf.Max(0.0001f, spriteSize.y);

        // Compensa a escala do pai, eixo a eixo: a torre pode crescer com upgrades (torres sem
        // TowerStack ainda usam scaleMult acumulado — ver TowerBase.Recalculate) e o anel não pode
        // crescer junto. Em torres COM TowerStack o pai fica travado em Vector3.one, então isto vira
        // 1 e não muda nada; sobrevive por causa das que ainda não têm pilha (e do Ally, que nunca tem).
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float parentX = Mathf.Abs(parentScale.x) < 0.0001f ? 1f : parentScale.x;
        float parentY = Mathf.Abs(parentScale.y) < 0.0001f ? 1f : parentScale.y;

        transform.localScale = new Vector3(scaleX / parentX, scaleY / parentY, 1f);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (gameObject != null) gameObject.SetActive(false);
    }

    private static Sprite GetSprite()
    {
        if (cachedSprite != null) return cachedSprite;
        cachedSprite = Resources.Load<Sprite>("Range");
        if (cachedSprite == null) cachedSprite = GenerateFallback();
        return cachedSprite;
    }

    // Fallback caso o sprite Range não seja encontrado.
    private static Sprite GenerateFallback()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f)) / r;
                float a = d > 1f ? 0f : (d > 0.9f ? 1f : 0.3f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
