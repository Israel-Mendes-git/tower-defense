using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class IceTurret : MonoBehaviour, IUpgradable
{
    [Header("References")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private GameObject upgradeUI;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private int baseUpgradeCost = 100;
    [SerializeField] private TMP_Text upgradeCostTxt;

    [Header("Attribute")]
    [SerializeField] private float targetingRange = 5f;
    [SerializeField] private float aps = 1f;
    [SerializeField] private float freezeTime = 1f;

    [Header("Range Visual")]
    [SerializeField] private GameObject rangeIndicator; // ← Arraste o GameObject do círculo aqui
    [SerializeField] private float rangePadding = 0.1f; // Espaço extra (opcional)

    [Header("Upgrade Settings")]
    [SerializeField] private int maxLevel = 3;

    private float apsBase;
    private float targetingRangeBase;
    private int level = 1; // Começa no nível 1
    private float timeUntilFire;
    private bool isSelected = false; // Controla se está "selecionado" (clicado)
    private Color defaultTextColor;

    private void Start()
    {
        apsBase = aps;
        targetingRangeBase = targetingRange;

        if (upgradeCostTxt != null)
            defaultTextColor = upgradeCostTxt.color;

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(Upgrade);

        // Começa tudo escondido
        if (rangeIndicator != null)
            rangeIndicator.SetActive(false);
        if (upgradeUI != null)
            upgradeUI.SetActive(false);

        UpdateRangeIndicator();
        UpdateUpgradeCostText();
    }

    private void Update()
    {
        timeUntilFire += Time.deltaTime;
        if (timeUntilFire >= 1f / aps)
        {
            FreezeEnemies();
            timeUntilFire = 0f;
        }

        // Atualiza custo se upgrade aberto
        if (upgradeUI.activeSelf && upgradeCostTxt != null)
        {
            UpdateUpgradeCostText();
        }

        // Atualiza o range visual
        UpdateRangeIndicator();
    }

    private void FreezeEnemies()
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, targetingRange, (Vector2)transform.position, 0f, enemyMask);
        if (hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];
                EnemyMovement em = hit.transform.GetComponent<EnemyMovement>();
                if (em != null)
                {
                    em.UpdateSpeed(0.5f);
                    StartCoroutine(ResetEnemySpeed(em));
                }
            }
        }
    }

    private IEnumerator ResetEnemySpeed(EnemyMovement em)
    {
        yield return new WaitForSeconds(freezeTime);
        em.ResetSpeed();
    }

    // Atualiza o tamanho do círculo de range
    private void UpdateRangeIndicator()
    {
        if (rangeIndicator == null) return;
        float diameter = targetingRange * 2f + rangePadding;
        rangeIndicator.transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    // ───── CLIQUE E SAÍDA DO MOUSE ─────
    private void OnMouseDown()
    {
        // Ignora clique se mouse está sobre UI
        if (UIManager.main != null && UIManager.main.IsHoveringUI())
            return;

        // Toggle: clica → abre/fecha upgrade + range
        if (isSelected)
        {
            CloseUpgradeUI();
        }
        else
        {
            OpenUpgradeUI();
        }
    }

    private void OnMouseExit()
    {
        // Quando o mouse sai da torre → fecha upgrade e esconde range
        if (isSelected)
        {
            CloseUpgradeUI();
        }
    }

    public void OpenUpgradeUI()
    {
        if (upgradeUI == null) return;

        upgradeUI.SetActive(true);
        isSelected = true;

        if (rangeIndicator != null)
            rangeIndicator.SetActive(true);

        UpdateUpgradeCostText();
    }

    public void CloseUpgradeUI()
    {
        if (upgradeUI == null) return;

        upgradeUI.SetActive(false);
        isSelected = false;

        if (rangeIndicator != null)
            rangeIndicator.SetActive(false);

        if (UIManager.main != null)
            UIManager.main.SetHoveringState(false);
    }

    public void Upgrade()
    {
        int nextCost = CalculateCost();
        if (nextCost > LevelManager.main.currency || level >= maxLevel)
            return;

        LevelManager.main.SpendCurrency(nextCost);
        level++;
        aps = CalculateAPS();
        targetingRange = CalculateRange();

        UpdateRangeIndicator();
        UpdateUpgradeCostText();
        CloseUpgradeUI();
    }

    private void UpdateUpgradeCostText()
    {
        if (upgradeCostTxt == null) return;

        if (level >= maxLevel)
        {
            upgradeCostTxt.text = "MAX";
            upgradeCostTxt.color = Color.gray;
            if (upgradeButton != null)
                upgradeButton.interactable = false;
        }
        else
        {
            int nextCost = CalculateCost();
            upgradeCostTxt.text = nextCost.ToString();
            upgradeCostTxt.color = (nextCost > LevelManager.main.currency) ? Color.red : defaultTextColor;
            if (upgradeButton != null)
                upgradeButton.interactable = true;
        }
    }

    private int CalculateCost() => Mathf.RoundToInt(baseUpgradeCost * Mathf.Pow(level + 1, 0.8f));

    private float CalculateAPS() => apsBase * Mathf.Pow(level, 0.5f);

    private float CalculateRange() => targetingRangeBase * Mathf.Pow(level, 0.4f);

    private void OnDrawGizmosSelected()
    {
        Handles.color = Color.cyan;
        Handles.DrawWireDisc(transform.position, Vector3.forward, targetingRange);
    }

    public int GetCurrentLevel() => level;

    public int GetMaxLevel() => maxLevel;

    public int CalculateNextCost() => CalculateCost(); // ou o método que calcula o custo do próximo nível

    public string GetUpgradeDescription()
    {
        return "Aumenta dano de explosão, raio e velocidade de ataque";
    }
}