using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Caça "torre piscando": detecta, FRAME A FRAME, quem está alternando propriedade visual.
//
// Piscar é sempre a mesma história — dois sistemas escrevendo a mesma propriedade em ordens
// diferentes, cada um desfazendo o do outro. O olho vê cintilação; o código não denuncia nada,
// porque cada escrita isolada está correta. A única forma honesta de achar é medir por frame e
// perguntar quem alternou, quantas vezes, e entre quais valores.
//
// OBSERVADOR PURO: não escreve nada em ninguém. Anexado em runtime; nada na cena depende dele.
public class FlickerProbe : MonoBehaviour
{
    // Quantas alternâncias num sprite para ele contar como "piscando". Duas trocas podem ser uma
    // transição legítima (a torre foi sabotada e voltou); dezenas são disputa.
    [SerializeField] private int limiteDeAlternancias = 6;

    private class Registro
    {
        public string caminho;
        public Color ultimaCor;
        public bool ultimoEnabled;
        public int ultimaOrdem;
        public int trocasDeCor, trocasDeEnabled, trocasDeOrdem;
        public Color corA, corB;

        // O QUE REALMENTE MEDE PISCAR. Contar trocas não serve sozinho: uma transição suave de
        // 12 frames conta 12 trocas e não pisca nada, enquanto um corte único de branco para
        // cinza conta 1 e salta na cara. O que o olho vê é o DEGRAU — o quanto o brilho mudou
        // de um frame para o seguinte.
        public float maiorDegrau;
    }

    private static float Luminancia(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

    private readonly Dictionary<int, Registro> registros = new Dictionary<int, Registro>();
    private int framesObservados;

    public static FlickerProbe main;
    private void Awake() => main = this;

    private void LateUpdate() => Amostrar();

    // Roda no fim do frame, depois de todo mundo já ter escrito. É de propósito: o que interessa
    // é o valor com que o frame FOI DESENHADO, não os intermediários.
    private void Amostrar()
    {
        framesObservados++;

        foreach (TowerBase t in FindObjectsOfType<TowerBase>())
        {
            Transform raiz = t.transform.root;
            foreach (SpriteRenderer sr in raiz.GetComponentsInChildren<SpriteRenderer>(true))
            {
                int id = sr.GetInstanceID();
                Registro r;
                if (!registros.TryGetValue(id, out r))
                {
                    r = new Registro();
                    r.caminho = raiz.name + "/" + sr.gameObject.name;
                    r.ultimaCor = sr.color;
                    r.ultimoEnabled = sr.enabled;
                    r.ultimaOrdem = sr.sortingOrder;
                    r.corA = sr.color;
                    registros[id] = r;
                    continue;
                }

                // SÓ CONTA O QUE É DESENHADO. Sprite desabilitado pode trocar de cor à vontade
                // que ninguém vê — e contá-lo produz um falso positivo caro: o placeholder da
                // raiz das torres fica desligado desde que a pilha nasce (ver TowerStack), e ele
                // sozinho acusava um degrau de 0,678 que não existe na tela.
                if (sr.enabled && r.ultimoEnabled && sr.color != r.ultimaCor)
                {
                    r.trocasDeCor++;
                    float degrau = Mathf.Abs(Luminancia(sr.color) - Luminancia(r.ultimaCor));
                    if (degrau > r.maiorDegrau) { r.maiorDegrau = degrau; r.corB = r.ultimaCor; r.corA = sr.color; }
                }
                r.ultimaCor = sr.color;

                // Aparecer/sumir também é piscada, e das piores: o degrau é a luminância inteira.
                if (sr.enabled != r.ultimoEnabled)
                {
                    r.trocasDeEnabled++;
                    float degrau = Luminancia(sr.color);
                    if (degrau > r.maiorDegrau) { r.maiorDegrau = degrau; r.corA = sr.color; r.corB = Color.clear; }
                    r.ultimoEnabled = sr.enabled;
                }
                if (sr.sortingOrder != r.ultimaOrdem) { r.trocasDeOrdem++; r.ultimaOrdem = sr.sortingOrder; }
            }
        }
    }

    public string Relatorio()
    {
        var sb = new StringBuilder();
        sb.AppendLine("frames observados: " + framesObservados + "  sprites vigiados: " + registros.Count);

        // Ordena pelo maior degrau: quem salta mais brilho de um frame para o outro é quem pisca.
        var lista = new List<Registro>(registros.Values);
        lista.Sort(delegate (Registro a, Registro b) { return b.maiorDegrau.CompareTo(a.maiorDegrau); });

        float pior = 0f;
        int mostrados = 0;
        foreach (Registro r in lista)
        {
            int total = r.trocasDeCor + r.trocasDeEnabled + r.trocasDeOrdem;
            if (total < limiteDeAlternancias) continue;
            if (r.maiorDegrau > pior) pior = r.maiorDegrau;
            if (mostrados++ >= 8) continue;
            sb.AppendLine("  " + r.caminho
                + "  maiorDegrau=" + r.maiorDegrau.ToString("0.000")
                + "  trocas: cor=" + r.trocasDeCor + " enabled=" + r.trocasDeEnabled + " ordem=" + r.trocasDeOrdem
                + "  " + Descrever(r.corB) + " -> " + Descrever(r.corA));
        }
        sb.AppendLine("PIOR DEGRAU DE BRILHO ENTRE FRAMES: " + pior.ToString("0.000")
            + "   (acima de ~0,15 o olho lê como piscada)");
        return sb.ToString();
    }

    // Resumo em UMA LINHA. O console do Unity mostra só a primeira linha de uma mensagem
    // multilinha na listagem, então um relatório bonito de várias linhas é ilegível justamente
    // pelo canal por onde ele precisa sair.
    public string ResumoNumaLinha()
    {
        // PISCAR É ALTERNÂNCIA REPETIDA, não uma transição única. Sem este filtro o resumo
        // acusava 1,000 por causa do placeholder da raiz sendo desligado UMA vez, no instante em
        // que a pilha de blocos nasce — evento correto, que acontece uma vez na vida da torre.
        float pior = 0f;
        string culpado = "-";
        int oscilando = 0;
        foreach (var kv in registros)
        {
            Registro r = kv.Value;
            if (r.trocasDeCor + r.trocasDeEnabled + r.trocasDeOrdem < limiteDeAlternancias) continue;
            oscilando++;
            if (r.maiorDegrau > pior) { pior = r.maiorDegrau; culpado = r.caminho; }
        }
        return "pior_degrau_de_brilho=" + pior.ToString("0.000")
            + " (limiar ~0,15)  em=" + culpado
            + "  sprites_oscilando=" + oscilando + "/" + registros.Count
            + "  (min " + limiteDeAlternancias + " alternancias)  frames=" + framesObservados;
    }

    private static string Descrever(Color c)
        => "(" + c.r.ToString("0.00") + "," + c.g.ToString("0.00") + "," + c.b.ToString("0.00")
           + " a=" + c.a.ToString("0.00") + ")";
}
