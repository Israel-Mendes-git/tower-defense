using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class SniperTurret : MonoBehaviour, IUpgradable
{
    [Header("References")]
    [SerializeField] private Transform turretRotationPoint;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firingPoint;
    [SerializeField] private GameObject upgradeUI;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradeCostTxt;

    [Header("Attributes - SNIPER")]
    [SerializeField] private float targetingRange = 18f;      // RANGE ABSURDO!
    [SerializeField] private float rotationSpeed = 120f;      // Rotaciona rápido para mirar
    [SerializeField] private float aps = 0.25f;               // Muito lento: atira a cada 4 segundos
    [SerializeField] private float baseDamage = 150f;         // DANO MASSIVO
    [SerializeField] private int baseUpgradeCost = 250;

    [Header("Range Visual")]
    [SerializeField] private GameObject rangeIndicator;       // ← Arraste o GameObject do círculo aqui
    [SerializeField] private float rangePadding = 0.1f;       // Espaço extra (opcional)

    [Header("Upgrade Settings")]
    [SerializeField] private int maxLevel = 3;

    private float apsBase;
    private float rangeBase;
    private float damageBase;
    private Transform target;
    private float timeUntilFire;
    private int level = 1;
    private bool isSelected = false; // Controla se está "selecionado" (clicado)
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
        if (rangeIndicator != null)
            rangeIndicator.SetActive(false);
        if (upgradeUI != null)
            upgradeUI.SetActive(false);

        UpdateRangeIndicator();
        UpdateUpgradeCostText();
    }

    private void Update()
    {
        // Lógica de alvo e tiro
        if (target == null || !IsTargetInRange())
        {
            FindTarget();
        }

        if (target != null)
        {
            RotateTowardsTarget();

            timeUntilFire += Time.deltaTime;
            if (timeUntilFire >= 1f / aps)
            {
                Shoot();
                timeUntilFire = 0f;
            }
        }

        // Atualiza custo se upgrade aberto
        if (upgradeUI.activeSelf && upgradeCostTxt != null)
        {
            UpdateUpgradeCostText();
        }

        // Atualiza o range visual
        UpdateRangeIndicator();
    }

    private void FindTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, targetingRange, enemyMask);
        if (hits.Length == 0)
        {
            target = null;
            return;
        }

        // Prioriza o inimigo mais avançado no caminho
        Transform bestTarget = null;
        float farthestDistance = -1f;

        foreach (var hit in hits)
        {
            EnemyMovement em = hit.GetComponent<EnemyMovement>();
            if (em != null)
            {
                float dist = em.GetDistanceTraveled();
                if (dist > farthestDistance)
                {
                    farthestDistance = dist;
                    bestTarget = hit.transform;
                }
            }
        }

        target = bestTarget ?? hits[0].transform;
    }

    private bool IsTargetInRange()
    {
        return target != null && Vector2.Distance(transform.position, target.position) <= targetingRange;
    }

    private void RotateTowardsTarget()
    {
        Vector2 direction = (target.position - turretRotationPoint.position);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
        turretRotationPoint.rotation = Quaternion.RotateTowards(turretRotationPoint.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void Shoot()
    {
        GameObject bulletObj = Instantiate(bulletPrefab, firingPoint.position, Quaternion.identity);
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.SetTarget(target);
            bullet.SetDamage(CalculateDamage()); // Dano altíssimo
        }
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
            int cost = CalculateCost();
            upgradeCostTxt.text = cost.ToString();
            upgradeCostTxt.color = (cost > LevelManager.main.currency) ? Color.red : defaultTextColor;
            if (upgradeButton != null)
                upgradeButton.interactable = true;
        }
    }

    private int CalculateCost() => Mathf.RoundToInt(baseUpgradeCost * Mathf.Pow(level + 1, 0.9f));

    private float CalculateAPS() => apsBase * Mathf.Pow(level, 0.15f);

    private float CalculateDamage() => damageBase * Mathf.Pow(level, 1.2f);

    private float CalculateRange() => rangeBase * Mathf.Pow(level, 0.3f);

    private void OnDrawGizmosSelected()
    {
        Handles.color = new Color(0.2f, 0.8f, 1f, 0.4f);
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