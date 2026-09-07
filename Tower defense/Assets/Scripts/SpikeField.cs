using UnityEngine;

// Campo de espinhos plantado sobre a rota — VERBO da Tachinha: negar área.
// Diferente de todas as outras torres, o dano não sai de um projétil mirado: fica no chão,
// esperando. Vale terreno, não alvo. Consome cargas a cada acerto e some quando acabam.
public class SpikeField : MonoBehaviour
{
    private static Sprite cachedSprite;

    private int damage;
    private int chargesLeft;
    private float radius;
    private float tickInterval;
    private LayerMask enemyMask;
    private bool seesCamo;
    private bool isSharp;
    private float lifetime;

    private float nextTick;
    private SpriteRenderer sr;

    // charges <= 0 significa campo permanente (não se gasta).
    // isSharp=true para espinhos (não furam chumbo); false para fogo/ácido, que ferem qualquer coisa.
    public static SpikeField Spawn(Vector3 position, float radius, int damage, int charges,
                                   float lifetime, float tickInterval, LayerMask enemyMask, bool seesCamo, Color color,
                                   bool isSharp = true)
    {
        var go = new GameObject("SpikeField");
        go.transform.position = position;

        var f = go.AddComponent<SpikeField>();
        f.damage = damage;
        f.chargesLeft = charges;
        f.radius = radius;
        f.lifetime = lifetime;
        f.tickInterval = tickInterval;
        f.enemyMask = enemyMask;
        f.seesCamo = seesCamo;
        f.isSharp = isSharp;

        f.sr = go.AddComponent<SpriteRenderer>();
        f.sr.sprite = GetSprite();
        f.sr.color = color;

        // "-1 fixo" parecia certo ("fica sob os inimigos") mas esquecia que o CHÃO isométrico usa
        // sortingOrder = (col+row)*100 (ver IsoGrid) — nunca negativo. Um valor fixo -1 ficava
        // ABAIXO de todo tile de chão, ou seja: debaixo do próprio terreno, sempre invisível.
        // A conta certa é relativa à célula onde o campo nasceu: um passo abaixo do que um
        // inimigo PARADO nesse mesmo ponto receberia (SortingOrderAt), então fica por CIMA do
        // chão e por BAIXO de quem estiver pisando ali.
        Vector3 origin = IsoBoard.main != null ? IsoBoard.main.Origin : Vector3.zero;
        f.sr.sortingOrder = IsoGrid.SortingOrderAt(position, origin) - 1;

        float d = f.sr.sprite.bounds.size.x;
        go.transform.localScale = Vector3.one * ((radius * 2f) / Mathf.Max(0.0001f, d));

        if (lifetime > 0f) Destroy(go, lifetime);
        return f;
    }

    private void Update()
    {
        if (Time.time < nextTick) return;
        nextTick = Time.time + tickInterval;

        int n = Targeting.Overlap(transform.position, radius, enemyMask);
        for (int i = 0; i < n; i++)
        {
            Collider2D hit = Targeting.Alcancados[i];
            if (hit == null) continue;
            if (IsoGrid.CellDistance(transform.position, hit.transform.position) > radius) continue;

            Health h = hit.GetComponent<Health>();
            if (h == null) continue;
            if (h.IsCamo && !seesCamo) continue;

            h.TakeDamage(damage, seesCamo, isSharp);

            if (chargesLeft > 0 && --chargesLeft <= 0)
            {
                Destroy(gameObject);
                return;
            }
        }

        // Desbota conforme as cargas acabam, para o jogador ver que o campo está se esgotando.
        if (chargesLeft > 0)
        {
            Color c = sr.color;
            c.a = Mathf.Lerp(0.25f, 0.8f, Mathf.Clamp01(chargesLeft / 20f));
            sr.color = c;
        }
    }

    private static Sprite GetSprite()
    {
        if (cachedSprite != null) return cachedSprite;

        // Disco pontilhado gerado em runtime — evita depender de um asset de arte.
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                // espinhos: raios finos saindo do centro
                float ang = Mathf.Atan2(dy, dx);
                float spikes = Mathf.Abs(Mathf.Cos(ang * 8f));
                float a = d > 1f ? 0f : (spikes > 0.75f ? 0.9f : 0.35f) * (1f - d * 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        cachedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedSprite;
    }
}
