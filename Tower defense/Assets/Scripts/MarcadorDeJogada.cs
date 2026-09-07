using TMPro;
using UnityEngine;

// Marcador visual de uma jogada do adversário: o anel no chão e o rótulo que dizem, NO TABULEIRO,
// o que a manchete disse em palavras.
//
// POR QUE ELE EXISTE. A regra da perda exige que toda ameaça a uma torre seja TELEGRAFADA. Uma
// manchete no topo da tela anuncia, mas não aponta: o jogador lê "ele marcou a sua Sniper" e
// ainda precisa descobrir qual das nove torres é a Sniper, com a onda já andando. O anel resolve
// isso no lugar onde a decisão vai ser tomada, que é o tabuleiro — anunciar sem apontar é meio
// telegrama.
//
// Pulsa de propósito: marcador estático some para o olho acostumado depois de alguns segundos, e
// o momento em que ele PRECISA ser visto é justamente quando a tela está cheia.
public class MarcadorDeJogada : MonoBehaviour
{
    private const float Periodo = 0.9f;

    private Transform seguir;
    private bool acompanhaAlguem;
    private TextMeshPro rotulo;
    private RangeIndicator anel;
    private Color cor;
    private float raio;

    // posicao: onde nasce. seguir: transform que ele acompanha (null = fica parado no chão).
    // raioEmCelulas: tamanho do anel na mesma unidade de alcance que o resto do jogo usa.
    public static MarcadorDeJogada Criar(Vector3 posicao, Transform seguir, float raioEmCelulas,
        string texto, Color cor)
    {
        var go = new GameObject("MarcadorDeJogada");
        go.transform.position = posicao;

        var m = go.AddComponent<MarcadorDeJogada>();
        m.seguir = seguir;
        m.acompanhaAlguem = seguir != null;
        m.cor = cor;
        m.raio = raioEmCelulas;

        // Reaproveita o anel elíptico do RangeIndicator em vez de desenhar um círculo próprio:
        // ele já resolve a proporção 2:1 do chão isométrico e a compensação de escala do pai, e é
        // o mesmo formato que o jogador já lê como "alcance" no resto do jogo.
        if (raioEmCelulas > 0f)
        {
            m.anel = RangeIndicator.Create(go.transform);
            m.anel.Show(raioEmCelulas, new Color(cor.r, cor.g, cor.b, 0.45f));
        }

        var textoGO = new GameObject("Rotulo");
        textoGO.transform.SetParent(go.transform, false);
        textoGO.transform.localPosition = Vector3.up * 0.9f;
        textoGO.transform.localScale = Vector3.one * 0.12f;

        m.rotulo = textoGO.AddComponent<TextMeshPro>();
        m.rotulo.text = texto;
        m.rotulo.fontSize = 9;
        m.rotulo.alignment = TextAlignmentOptions.Center;
        m.rotulo.color = cor;
        m.rotulo.fontStyle = FontStyles.Bold;
        m.rotulo.outlineWidth = 0.25f;
        m.rotulo.outlineColor = new Color32(10, 12, 16, 255);

        // Mesma lição do FloatingText: ordem fixa baixa fica ENTERRADA sob o chão isométrico, que
        // ordena por célula e passa de 2000 nas fases maiores. Aviso tem que ser legível sempre,
        // então vai para a camada acima do mundo.
        var mr = textoGO.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingLayerName = "UI";
            mr.sortingOrder = 100;
        }

        return m;
    }

    private void Update()
    {
        // A torre marcada pode CAIR no meio da onda — é literalmente o objetivo da jogada. Sem
        // isto o marcador ficaria órfão, apontando um plot vazio até o fim da rodada.
        if (acompanhaAlguem && seguir == null)
        {
            Destroy(gameObject);
            return;
        }
        if (seguir != null) transform.position = seguir.position;

        float p = 0.55f + 0.45f * Mathf.Sin(Time.time * (Mathf.PI * 2f / Periodo));
        if (rotulo != null) rotulo.color = new Color(cor.r, cor.g, cor.b, p);
        if (anel != null) anel.Show(raio, new Color(cor.r, cor.g, cor.b, 0.18f + 0.32f * p));
    }

    public void Encerrar()
    {
        if (this != null && gameObject != null) Destroy(gameObject);
    }
}
