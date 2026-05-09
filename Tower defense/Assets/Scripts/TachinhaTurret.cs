using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class TachinhaTurret : MonoBehaviour, IUpgradable
{
    [Header("References")]
    [SerializeField] private Transform firingPoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private GameObject upgradeUI;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradeCostTxt;

    [Header("Attributes")]
    [SerializeField] private float targetingRange = 5f;
    [SerializeField] private float aps = 1f;
    [SerializeField] private float baseDamage = 8f;
    [SerializeField] private int baseUpgradeCost = 120;

    [Header("Spawn Offset")]
    [SerializeField] private float spawnOffset = 0.4f;

    [Header("Range Visual - UI Image")]
    [SerializeField] private Image rangeImage; // ← Arraste a UI Image do círculo aqui (dentro do Canvas filho)
    [SerializeField] private float rangePadding = 0.1f; // Espaço extra

    [Header("Upgrade Settings")]
    [SerializeField] private int maxLevel = 3;

    private float apsBase;
    private float rangeBase;
    private float damageBase;
    private int level = 1;
    private float timeUntilFire;
    private bool isSelected = false;

    private static readonly Vector2[] directions = {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
        new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1)
    };
    private Color defaultTextColor;

    private void Start()
    {
        apsBase = aps;
        rangeBase = targetingRange;
        damageBase = baseDamage;

        if (upgradeCostTxt != null)
            defaultTextColor = upgradeCostTxt.color;

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(Upgrade);

        // Começa tudo escondido
        if (rangeImage != null && rangeImage.gameObject != null)
            rangeImage.gameObject.SetActive(false);
        if (upgradeUI != null)
            upgradeUI.SetActive(false);

        UpdateRangeVisual();
        UpdateUpgradeCostText();
    }

    private void Update()
    {
        // Lógica de tiro
        if (!HasEnemyInRange())
        {
            timeUntilFire = 0f;
            return;
        }

        timeUntilFire += Time.deltaTime;
        if (timeUntilFire >= 1f / aps)
        {
            Shoot();
            timeUntilFire = 0f;
        }

        // Atualiza custo se upgrade aberto
        if (upgradeUI.activeSelf && upgradeCostTxt != null)
        {
            UpdateUpgradeCostText();
        }

        // Atualiza visual do range
        UpdateRangeVisual();
    }

    private void UpdateRangeVisual()
    {
        if (rangeImage == null || rangeImage.gameObject == null) return;

        // Mostra/esconde com base no estado do upgrade
        bool shouldShow = upgradeUI.activeSelf;

        if (rangeImage.gameObject.activeSelf != shouldShow)
        {
            rangeImage.gameObject.SetActive(shouldShow);
        }

        if (!shouldShow) return;

        // Escala do range em unidades do mundo → convertido para tamanho da UI Image
        float worldDiameter = targetingRange * 2f + rangePadding;

        // Fator de escala: ajuste conforme o Canvas World Space (geralmente pequeno)
        float uiScaleFactor = 1f / transform.localScale.x; // Ajusta pela escala da torre
        float uiDiameter = worldDiameter * uiScaleFactor * 100f; // 100 é padrão para Canvas World Space

        rangeImage.rectTransform.sizeDelta = new Vector2(uiDiameter, uiDiameter);
    }

    private bool HasEnemyInRange()
    {
        return Physics2D.OverlapCircle(transform.position, targetingRange, enemyMask);
    }

    private void Shoot()
    {
        foreach (Vector2 dir in directions)
        {
            Vector2 normalizedDir = dir.normalized;
            Vector3 spawnPos = firingPoint.position + (Vector3)(normalizedDir * spawnOffset);
            float angle = Mathf.Atan2(normalizedDir.y, normalizedDir.x) * Mathf.Rad2Deg - 90f;
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);
            GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, rot);
            TachinhaBullet bullet = bulletObj.GetComponent<TachinhaBullet>();
            if (bullet != null)
            {
                bullet.Init(normalizedDir, CalculateDamage());
            }
        }
    }

    // ───── CLIQUE NA TORRE ─────
    private void OnMouseDown()
    {
        // Ignora clique se mouse está sobre UI
        if (UIManager.main != null && UIManager.main.IsHoveringUI())
            return;

        // Toggle
        if (isSelected)
        {
            CloseUpgradeUI();
        }
        else
        {
            OpenUpgradeUI();
        }
    }

    public void OpenUpgradeUI()
    {
        if (upgradeUI == null) return;

        upgradeUI.SetActive(true);
        isSelected = true;

        if (rangeImage != null && rangeImage.gameObject != null)
            rangeImage.gameObject.SetActive(true);

        UpdateUpgradeCostText();
        UpdateRangeVisual();
    }

    public void CloseUpgradeUI()
    {
        if (upgradeUI == null) return;

        upgradeUI.SetActive(false);
        isSelected = false;

        if (rangeImage != null && rangeImage.gameObject != null)
            rangeImage.gameObject.SetActive(false);

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
        UpdateRangeVisual();
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

    private int CalculateCost() => Mathf.RoundToInt(baseUpgradeCost * Mathf.Pow(level + 1, 0.6f));
    private float CalculateAPS() => apsBase * Mathf.Pow(level, 0.2f);
    private float CalculateDamage() => damageBase * Mathf.Pow(level, 0.25f);
    private float CalculateRange() => rangeBase * Mathf.Pow(level, 0.1f);

    private void OnDrawGizmosSelected()
    {
        Handles.color = new Color(0.3f, 0.8f, 1f, 0.6f);
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