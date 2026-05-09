using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeUIController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject upgradePanel;           // Painel inteiro de upgrade
    [SerializeField] private Image towerIcon;                   // Ícone da torre atual
    [SerializeField] private Sprite[] levelSprites;             // Sprites por nível (0 = lvl1, 1 = lvl2, 2 = lvl3)
    [SerializeField] private TMP_Text levelText;                // "Nível X / 3"
    [SerializeField] private Image nextLevelPreview;            // Prévia do próximo nível
    [SerializeField] private TMP_Text nextCostText;             // Custo do próximo upgrade
    [SerializeField] private Button upgradeButton;              // Botão de upgrade
    [SerializeField] private TMP_Text upgradeButtonText;        // Texto dentro do botão

    public static UpgradeUIController uiController;

    private IUpgradable selectedTower; // Torre selecionada (qualquer tipo que implemente IUpgradable)
    private Color defaultButtonTextColor;

    private void Awake()
    {
        uiController = this;

        // Esconde painel no início
        if (upgradePanel != null)
            upgradePanel.SetActive(false);

        if (upgradeButtonText != null)
            defaultButtonTextColor = upgradeButtonText.color;

        // Conecta o botão de upgrade
        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(OnUpgradeButtonClicked);
    }

    public void ShowUpgradeUI(IUpgradable tower)
    {
        if (tower == null) return;

        selectedTower = tower;
        upgradePanel.SetActive(true);

        UpdateUI();
    }

    public void HideUpgradeUI()
    {
        upgradePanel.SetActive(false);
        selectedTower = null;
    }

    public void UpdateUI()
    {
        if (selectedTower == null) return;

        int currentLevel = selectedTower.GetCurrentLevel();
        int maxLvl = selectedTower.GetMaxLevel();

        // Ícone atual (se você tiver sprites diferentes por torre, veja nota abaixo)
        if (towerIcon != null && levelSprites != null && currentLevel - 1 < levelSprites.Length)
        {
            towerIcon.sprite = levelSprites[currentLevel - 1];
        }

        // Texto de nível
        if (levelText != null)
        {
            levelText.text = $"Nível {currentLevel} / {maxLvl}";
        }

        // Próximo nível e custo
        if (currentLevel < maxLvl)
        {
            int nextCost = selectedTower.CalculateNextCost();
            bool canAfford = nextCost <= LevelManager.main.currency;

            // Prévia do próximo
            if (nextLevelPreview != null && currentLevel < levelSprites.Length)
            {
                nextLevelPreview.sprite = levelSprites[currentLevel];
                nextLevelPreview.color = canAfford ? new Color(1f, 1f, 1f, 0.7f) : new Color(0.5f, 0.5f, 0.5f, 0.7f);
                nextLevelPreview.gameObject.SetActive(true);
            }

            // Custo
            if (nextCostText != null)
            {
                nextCostText.text = nextCost.ToString();
                nextCostText.color = canAfford ? Color.white : Color.red;
            }

            // Botão
            if (upgradeButton != null)
                upgradeButton.interactable = canAfford;

            if (upgradeButtonText != null)
            {
                upgradeButtonText.text = "Upgrade";
                upgradeButtonText.color = defaultButtonTextColor;
            }
        }
        else
        {
            if (nextLevelPreview != null)
                nextLevelPreview.gameObject.SetActive(false);

            if (nextCostText != null)
                nextCostText.text = "";

            if (upgradeButtonText != null)
            {
                upgradeButtonText.text = "MAX";
                upgradeButtonText.color = Color.gray;
            }

            if (upgradeButton != null)
                upgradeButton.interactable = false;
        }
    }

    public void OnUpgradeButtonClicked()
    {
        if (selectedTower != null)
        {
            selectedTower.Upgrade();
            UpdateUI(); // Atualiza imediatamente após upar
        }
    }
}