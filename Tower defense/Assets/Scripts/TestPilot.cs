using System.Collections.Generic;
using UnityEngine;

// Joga a partida sozinho, para que uma medição possa rodar contra uma partida REAL em vez de
// contra um tabuleiro montado à mão.
//
// Não é IA de jogo e não tenta ser boa: constrói perto do traçado e gasta o que tem, que é o
// comportamento que já serviu de referência antes (ver ROADMAP, Etapa 3 — "a IA de teste
// constrói burramente e ainda assim chegou à rodada 26"). O ponto não é jogar bem, é produzir
// uma defesa plausível, com torres subindo de tier ao longo das rodadas, para que medidas como
// a do OcclusionProbe tenham do que falar.
//
// FERRAMENTA DE DIAGNÓSTICO, não conteúdo do jogo: nada na cena depende dela, e ela só age se
// alguém a anexar em runtime.
public class TestPilot : MonoBehaviour
{
    // Quantas torres ele monta antes de passar a investir em altura. Acima disso o dinheiro vai
    // todo para upgrade, que é o que faz as pilhas crescerem — justamente o que se quer medir.
    [SerializeField] private int torresAlvo = 10;

    private void OnEnable() => EnemySpawner.onWaveComplete.AddListener(Jogar);
    private void OnDisable() => EnemySpawner.onWaveComplete.RemoveListener(Jogar);

    private void Start() => Jogar();

    private void Jogar()
    {
        LevelManager lm = LevelManager.main;
        if (lm == null || BuildManager.main == null) return;

        // Constrói enquanto faltar torre e houver caixa.
        int guarda = 0;
        while (ContarTorres() < torresAlvo && guarda++ < 20)
            if (!Construir(lm)) break;

        // O resto vai para altura. Sempre o upgrade mais barato disponível: é o que um jogador
        // faz quando não tem plano, e mantém a defesa subindo de tier de forma parelha.
        guarda = 0;
        while (guarda++ < 40)
            if (!Upar(lm)) break;
    }

    private static int ContarTorres()
    {
        var raizes = new List<Transform>();
        foreach (TowerBase t in FindObjectsOfType<TowerBase>())
            if (!raizes.Contains(t.transform.root)) raizes.Add(t.transform.root);
        return raizes.Count;
    }

    private bool Construir(LevelManager lm)
    {
        var caminho = new List<Vector3>();
        if (lm.startPoint != null) caminho.Add(lm.startPoint.position);
        if (lm.path != null) foreach (Transform t in lm.path) if (t != null) caminho.Add(t.position);
        if (caminho.Count == 0) return false;

        Plot melhor = null;
        float menorDistancia = float.MaxValue;
        foreach (Plot p in FindObjectsOfType<Plot>())
        {
            if (p.towerObj != null || !p.gameObject.activeInHierarchy) continue;
            float d = float.MaxValue;
            foreach (Vector3 c in caminho)
            {
                float x = IsoGrid.CellDistance(p.transform.position, c);
                if (x < d) d = x;
            }
            if (d < menorDistancia) { menorDistancia = d; melhor = p; }
        }
        if (melhor == null) return false;

        // Roda o catálogo para a defesa sair variada em vez de dez Basic Turret — uma defesa de
        // um tipo só não representa a silhueta média do tabuleiro, que é o que se quer medir.
        int inicio = ContarTorres();
        for (int i = 0; i < 9; i++)
        {
            Tower t = BuildManager.main.GetTower((inicio + i) % 9);
            if (t == null || t.prefab == null) continue;
            if (t.cost > lm.currency) continue;

            lm.SpendCurrency(t.cost);
            GameObject go = Instantiate(t.prefab, melhor.transform.position, Quaternion.identity);
            IsoSorter.Attach(go, false);
            TowerValue v = go.GetComponent<TowerValue>();
            if (v == null) v = go.AddComponent<TowerValue>();
            v.Init(t.cost, melhor);
            if (go.GetComponent<TowerIntegrity>() == null) go.AddComponent<TowerIntegrity>();
            BuildManager.main.RegisterPlacedTower(go);
            melhor.towerObj = go;
            return true;
        }
        return false;
    }

    private bool Upar(LevelManager lm)
    {
        TowerBase alvo = null;
        int trilha = 0;
        int menorCusto = int.MaxValue;

        foreach (TowerBase t in FindObjectsOfType<TowerBase>())
        {
            for (int p = 0; p < 2; p++)
            {
                UpgradeTier prox = t.NextTier(p);
                if (prox == null) continue;
                if (t.IsPathLocked(p)) continue;
                if (prox.cost > lm.currency) continue;
                if (prox.cost < menorCusto) { menorCusto = prox.cost; alvo = t; trilha = p; }
            }
        }
        if (alvo == null) return false;

        alvo.UpgradePath(trilha);
        return true;
    }
}
