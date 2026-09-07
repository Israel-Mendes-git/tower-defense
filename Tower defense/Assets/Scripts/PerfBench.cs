using System.Collections;
using System.Text;
using UnityEngine;

// Bancada de DEGRAUS: quantos inimigos o jogo aguenta, e o que estoura primeiro.
//
// POR QUE EXISTE, e por que não serve o AutoTeste. Medir performance jogando uma rodada alta tem
// três defeitos que já custaram uma sessão inteira:
//
//  1. A rodada 30 derrubou o Editor antes de emitir um único relatório — cenário que congela não
//     mede nada, só prova que congela.
//  2. A população real de uma rodada não é a fila de spawn. A cadeia do SpawnAfterDead é
//     multiplicativa (1 MOAB vira 26 unidades, 1 Boss vira 30), então "rodada 30" não é um número
//     de inimigos, é uma variável fora de controle.
//  3. A defesa muda junto: o TestPilot constrói conforme o dinheiro, e comparar dois pontos com
//     defesas diferentes não compara nada.
//
// Aqui a carga é imposta em degraus fixos, com a MESMA defesa, e o tipo spawnado é o Enemy básico
// — a folha da cadeia, que não gera filhote. Assim o eixo x é de verdade "número de inimigos".
//
// Depende do PerfProbe para o detalhamento do frame; esta classe só impõe a carga e anuncia o
// degrau, para o relatório do probe poder ser lido lado a lado com a contagem.
//
// FERRAMENTA DE DIAGNÓSTICO, não conteúdo: sem o componente na cena, nada disto roda.
public class PerfBench : MonoBehaviour
{
    [Tooltip("Quantos inimigos manter em campo em cada degrau.")]
    [SerializeField] private int[] degraus = { 25, 50, 100, 200, 400 };

    [Tooltip("Segundos REAIS medindo cada degrau, depois de a carga estar montada.")]
    [SerializeField] private float segundosPorDegrau = 8f;

    [Tooltip("Torres a construir antes de começar. A defesa fica FIXA durante toda a varredura.")]
    [SerializeField] private int torresAlvo = 10;

    // O Enemy básico é o índice 0 e é a folha da cadeia (não tem SpawnAfterDead). Spawnar Tank ou
    // MOAB faria a população se multiplicar durante a medição e o degrau perderia o sentido.
    [SerializeField] private int prefabDoInimigo = 0;

    // O alvo precisa SOBREVIVER ao degrau. A primeira versão spawnava o Enemy com a vida dele (2)
    // contra 10 torres: morriam no frame em que nasciam e todo relatório saiu com inimigos=0.
    // Vida alta não distorce a medida — o custo de um inimigo é transform, collider, sprite e
    // Update, nada disso depende de quantos pontos de vida ele tem.
    [SerializeField] private int vidaDoAlvo = 1000000;

    private void Start()
    {
        StartCoroutine(Rodar());
    }

