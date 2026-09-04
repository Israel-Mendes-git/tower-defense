using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BuildManager : MonoBehaviour
{
    public static BuildManager main;

    [Header("References")]
    [SerializeField] private Tower[] towers; // seus prefabs de torres

    public UnityEvent onBuildConsumed = new UnityEvent();

    private int selectedTower = -1;
    private bool canBuild = false;
    private int lastToggleFrame = -1;

    // Novo: Lista de todas as torretas que foram colocadas no mapa
    private List<GameObject> placedTowers = new List<GameObject>();

    private void Awake()
    {
        if (main != null && main != this)
        {
            Destroy(gameObject);
            return;
        }
        main = this;
        // DontDestroyOnLoad(gameObject); // opcional - se quiser persistir entre cenas
    }

    // Custo da OPÇÃO MAIS BARATA que entrega cada tipo de dano, lido do catálogo da loja.
    // Alimenta a regra de ouro do DefenseReadout: só é justo cobrar do jogador uma resposta
    // que ele consegue comprar agora. Sem isto, "sua defesa não tem resposta a chumbo" pode
    // significar tanto "você escolheu mal" quanto "o jogo não te deu a peça" — e o segundo
    // caso não é dificuldade, é armadilha.
    public Dictionary<DamageKind, int> CatalogoResumido()
    {
        var r = new Dictionary<DamageKind, int>();
        if (towers == null) return r;

        for (int i = 0; i < towers.Length; i++)
        {
            var t = towers[i];
            if (t == null || t.prefab == null) continue;

            // Torre bloqueada não conta como resposta disponível. É isto que faz a regra de
            // ouro do CounterCommander valer de verdade: ele não pode cobrar uma resposta que
            // o jogador ainda não conquistou o direito de comprar.
            if (!Unlocks.TorreLiberada(i)) continue;

            // Sem `??`: a sobrecarga de == do Unity não é respeitada pelo operador, e um
            // componente ausente voltaria como "fake null" (já custou 4 sons mudos aqui).
            TowerBase tb = t.prefab.GetComponent<TowerBase>();
            if (tb == null) tb = t.prefab.GetComponentInChildren<TowerBase>();
            if (tb == null) continue;

            DamageKind k = DefenseReadout.KindOf(tb);
            if (!r.ContainsKey(k) || t.cost < r[k]) r[k] = t.cost;
        }
        return r;
    }

    // Novo: Método para registrar uma torre quando ela é colocada
    public void RegisterPlacedTower(GameObject tower)
    {
        placedTowers.Add(tower);
    }

    // Novo: Método para remover da lista quando uma torre é destruída/vendida
    public void UnregisterPlacedTower(GameObject tower)
    {
        placedTowers.Remove(tower);
    }

    // Novo: Destrói TODAS as torretas colocadas e limpa a lista
    public void ResetAllTowers()
    {
        foreach (var tower in placedTowers)
        {
            if (tower != null)
            {
                Destroy(tower);
            }
        }
        placedTowers.Clear();

        // Limpa seleção atual
        ClearSelection();
    }


    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (LevelManager.main == null || LevelManager.main.isDead) return;
        if (UIManager.main != null && UIManager.main.IsHoveringUI()) return;
        if (Camera.main == null) return;

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // RaycastAll, não Raycast: o simples devolve apenas o primeiro collider, então um plot
        // sobreposto à torre fazia o clique contar como "clicou no vazio" e largava a seleção.
        RaycastHit2D[] hits = Physics2D.RaycastAll(mousePos, Vector2.zero);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null) continue;

            if (c.GetComponent<IHasRange>() != null || c.GetComponentInParent<IHasRange>() != null) return;

            // O plot ocupado conta como clique na torre: o Plot abre o painel de upgrade no mesmo
            // clique, e sem isto o painel seria fechado no mesmo frame em que acabou de abrir.
            Plot p = c.GetComponent<Plot>();
            if (p != null && p.towerObj != null) return;
        }

        // Clique no vazio FECHA o painel de upgrade — antes ele continuava aberto mostrando uma
        // torre que já nem estava mais selecionada. Esconder o alcance vem junto: HideUpgradeUI
        // chama MarkDeselected na torre, que chama HideRange.
        if (UIManager.main != null && UIManager.main.IsUpgradePanelOpen)
            UIManager.main.HideUpgradeUI();
    }

    // ADICIONE ESTE MÉTODO no seu BuildManager.cs (é essencial para o TowerButton acessar o custo)
    public Tower GetTower(int index)
    {
        if (index < 0 || index >= towers.Length)
            return null;
        return towers[index];
    }

    public void ToggleBuildMode(int towerIndex)
    {
        if (LevelManager.main.isDead == true) return;

        if (towerIndex < 0 || towerIndex >= towers.Length)
        {
            return;
        }

        // Torre ainda não conquistada: avisa em vez de simplesmente não responder. Botão que
        // não faz nada e não explica é o pior tipo de bug de interface — o jogador conclui que
        // o jogo travou, não que falta nível.
        if (!Unlocks.TorreLiberada(towerIndex))
        {
            // Mesmo padrão do "Faltam $X" do Plot: aviso flutuante no ponto do clique.
            if (Camera.main != null)
            {
                Vector3 mundo = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mundo.z = 0f;
                FloatingText.Spawn(mundo, "Nível " + Unlocks.NivelExigidoPelaTorre(towerIndex) + " para liberar",
                    new Color(1f, 0.45f, 0.45f));
            }
            return;
        }

        int currentFrame = Time.frameCount;

        // Se já está selecionada a mesma torre e não é o mesmo frame → desmarca
        if (selectedTower == towerIndex && canBuild && lastToggleFrame != currentFrame)
        {
            ClearSelection();
            lastToggleFrame = currentFrame;
            return;
        }

        // Seleciona (ou troca para outra torre)
        selectedTower = towerIndex;
        canBuild = true;
        lastToggleFrame = currentFrame;
    }
    // REMOVIDO: o par SelectTower(GameObject)/DeselectTower() e o campo selectedRange.
    // SelectTower nunca teve chamador — nem em código, nem como UnityEvent na cena — então
    // selectedRange era sempre nulo e DeselectTower era um no-op caro de entender. Quem mostra e
    // esconde o alcance de uma torre selecionada é a própria TowerBase (ShowRange/MarkDeselected),
    // acionada pelo Plot e pelo UIManager. Este par era um segundo mecanismo para a mesma coisa,
    // morto desde sempre; era ele que deixava o anel de alcance preso quando chamado à força.

    public bool CanBuild()
    {
        return canBuild && selectedTower >= 0;
    }

    public void ClearSelection()
    {
        selectedTower = -1;
        canBuild = false;
    }

    public Tower GetSelectedTower()
    {
        if (selectedTower < 0 || selectedTower >= towers.Length)
        {
            return null;
        }

        if (towers[selectedTower] == null)
        {
            return null;
        }

        return towers[selectedTower];
    }

    public int GetSelectedTowerIndex() => selectedTower;

    public void ConsumeBuild()
    {
        ClearSelection();
        onBuildConsumed?.Invoke();
    }
}