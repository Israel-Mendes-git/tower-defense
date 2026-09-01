using UnityEngine;

// Efeito de "estouro" ao inimigo morrer: copia o sprite dele, cresce e some rapidinho.
// Usa SpriteRenderer (renderiza no URP 2D com certeza).
public class DeathPop : MonoBehaviour
{
    private const float Life = 0.35f;

    private SpriteRenderer sr;
    private Vector3 baseScale;
    private float t;

    public static void Spawn(SpriteRenderer source)
    {
        if (source == null || source.sprite == null) return;

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