    private IEnumerator Rodar()
    {
        yield return new WaitForSeconds(1f); // deixa LevelManager/BuildManager terminarem o Awake

        if (LevelManager.main == null || EnemySpawner.main == null)
        {
            Debug.LogWarning("[PERFBENCH] cena sem LevelManager/EnemySpawner — nada a fazer.");
            yield break;
        }

        // timeScale 1 é OBRIGATÓRIO aqui. Com timeScale alto o FixedUpdate roda até
        // maximumDeltaTime/fixedDeltaTime = 16,7 vezes por frame, e o frame medido passa a ser o
        // de uma máquina 16 vezes mais carregada. Foi assim que "34 inimigos a 2 FPS" entrou no
        // relatório de 2026-09-05, quando o número honesto era 87 FPS.
        Time.timeScale = 1f;
        QualitySettings.vSyncCount = 0;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Game");
#endif

        if (GetComponent<PerfProbe>() == null) gameObject.AddComponent<PerfProbe>();

        // Defesa fixa: dinheiro e rodadas fictícias só para o TestPilot construir, e DEPOIS ele
        // sai de cena — se continuasse, a defesa mudaria no meio da varredura.
        Unlocks.LiberarTudo = true;
        LevelManager.main.IncreaseCurrency(300000);
        if (GetComponent<TestPilot>() == null) gameObject.AddComponent<TestPilot>();
        for (int i = 0; i < 6; i++) EnemySpawner.onWaveComplete.Invoke();

        float limite = Time.unscaledTime + 20f;
        while (ContarTorres() < torresAlvo && Time.unscaledTime < limite)
            yield return new WaitForSeconds(0.5f);

        TestPilot tp = GetComponent<TestPilot>();
        if (tp != null) Destroy(tp);

        int torres = ContarTorres();
        Debug.Log("[PERFBENCH] defesa montada e CONGELADA: " + torres + " torres. Degraus: "
            + string.Join(", ", System.Array.ConvertAll(degraus, d => d.ToString())));

        GameObject prefab = null;
        if (EnemySpawner.main.EnemyPrefabs != null
            && prefabDoInimigo < EnemySpawner.main.EnemyPrefabs.Count)
            prefab = EnemySpawner.main.EnemyPrefabs[prefabDoInimigo];

        if (prefab == null)
        {
            Debug.LogWarning("[PERFBENCH] prefab de inimigo nao encontrado — abortado.");
            yield break;
        }

        foreach (int alvo in degraus)
        {
            yield return Encher(prefab, alvo);

            Debug.Log("[PERFBENCH] ===== DEGRAU " + alvo + " inimigos, " + torres
                + " torres — medindo " + segundosPorDegrau + "s =====");

            // Repõe durante a medição: os inimigos morrem e chegam ao fim do traçado, e um degrau
            // que se esvazia mede a queda da carga, não a carga.
            float ate = Time.unscaledTime + segundosPorDegrau;
            while (Time.unscaledTime < ate)
            {
                Repor(prefab, alvo);
                // Os alvos são imortais, mas continuam CHEGANDO ao fim do traçado e cobrando dano.
                // Sem repor a vida, o jogador morre no meio da varredura, a cena de derrota entra
                // e os degraus seguintes medem um jogo que já acabou.
                LevelManager.main.playerHP = 999999;
                yield return new WaitForSeconds(0.25f);
            }
        }

        Debug.Log("[PERFBENCH] fim da varredura.");
    }

    private IEnumerator Encher(GameObject prefab, int alvo)
    {
        // Em levas, não de uma vez: instanciar 400 GameObjects num frame cria um pico de alocação
        // que seria lido como custo do degrau, quando é custo da montagem dele.
        int guarda = 0;
        while (Vivos() < alvo && guarda++ < 100)
        {
            for (int i = 0; i < 25 && Vivos() < alvo; i++) Nascer(prefab);
            yield return null;
        }
        yield return new WaitForSeconds(1f); // deixa assentar antes de medir
    }

    private void Repor(GameObject prefab, int alvo)
    {
        int faltam = alvo - Vivos();
        for (int i = 0; i < faltam && i < 40; i++) Nascer(prefab);
    }

    // Mesmo caminho de nascimento do EnemySpawner.Spawn: sem o EnemyStatusFX e o IsoSorter o
    // inimigo custaria menos do que custa em jogo, e a medida sairia otimista.
    private void Nascer(GameObject prefab)
    {
        Transform origem = LevelManager.main.startPoint;
        if (origem == null) return;

        GameObject go = Object.Instantiate(prefab, origem.position, Quaternion.identity);
        EnemyStatusFX.Attach(go);
        IsoSorter.Attach(go, moves: true);
        Blindar(go);
    }

    // Vida por reflexão, e os DOIS campos. O Awake do Health já rodou dentro do Instantiate e
    // fixou maxHitPoints a partir de hitPoints; mexer só num deles deixaria o inimigo com teto de
    // cura errado ou morrendo do mesmo jeito. É ferramenta de bancada — em jogo ninguém faz isto.
    private void Blindar(GameObject go)
    {
        Health h = go.GetComponent<Health>();
        if (h == null) return;

        const System.Reflection.BindingFlags F =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        System.Reflection.FieldInfo hp = typeof(Health).GetField("hitPoints", F);
        System.Reflection.FieldInfo max = typeof(Health).GetField("maxHitPoints", F);
        if (hp != null) hp.SetValue(h, vidaDoAlvo);
        if (max != null) max.SetValue(h, vidaDoAlvo);
    }

    private static int Vivos()
    {
        return EnemySpawner.main != null ? EnemySpawner.main.EnemiesAlive : 0;
    }

    private static int ContarTorres()
    {
        return TowerBase.Todas != null ? TowerBase.Todas.Count : 0;
    }
}
