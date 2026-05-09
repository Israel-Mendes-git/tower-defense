using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Color hoverColor;

    public GameObject towerObj; // Torre atualmente construída neste plot

    private Color startColor;

    private void Start()
    {
        startColor = sr.color;
    }

    private void OnMouseEnter()
    {
        // Só muda cor se não tiver torre (ou se quiser sempre hover)
        if (towerObj == null)
        {
            sr.color = hoverColor;
        }
    }

    private void OnMouseExit()
    {
        sr.color = startColor;
    }

    private void OnMouseDown()
    {
        // Ignora clique se mouse está sobre UI
        if (UIManager.main != null && UIManager.main.IsHoveringUI())
            return;

        // Caso 1: Já tem torre → abre upgrade
        if (towerObj != null)
        {
            var upgradable = towerObj.GetComponentInChildren<IUpgradable>();
            if (upgradable != null)
            {
                upgradable.OpenUpgradeUI();
            }
            return;
        }

        // Caso 2: Não tem torre → tenta construir
        if (BuildManager.main == null)
            return;

        Tower towerToBuild = BuildManager.main.GetSelectedTower();
        if (towerToBuild == null)
            return;

        if (towerToBuild.cost > LevelManager.main.currency)
        {
            // Opcional: feedback de "moedas insuficientes"
            Debug.Log("Moedas insuficientes para construir");
            return;
        }

        // Gasta moedas e instancia torre
        LevelManager.main.SpendCurrency(towerToBuild.cost);
        towerObj = Instantiate(towerToBuild.prefab, transform.position, Quaternion.identity);

        // Registra a torre no BuildManager (para poder destruí-la depois)
        if (BuildManager.main != null)
        {
            BuildManager.main.RegisterPlacedTower(towerObj);
        }

        // Limpa seleção automaticamente após construir
        BuildManager.main.ConsumeBuild();

        // Volta cor normal após construir
        sr.color = startColor;
    }
}