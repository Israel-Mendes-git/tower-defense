using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Turret : MonoBehaviour, IUpgradable
{
    [Header("References")]
    [SerializeField] private Transform turretRotationPoint;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firingPoint;

    [Header("Attribute")]
    [SerializeField] private float targetingRange = 5f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float bps = 1f;
    [SerializeField] private int baseUpgradeCost = 100;

    [Header("Upgrade Settings")]
    [SerializeField] public int maxLevel = 3;

    private float bpsBase;
    private float targetingRangeBase;
    private Transform target;
    private float timeUntilFire;
    public int level = 1;
    private bool isSelected = false;

    private void Start()
    {
        bpsBase = bps;
        targetingRangeBase = targetingRange;
    }

    private void Update()
    {
        if (target == null)
        {
            FindTarget();
            return;
        }

        RotateTowardsTarget();

        if (!CheckTargetIsInRange())
        {
            target = null;
        }
        else
        {
            timeUntilFire += Time.deltaTime;
            if (timeUntilFire >= 1f / bps)
            {
                Shoot();
                timeUntilFire = 0f;
            }
        }
    }

    private void Shoot()
    {
        GameObject bulletObj = Instantiate(bulletPrefab, firingPoint.position, Quaternion.identity);
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.SetTarget(target);
        }
    }

    private void FindTarget()
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, targetingRange, (Vector2)transform.position, 0f, enemyMask);
        if (hits.Length > 0)
        {
            target = hits[0].transform;
        }
    }

    private bool CheckTargetIsInRange()
    {
        return target != null && Vector2.Distance(target.position, transform.position) <= targetingRange;
    }

    private void RotateTowardsTarget()
    {
        float angle = Mathf.Atan2(target.position.y - transform.position.y, target.position.x - transform.position.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
        turretRotationPoint.rotation = Quaternion.RotateTowards(turretRotationPoint.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    // ───── CLIQUE ─────
    private void OnMouseDown()
    {
        if (UIManager.main != null && UIManager.main.IsHoveringUI())
            return;

        if (isSelected)
        {
            CloseUpgradeUI();
        }
        else
        {
            OpenUpgradeUI();
        }
    }

    // NÃO tem mais OnMouseExit() com fechamento automático

    public void OpenUpgradeUI()
    {
        isSelected = true;

        // Avisa o UIManager para mostrar a UI de upgrade
        if (UIManager.main != null)
        {
            UIManager.main.ShowUpgradeUI(this);
        }
    }

    public void CloseUpgradeUI()
    {
        isSelected = false;

        // Avisa o UIManager para esconder a UI de upgrade
        if (UIManager.main != null)
        {
            UIManager.main.HideUpgradeUI();
        }
    }

    public void Upgrade()
    {
        int nextCost = CalculateCost();
        if (nextCost > LevelManager.main.currency || level >= maxLevel)
            return;

        LevelManager.main.SpendCurrency(nextCost);
        level++;
        bps = CalculateBPS();
        targetingRange = CalculateRange();

        // Avisa o UIManager para atualizar a UI após upgrade
        if (UIManager.main != null)
        {
            UIManager.main.UpdateUpgradeUI();
        }
    }

    public int CalculateCost() => Mathf.RoundToInt(baseUpgradeCost * Mathf.Pow(level, 0.8f));

    private float CalculateBPS() => bpsBase * Mathf.Pow(level, 0.25f);

    private float CalculateRange() => targetingRangeBase * Mathf.Pow(level, 0.4f);

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