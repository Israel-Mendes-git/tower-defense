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

    // Chamado pela venda: libera o plot pra construir de novo.
    public void ClearTower()
    {
        towerObj = null;
        if (sr != null) sr.color = startColor;
    }

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
            // Feedback de verdade: antes isto era um Debug.Log que o jogador nunca via, então
            // clicar sem dinheiro parecia o jogo simplesmente ignorando o comando.
            int falta = towerToBuild.cost - LevelManager.main.currency;
            FloatingText.Spawn(transform.position, "Faltam $" + falta, new Color(1f, 0.45f, 0.45f));
            return;
        }

        // Gasta moedas e instancia torre
        LevelManager.main.SpendCurrency(towerToBuild.cost);
        AudioManager.Cue(AudioManager.Sfx.Buy);
        towerObj = Instantiate(towerToBuild.prefab, transform.position, Quaternion.identity);

        // A torre fica SOBRE a célula: a ordem de desenho vem da posição dela no tabuleiro,
        // e não do número que veio no prefab. Não se move, então basta resolver uma vez.
        IsoSorter.Attach(towerObj, moves: false);

        // Rastreia o investimento p/ permitir venda com reembolso
        TowerValue value = towerObj.GetComponent<TowerValue>();
        if (value == null) value = towerObj.AddComponent<TowerValue>();
        value.Init(towerToBuild.cost, this);

        // Integridade estrutural: é o que permite a torre ser DESTRUÍDA pelo Sabotador em vez de
        // só desligada (ver TowerIntegrity). Anexado aqui, no mesmo lugar do TowerValue, para
        // valer em toda torre construída sem depender de alguém lembrar de pôr no prefab — o
        // erro clássico deste projeto é justamente a peça que existe e nunca é ligada.
        if (towerObj.GetComponent<TowerIntegrity>() == null)
            towerObj.AddComponent<TowerIntegrity>();

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