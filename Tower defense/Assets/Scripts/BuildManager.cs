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
        if (Input.GetMouseButtonDown(0) && LevelManager.main.isDead == false)
        {
            // Se o mouse está sobre UI, ignora
            if (UIManager.main.IsHoveringUI())
                return;

            // Raycast para ver se clicou em algo
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

            // Se NÃO clicou em uma torre → deseleciona
            if (!hit.collider || hit.collider.GetComponent<IHasRange>() == null)
            {
                DeselectTower();
            }
        }
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