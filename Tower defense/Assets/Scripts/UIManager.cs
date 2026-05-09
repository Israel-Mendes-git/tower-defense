using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager main;

    private bool isHoveringUI;

    [Header("Upgrade UI Elements")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private Image towerIcon;
    [SerializeField] private Sprite[] levelSprites;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image nextLevelPreview;
    [SerializeField] private TMP_Text nextCostText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradeButtonText;

    private IUpgradable selectedTower;
    private Color defaultButtonTextColor;

    private void Awake()
    {
        main = this;

        if (upgradePanel != null)
            upgradePanel.SetActive(false);

        if (upgradeButtonText != null)
            defaultButtonTextColor = upgradeButtonText.color;

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(OnUpgradeClicked);

        if (LevelManager.main != null)
        {
            LevelManager.onCurrencyChanged.AddListener(UpdateUpgradeButtonState);
        }
    }

    private void OnDestroy()
    {
        // Limpa listener (opcional)
        // LevelManager.main.onCurrencyChanged.RemoveListener(UpdateUpgradeButtonState);
    }

    public void SetHoveringState(bool state)
    {
        isHoveringUI = state;
    }

    public bool IsHoveringUI()
    {
        return isHoveringUI;
    }

    public void ShowUpgradeUI(IUpgradable tower)
    {
        if (tower == null) return;

        selectedTower = tower;
        if (upgradePanel != null)
            upgradePanel.SetActive(true);

        UpdateUpgradeUI();
    }

    public void HideUpgradeUI()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(false);

        selectedTower = null;
    }

    public void UpdateUpgradeUI()
    {
        if (selectedTower == null) return;

        Turret turret = selectedTower as Turret;
        if (turret == null) return;

        int currentLevel = turret.level;
        int maxLvl = turret.maxLevel;

        // Ícone atual
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
        UpdateUpgradeButtonState();
    }

    // Método chamado sempre que moeda muda ou painel abre
    private void UpdateUpgradeButtonState()
    {
        if (selectedTower == null || upgradeButton == null) return;

        Turret turret = selectedTower as Turret;
        if (turret == null) return;

        if (turret.level >= turret.maxLevel)
        {
            if (upgradeButtonText != null)
            {
                upgradeButtonText.text = "MAX";
                upgradeButtonText.color = Color.gray;
            }
            upgradeButton.interactable = false;
            return;
        }

        int nextCost = turret.CalculateCost();
        bool canAfford = nextCost <= LevelManager.main.currency;

        // Prévia
        if (nextLevelPreview != null)
        {
            if (turret.level < levelSprites.Length)
            {
                nextLevelPreview.sprite = levelSprites[turret.level];
                nextLevelPreview.color = canAfford ? new Color(1f, 1f, 1f, 0.7f) : new Color(0.5f, 0.5f, 0.5f, 0.7f);
                nextLevelPreview.gameObject.SetActive(true);
            }
            else
            {
                nextLevelPreview.gameObject.SetActive(false);
            }
        }

        // Custo
        if (nextCostText != null)
        {
            nextCostText.text = nextCost.ToString();
            nextCostText.color = canAfford ? Color.white : Color.red;
        }

        // Botão
        upgradeButton.interactable = canAfford;

        if (upgradeButtonText != null)
        {
            upgradeButtonText.text = canAfford ? "Upgrade" : "Upgrade";
            upgradeButtonText.color = defaultButtonTextColor;
        }
    }

    private void OnUpgradeClicked()
    {
        if (selectedTower != null)
        {
            selectedTower.Upgrade();
            UpdateUpgradeUI(); // Atualiza tudo após upar
        }
    }
}