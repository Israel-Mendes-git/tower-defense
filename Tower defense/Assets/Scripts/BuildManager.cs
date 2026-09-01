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

        // Clique no vazio: além de esconder o alcance, FECHA o painel de upgrade — antes ele
        // continuava aberto mostrando uma torre que já nem estava mais selecionada.
        DeselectTower();
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
    private IHasRange selectedRange;

    public void SelectTower(GameObject tower)
    {
        if(LevelManager.main.isDead == true) return;

        if (selectedRange != null)
            selectedRange.HideRange();

        selectedRange = tower.GetComponent<IHasRange>();

        if (selectedRange != null)
            selectedRange.ShowRange();
    }

    public void DeselectTower()
    {
        if (selectedRange != null)
            selectedRange.HideRange();

        selectedRange = null;
    }


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