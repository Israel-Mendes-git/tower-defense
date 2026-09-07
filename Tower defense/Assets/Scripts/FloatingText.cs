using UnityEngine;
using TMPro;

// Texto flutuante no mundo (ex: "+10" ao matar um inimigo): sobe e some.
public class FloatingText : MonoBehaviour
{
    private const float Life = 0.8f;
    private const float RiseSpeed = 1.3f;

    private TextMeshPro tmp;
    private float t;

    // scale: porte do que gerou o texto (transform.localScale.x de quem chamou). Default 1 == o
    // comportamento de sempre. Os UFOs vão de 1.2 a 3.3 (ver EnemyTraitBadge/IsoBoard) — um texto
    // que nasce sempre 0.3 acima da ORIGEM (o pé do UFO, não o corpo) fica dentro/abaixo do
    // próprio inimigo quando ele é grande; escalar a subida junto resolve sem mexer no tamanho da
    // fonte (números continuam do mesmo tamanho, só nascem mais alto sobre corpos maiores).
    // TETO POR FRAME. Cada texto cria um GameObject com um TextMeshPro, e TextMeshPro é caro de
    // nascer (gera malha, resolve fonte). Numa explosão que mata cinquenta inimigos de uma vez,
    // são cinquenta deles no mesmo quadro — medido, frames de até 13,5 SEGUNDOS numa onda cheia.
    //
    // Descartar o excesso não custa informação: ninguém lê cinquenta números sobrepostos no mesmo
    // instante. O que o jogador precisa ver é QUE está matando, e os primeiros já dizem isso.
    private const int MaximoPorFrame = 8;
    private static int criadosNesteFrame;
    private static int frameDaContagem = -1;

    public static void Spawn(Vector3 worldPos, string text, Color color, float scale = 1f)
    {
        if (frameDaContagem != Time.frameCount)
        {
            frameDaContagem = Time.frameCount;
            criadosNesteFrame = 0;
        }
        if (criadosNesteFrame >= MaximoPorFrame) return;
        criadosNesteFrame++;

        var go = new GameObject("FloatingText");
        go.transform.position = worldPos + Vector3.up * (0.3f * Mathf.Max(0.5f, scale));
        go.transform.localScale = Vector3.one * 0.12f;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 8;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;

        // Contorno escuro fixo: sem isto, texto claro (ex.: o "+$" verde-claro do Farm/Ladrão)
        // desaparece sobre a grama clara do tabuleiro — a mesma cor que combina com o fundo escuro
        // da UI falha justamente sobre o board, que é onde a maioria destes textos nasce.
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(10, 12, 16, 255);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            // "200" foi escrito quando o tabuleiro era top-down e nada passava disso. O chão
            // isométrico ordena por célula ((col+row)*100, ver IsoGrid) e chega a 2200 nas fases
            // maiores, então a ordem fixa enterrava o texto sob o PRÓPRIO tabuleiro em quase todo
            // o mapa — medido: numa célula mediana o chão está em 1000. Texto de feedback tem que
            // ser sempre legível, então sobe para a camada UI, acima de qualquer coisa do mundo.
            mr.sortingLayerName = "UI";
            mr.sortingOrder = 100;
        }

        go.AddComponent<FloatingText>().tmp = tmp;
    }

    private void Update()
    {
        t += Time.deltaTime;
        transform.position += Vector3.up * RiseSpeed * Time.deltaTime;
        float p = Mathf.Clamp01(t / Life);
        Color c = tmp.color;
        c.a = 1f - p;
        tmp.color = c;
        if (t >= Life) Destroy(gameObject);
    }
}
