using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TowerButton : MonoBehaviour
{
    [SerializeField] private int towerIndex = 0;
    [SerializeField] private GameObject aura; // highlight / borda / glow
    [SerializeField] private TMP_Text costTxt;

    private Button button;
    private bool costSet = false; // Para setar o custo só uma vez (otimização)

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);

        if (aura != null)
            aura.SetActive(false);
    }

    private void Update()
    {
        if (BuildManager.main == null) return;

        Tower tower = BuildManager.main.GetTower(towerIndex);
        if (tower == null) return;

        // Seta o custo só uma vez
        if (costTxt != null && !costSet)
        {
            costTxt.text = tower.cost.ToString();
            costSet = true;
        }
        else if (costTxt != null)
        {
            // Muda cor se não tem dinheiro
            costTxt.color = (tower.cost > LevelManager.main.currency) ? Color.red : Color.white;
        }

        // Atualiza aura de seleção
        bool isSelected = BuildManager.main.GetSelectedTowerIndex() == towerIndex;
        if (aura != null)
            aura.SetActive(isSelected);
    }


    private void OnClick()
    {
        if (BuildManager.main == null) return;
        BuildManager.main.ToggleBuildMode(towerIndex);
    }
}