using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

// Onde o frame está sendo gasto — script, física, render ou coleta de lixo.
//
// POR QUE EXISTE. A sessão de 2026-09-05 otimizou cinco caminhos legítimos sem mover o ponteiro,
// porque a causa era outra (uma NRE por frame). O erro não foi otimizar mal, foi ESCOLHER O ALVO
// sem medir: o profiler dizia "GC" e a conclusão virou "o código aloca demais", quando a origem
// era exceção. Este probe existe para que a próxima escolha de alvo venha de número, não de
// suspeita.
//
// Ele lê os contadores do próprio Unity — os mesmos que a janela do Profiler mostra — e reduz o
// frame a poucos números que se somam. Se `script` domina, o alvo é código de jogo; se `fisica`
// domina, é collider/Rigidbody; se `render` domina, é draw call; se `alloc` é alto, é lixo.
//
// Os tempos usam Recorder (marcadores do player loop) e os contadores usam ProfilerRecorder, que
// é a API de CONTADOR — ler bytes de dentro de um marcador de tempo seria supor o significado do
// campo, e suposição não medida é como esta lista de armadilhas cresceu.
//
// FERRAMENTA DE DIAGNÓSTICO, não conteúdo: sem o componente na cena, nada disto roda.
public class PerfProbe : MonoBehaviour
{
    public static PerfProbe main;

    [Tooltip("Segundos REAIS entre relatórios (unscaled: o timeScale não deve mexer na medição).")]
    [SerializeField] private float intervalo = 5f;

    // Marcadores de TEMPO do player loop. Nem todo nome existe em toda versão do Unity, por isso
    // cada um passa por isValid — nome inválido devolve zero em silêncio, e zero silencioso é
    // exatamente como se perde uma sessão inteira.
    private Recorder recScript;   // todos os Update() de MonoBehaviour
    private Recorder recFixed;    // todos os FixedUpdate() — o que o timeScale multiplica
    private Recorder recFisica;   // passo do Physics2D
    private Recorder recRender;   // Camera.Render

    // O QUE NÃO É O JOGO. A primeira medição deu frame de 12,9 ms com script+física somando
    // 1,3 ms: 11,6 ms sem dono. Atribuir esse buraco ao jogo seria repetir o erro de 2026-09-05
    // em outra roupa. No Editor a maior parte dele costuma ser o próprio loop do Editor e a
    // espera pelo present da GPU — nenhum dos dois existe num build, e nenhum dos dois se
    // conserta mexendo em código de jogo.
    private Recorder recEditor;   // EditorLoop: a UI do Editor, não o jogo
    private Recorder recGfxWait;  // espera pelo present: sinal de VSync/GPU, não de CPU

    // CONTADORES. "GC Allocated In Frame" é o número que denuncia lixo por frame; "Draw Calls
    // Count" separa "o jogo pensa demais" de "o jogo desenha demais".
    private ProfilerRecorder cntAlloc;
    private ProfilerRecorder cntDrawCalls;

    private float proximo;
    private int frames;
    private float somaMs, piorMs;
    private float somaScript, somaFixed, somaFisica, somaRender, somaEditor, somaGfxWait;
    private long somaAlloc, somaDraw;
    private int colecoesNoInicio;

    private void OnEnable()
    {
        main = this;

        recScript = Recorder.Get("BehaviourUpdate");
        recFixed = Recorder.Get("FixedBehaviourUpdate");
        recFisica = Recorder.Get("Physics2D.Simulate");
        recRender = Recorder.Get("Camera.Render");
        recEditor = Recorder.Get("EditorLoop");
        recGfxWait = Recorder.Get("Gfx.WaitForPresentOnGfxThread");
        Ligar(recScript); Ligar(recFixed); Ligar(recFisica); Ligar(recRender);
        Ligar(recEditor); Ligar(recGfxWait);

        cntAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        cntDrawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");

        colecoesNoInicio = System.GC.CollectionCount(0);
        proximo = Time.unscaledTime + intervalo;
        Zerar();
    }

    private void OnDisable()
    {
        if (cntAlloc.Valid) cntAlloc.Dispose();
        if (cntDrawCalls.Valid) cntDrawCalls.Dispose();
    }

    private static void Ligar(Recorder r)
    {
        if (r != null && r.isValid) r.enabled = true;
    }

    private static float Ms(Recorder r)
    {
        if (r == null || !r.isValid) return 0f;
        return r.elapsedNanoseconds / 1000000f;
    }

    private static long Valor(ProfilerRecorder r)
    {
        return r.Valid ? r.LastValue : 0L;
    }

    private void LateUpdate()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        frames++;
        somaMs += ms;
        if (ms > piorMs) piorMs = ms;

        somaScript += Ms(recScript);
        somaFixed += Ms(recFixed);
        somaFisica += Ms(recFisica);
        somaRender += Ms(recRender);
        somaEditor += Ms(recEditor);
        somaGfxWait += Ms(recGfxWait);
        somaAlloc += Valor(cntAlloc);
        somaDraw += Valor(cntDrawCalls);

        if (Time.unscaledTime < proximo) return;
        proximo = Time.unscaledTime + intervalo;

        int n = Mathf.Max(1, frames);
        int colecoes = System.GC.CollectionCount(0) - colecoesNoInicio;
        colecoesNoInicio = System.GC.CollectionCount(0);

        // Uma linha só: o console do Unity trunca mensagem multilinha na listagem, e relatório que
        // não se lê pela listagem é relatório perdido.
        StringBuilder sb = new StringBuilder("[PERFPROBE] ");
        sb.Append("fps=").Append(Mathf.RoundToInt(1000f / Mathf.Max(0.01f, somaMs / n)))
          .Append(" frame=").Append((somaMs / n).ToString("0.0")).Append("ms")
          .Append(" pior=").Append(piorMs.ToString("0")).Append("ms")
          .Append(" || script=").Append((somaScript / n).ToString("0.0"))
          .Append(" fixed=").Append((somaFixed / n).ToString("0.0"))
          .Append(" fisica=").Append((somaFisica / n).ToString("0.0"))
          .Append(" render=").Append((somaRender / n).ToString("0.0"))
          .Append(" [editor=").Append((somaEditor / n).ToString("0.0"))
          .Append(" gfxwait=").Append((somaGfxWait / n).ToString("0.0")).Append("]")
          .Append(" || alloc=").Append((somaAlloc / n / 1024f).ToString("0")).Append("KB/frame")
          .Append(" coletas=").Append(colecoes)
          .Append(" draws=").Append(somaDraw / n)
          .Append(" || inimigos=").Append(Contar())
          .Append(" torres=").Append(TowerBase.Todas != null ? TowerBase.Todas.Count : -1)
          .Append(" transforms=").Append(Object.FindObjectsOfType<Transform>().Length);

        Debug.Log(sb.ToString());
        Zerar();
    }

    private void Zerar()
    {
        frames = 0; somaMs = 0f; piorMs = 0f;
        somaScript = somaFixed = somaFisica = somaRender = somaEditor = somaGfxWait = 0f;
        somaAlloc = 0; somaDraw = 0;
    }

    // Conta pelo spawner quando dá, para não pagar um FindObjectsOfType dentro do instrumento que
    // mede custo — medir com instrumento pesado já foi armadilha paga nesta base.
    private static int Contar()
    {
        if (EnemySpawner.main != null) return EnemySpawner.main.EnemiesAlive;
        return Object.FindObjectsOfType<EnemyMovement>().Length;
    }
}
