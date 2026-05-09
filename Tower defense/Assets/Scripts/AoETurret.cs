using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class AoETurret : MonoBehaviour, IUpgradable
{
    [Header("References")]
    [SerializeField] private Transform firingPoint; // onde o projétil nasce
    [SerializeField] private GameObject bulletPrefab; // AoEBullet prefab
    [SerializeField] private LayerMask enemyMask;

    [Header("Attributes")]
    [SerializeField] private float targetingRange = 6f;
    [SerializeField] private float aps = 0.8f; // ataques por segundo
    [SerializeField] private float baseExplosionDamage = 40f;
    [SerializeField] private float baseExplosionRadius = 2.5f;
    [SerializeField] private int baseUpgradeCost = 180;

    [Header("Upgrade Settings")]
    [SerializeField] public int maxLevel = 3;

    private float apsBase;
    private float targetingRangeBase;
    private float damageBase;
    private float radiusBase;
    private int level = 1;
    private float timeUntilFire;
    private Transform target;
    private bool isSelected = false;

    private void Start()
    {
        apsBase = aps;
        targetingRangeBase = targetingRange;
        damageBase = baseExplosionDamage;
        radiusBase = baseExplosionRadius;
    }

    private void Update()
    {
        if (target == null || !IsTargetInRange())
        {
            FindTarget();
        }

        if (target != null)
        {
            timeUntilFire += Time.deltaTime;
            if (timeUntilFire >= 1f / aps)
            {
                Shoot();
                timeUntilFire = 0f;
            }
        }
    }

    private void FindTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, targetingRange, enemyMask);
        if (hits.Length > 0)
        {
            target = hits[0].transform;
            float closestDist = Vector2.Distance(transform.position, target.position);
            foreach (var hit in hits)
            {
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    target = hit.transform;
                }
            }
        }
    }

    private bool IsTargetInRange()
    {
        return target != null && Vector2.Distance(transform.position, target.position) <= targetingRange;
    }

    private void Shoot()
    {
        if (bulletPrefab == null || firingPoint == null) return;
        GameObject bulletObj = Instantiate(bulletPrefab, firingPoint.position, Quaternion.identity);
        AoEBullet bullet = bulletObj.GetComponent<AoEBullet>();
        if (bullet != null)
        {
            bullet.SetTarget(target);
            bullet.SetDamage(CalculateDamage());
            bullet.SetRadius(CalculateRadius());
        }
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

    // NÃO fecha automaticamente ao sair com o mouse
    private void OnMouseExit()
    {
        // vazio intencionalmente
    }

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
        aps = CalculateAPS();
        targetingRange = CalculateRange();

        // Avisa o UIManager para atualizar a UI após upgrade
        if (UIManager.main != null)
        {
            UIManager.main.UpdateUpgradeUI();
        }
    }

    private int CalculateCost() => Mathf.RoundToInt(baseUpgradeCost * Mathf.Pow(level + 1, 0.8f));

    private float CalculateAPS() => apsBase * Mathf.Pow(level, 0.55f);

    private float CalculateDamage() => damageBase * Mathf.Pow(level, 0.7f);

    private float CalculateRadius() => radiusBase * Mathf.Pow(level, 0.5f);

    private float CalculateRange() => targetingRangeBase * Mathf.Pow(level, 0.45f);

    private void OnDrawGizmosSelected()
    {
        Handles.color = new Color(1f, 0.3f, 0.3f, 0.6f);
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