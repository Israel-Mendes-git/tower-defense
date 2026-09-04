using UnityEngine;

// Selo pequeno sobre o inimigo para os traços que o jogador lia por tint de corpo inteiro e que a
// troca de arte (UFO colorido) não pode apagar: blindagem (Armored), chumbo/leadArmor (Lead) e
// regen. Cor agora é o EIXO DE AMEAÇA do UFO inteiro (ver IsoGrid/Health) — tingir o corpo de novo
// destruiria essa leitura. Em vez disso, um disquinho pequeno no canto, na convenção já usada por
// SpikeField/RangeIndicator (sprite procedural em runtime, cacheado).
//
// Fica na MESMA hierarquia do inimigo (child do prefab) para que o IsoSorter capture o renderer
// junto com o corpo — mas o IsoSorter PULA este componente de propósito (ver IsoSorter.Apply) e
// quem decide a ordem de desenho é este script, sempre um passo acima do corpo, para o selo nunca
// ficar escondido atrás do UFO.
public class EnemyTraitBadge : MonoBehaviour
{
    [SerializeField] private bool pulse;

    private static Sprite cachedSprite;
    private SpriteRenderer sr;
    private Vector3 origin;
    private Color baseColor;

    // Usado ao montar o prefab por código (ver Editor tooling) — evita expor o campo como público.
    public void Configure(bool pulseOn)
    {
        pulse = pulseOn;
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        if (sr.sprite == null) sr.sprite = GetSprite();
        baseColor = sr.color;
        origin = IsoBoard.main != null ? IsoBoard.main.Origin : Vector3.zero;
    }

    private void LateUpdate()
    {
        if (sr == null) return;

        sr.sortingOrder = IsoGrid.SortingOrderAt(transform.position, origin) + 1;

        if (pulse)
        {
            float a = Mathf.Lerp(0.55f, 1f, (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f);
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * a);
        }
    }

    private static Sprite GetSprite()
    {
        if (cachedSprite != null) return cachedSprite;

        // Disco cheio com aro mais opaco — lê como um "selo" a qualquer escala pequena.
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                float a = d > 1f ? 0f : (d > 0.68f ? 1f : 0.9f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        cachedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedSprite;
    }
}
