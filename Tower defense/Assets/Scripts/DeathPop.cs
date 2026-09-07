using UnityEngine;

// Efeito de "estouro" ao inimigo morrer: copia o sprite dele, cresce e some rapidinho.
// Usa SpriteRenderer (renderiza no URP 2D com certeza).
public class DeathPop : MonoBehaviour
{
    private const float Life = 0.35f;

    private SpriteRenderer sr;
    private Vector3 baseScale;
    private float t;

    // Mesmo teto por frame do FloatingText, e pelo mesmo motivo: uma explosão que mata dezenas de
    // inimigos criava dezenas de GameObjects no mesmo quadro. Aqui é mais barato (um
    // SpriteRenderer, não um TextMeshPro), mas em volume também trava — e o estouro de cinquenta
    // inimigos simultâneos vira uma mancha só de qualquer jeito.
    private const int MaximoPorFrame = 12;
    private static int criadosNesteFrame;
    private static int frameDaContagem = -1;

    public static void Spawn(SpriteRenderer source)
    {
        if (source == null || source.sprite == null) return;

        if (frameDaContagem != Time.frameCount)
        {
            frameDaContagem = Time.frameCount;
            criadosNesteFrame = 0;
        }
        if (criadosNesteFrame >= MaximoPorFrame) return;
        criadosNesteFrame++;

        var go = new GameObject("DeathPop");
        go.transform.position = source.transform.position;
        go.transform.rotation = source.transform.rotation;
        go.transform.localScale = source.transform.lossyScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.color = source.color;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder + 1;

        go.AddComponent<DeathPop>();
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / Life);
        transform.localScale = baseScale * (1f + p * 0.9f); // cresce ~90%
        Color c = sr.color;
        c.a = 1f - p;                                        // some
        sr.color = c;
        if (t >= Life) Destroy(gameObject);
    }
}
