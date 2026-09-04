using UnityEngine;

// Marca um SpriteRenderer como parte do EnemyStatusFX — IsoSorter usa isto pra NÃO sobrescrever
// a ordem de desenho dele (mesmo padrão de TowerStackBlock/RangeIndicator/EnemyTraitBadge).
public class EnemyStatusFXLayer : MonoBehaviour { }

// Estados DINÂMICOS que ligam e desligam em runtime e hoje não têm NENHUM feedback visual:
// `Health.IsMarked` (Detector marca o alvo pra levar mais dano) e o lento/congelado do Gelo
// (`EnemyMovement.IsSlowed`/`IsFrozen`). A mecânica existe e funciona — o jogador só não tem
// como VER que ela está acontecendo sem abrir o inspetor.
//
// `Health.IsShielded` (escudo do Shielder) já tinha o efeito dele: um blend de cor por cima do
// corpo, em Health.Update. Não duplicar aqui — por isso o vocabulário do ROADMAP 1.2: cor=ameaça,
// porte=massa, badge=traço ESTÁTICO (ver EnemyTraitBadge), efeito=estado DINÂMICO, e cada estado
// usa exatamente UM canal:
//   - marcado          -> anel pulsante ACIMA do UFO (não mexe no corpo)
//   - lento/congelado  -> véu translúcido por cima do corpo inteiro (não mexe em sr.color do
//     corpo — esse canal já é o eixo de ameaça e o blend de escudo)
//   - empurrado (Gelo/Ventania) -> não ganha efeito aqui de propósito: PushBack já move o
//     transform.position pra trás na hora, e esse recuo visível NA MESMA rota é o próprio feedback
//     (é um evento instantâneo, não um estado contínuo pra precisar de ícone).
//
// Anexado centralmente pelo EnemySpawner (mesmo padrão do IsoSorter): não é traço de nenhum dos
// 15 TIPOS, qualquer inimigo pode ser marcado ou congelado, então não pertence a prefab nenhum.
public class EnemyStatusFX : MonoBehaviour
{
    private static Sprite ringSprite;
    private static Sprite veilSprite;

    private Health health;
    private EnemyMovement movement;
    private SpriteRenderer markRenderer;
    private SpriteRenderer frostRenderer;
    private Vector3 origin;

    public static void Attach(GameObject go)
    {
        if (go == null || go.GetComponent<EnemyStatusFX>() != null) return;
        go.AddComponent<EnemyStatusFX>();
    }

    private void Awake()
    {
        health = GetComponent<Health>();
        movement = GetComponent<EnemyMovement>();
        origin = IsoBoard.main != null ? IsoBoard.main.Origin : Vector3.zero;

        GameObject markGo = new GameObject("MarkFX");
        markGo.transform.SetParent(transform, false);
        markGo.transform.localPosition = new Vector3(0f, 0.34f, 0f);
        markGo.transform.localScale = Vector3.one * 0.32f;
        markGo.AddComponent<EnemyStatusFXLayer>();
        markRenderer = markGo.AddComponent<SpriteRenderer>();
        markRenderer.sprite = RingSprite();
        markGo.SetActive(false);

        GameObject frostGo = new GameObject("FrostFX");
        frostGo.transform.SetParent(transform, false);
        frostGo.transform.localPosition = Vector3.zero;
        frostGo.transform.localScale = Vector3.one * 0.85f;
        frostGo.AddComponent<EnemyStatusFXLayer>();
        frostRenderer = frostGo.AddComponent<SpriteRenderer>();
        frostRenderer.sprite = VeilSprite();
        frostGo.SetActive(false);
    }

    private void LateUpdate()
    {
        int order = IsoGrid.SortingOrderAt(transform.position, origin);

        bool marked = health != null && health.IsMarked;
        if (marked)
        {
            if (!markRenderer.gameObject.activeSelf) markRenderer.gameObject.SetActive(true);
            float pulse = Mathf.Lerp(0.5f, 1f, (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f);
            markRenderer.color = new Color(1f, 0.2f, 0.15f, pulse); // vermelho-mira: "alvo travado"
            markRenderer.sortingOrder = order + 2; // acima até do selo estático (badge fica em +1)
        }
        else if (markRenderer.gameObject.activeSelf)
        {
            markRenderer.gameObject.SetActive(false);
        }

        bool frozen = movement != null && movement.IsFrozen;
        bool slowed = movement != null && movement.IsSlowed;
        if (slowed)
        {
            if (!frostRenderer.gameObject.activeSelf) frostRenderer.gameObject.SetActive(true);
            frostRenderer.color = new Color(0.6f, 0.9f, 1f, frozen ? 0.55f : 0.3f); // congelado = mais opaco
            frostRenderer.sortingOrder = order + 1;
        }
        else if (frostRenderer.gameObject.activeSelf)
        {
            frostRenderer.gameObject.SetActive(false);
        }
    }

    // Anel fino — não confundir com o disco cheio do selo estático (EnemyTraitBadge).
    private static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
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
                float a = (d <= 1f && d > 0.6f) ? 1f : 0f;
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return ringSprite;
    }

    // Disco cheio, mais suave nas bordas — véu por cima do corpo inteiro.
    private static Sprite VeilSprite()
    {
        if (veilSprite != null) return veilSprite;
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
                float a = d > 1f ? 0f : Mathf.Clamp01(1.15f - d);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        veilSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return veilSprite;
    }
}
