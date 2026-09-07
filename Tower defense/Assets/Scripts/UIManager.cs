using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager main;

    private bool isHoveringUI;

    [Header("Upgrade UI Elements")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private TMP_Text levelText;

    // Encontrados por nome dentro do painel
    private Image towerIconImage;
    private TMP_Text statsText;
    private Button pathAButton, pathBButton;
    private TMP_Text pathALabel, pathBLabel;
    private Button priorityButton;
    private TMP_Text priorityLabel;
    private Button sellButton;
    private TMP_Text sellLabel;
    private TMP_Text closeLabel;

    private IUpgradable selectedTower;
    private TowerBase towerBase;

    // Trilha sob o mouse (-1 = nenhuma). Enquanto houver uma, o painel mostra o que aquele
    // upgrade FAZ em vez dos stats atuais.
    private int previewPath = -1;

    private void Awake()
    {
        main = this;

        if (upgradePanel != null)
        {
            pathAButton = FindButton("PathAButton", out pathALabel, OnPathAClicked);
            pathBButton = FindButton("PathBButton", out pathBLabel, OnPathBClicked);
            priorityButton = FindButton("PriorityButton", out priorityLabel, OnPriorityClicked);
            sellButton = FindButton("SellButton", out sellLabel, OnSellClicked);
            FindButton("CloseBtn", out closeLabel, HideUpgradeUI); // botão Fechar

            Transform st = upgradePanel.transform.Find("StatsText");
            if (st != null) statsText = st.GetComponent<TMP_Text>();

            Transform icon = upgradePanel.transform.Find("UpgradeImage");
            if (icon != null) towerIconImage = icon.GetComponent<Image>();

            // Passar o mouse sobre a trilha revela a descrição do próximo tier.
            HookHover(pathAButton, 0);
            HookHover(pathBButton, 1);

            upgradePanel.SetActive(false);
        }

        LevelManager.onCurrencyChanged.AddListener(OnCurrencyChanged);
    }

    private Button FindButton(string name, out TMP_Text label, UnityEngine.Events.UnityAction onClick)
    {
        label = null;
        Transform t = upgradePanel.transform.Find(name);
        if (t == null) return null;
        Button b = t.GetComponent<Button>();
        label = t.GetComponentInChildren<TMP_Text>(true);
        if (b != null) b.onClick.AddListener(onClick);
        return b;
    }

    // Liga entrar/sair do mouse num botão de trilha à pré-visualização do tier.
    // Montado em runtime de propósito: não exige fiação manual na cena.
    private void HookHover(Button b, int path)
    {
        if (b == null) return;

        EventTrigger trig = b.gameObject.GetComponent<EventTrigger>();
        if (trig == null) trig = b.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(delegate { previewPath = path; RefreshStats(); });
        trig.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(delegate { previewPath = -1; RefreshStats(); });
        trig.triggers.Add(exit);
    }

    public void SetHoveringState(bool state) => isHoveringUI = state;

    // O ponteiro está sobre a UI? Isto antes dependia de alguém chamar SetHoveringState — e
    // ninguém chamava, nem script nem cena. O guard vivia devolvendo false, e todo clique em
    // botão vazava para o mundo atrás dele (construía torre, fechava painel, mandava o aliado
    // patrulhar). Agora a resposta vem do próprio EventSystem, sem depender de fiação na cena.
    public bool IsHoveringUI()
    {
        if (isHoveringUI) return true; // override manual, caso a cena queira forçar

        EventSystem es = EventSystem.current;
        if (es == null) return false;
        if (es.IsPointerOverGameObject()) return true;

        for (int i = 0; i < Input.touchCount; i++)
            if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;

        return false;
    }

    public void ShowUpgradeUI(IUpgradable tower)
    {
        if (tower == null) return;

        // DESELECIONA A ANTERIOR. Sem isto o anel de alcance dela fica preso na tela para sempre:
        // clicar numa segunda torre abre o painel da nova (Plot.OnMouseDown -> OpenUpgradeUI) e o
        // BuildManager nem chega a rodar seu "clique no vazio" — ele dá return assim que o raycast
        // acerta algo com IHasRange. A referência à torre antiga era simplesmente sobrescrita
        // aqui, e ninguém mais tinha como mandá-la esconder o anel.
        TowerBase anterior = towerBase;
        if (anterior != null && !ReferenceEquals(anterior, tower)) anterior.MarkDeselected();

        selectedTower = tower;
        towerBase = tower as TowerBase;
        previewPath = -1;
        if (upgradePanel != null) upgradePanel.SetActive(true);
        UpdateUpgradeUI();
    }

    public void HideUpgradeUI()
    {
        if (towerBase != null) towerBase.MarkDeselected(); // esconde o alcance + reseta a torre
        if (upgradePanel != null) upgradePanel.SetActive(false);
        selectedTower = null;
        towerBase = null;
        previewPath = -1;
    }

    // True enquanto o painel de upgrade estiver aberto (quem clica no vazio usa isto para fechar).
    public bool IsUpgradePanelOpen => selectedTower != null;

    private void OnCurrencyChanged()
    {
        if (selectedTower != null) UpdateUpgradeUI();
    }

    public void UpdateUpgradeUI()
    {
        if (selectedTower == null) return;

        int currentLevel = selectedTower.GetCurrentLevel();
        int maxLvl = selectedTower.GetMaxLevel();

        if (levelText != null)
            levelText.text = $"Nível {currentLevel} / {maxLvl}";

        RefreshStats();

        // Ícone (brilha mais em níveis altos)
        if (towerIconImage != null)
        {
            Sprite icon = selectedTower.GetIcon();
            if (icon != null)
            {
                towerIconImage.sprite = icon;
                towerIconImage.preserveAspect = true;
                float b = maxLvl > 0 ? Mathf.Lerp(0.7f, 1f, (float)currentLevel / maxLvl) : 1f;
                towerIconImage.color = new Color(b, b, b, 1f);
                towerIconImage.gameObject.SetActive(true);
            }
            else towerIconImage.gameObject.SetActive(false);
        }

        // Trilhas de upgrade
        UpdatePathButton(0, pathAButton, pathALabel);
        UpdatePathButton(1, pathBButton, pathBLabel);

        RefreshTargetingAndSell();
    }

    // O corpo do painel: ou a prévia do upgrade sob o mouse, ou o que a torre é hoje.
    private void RefreshStats()
    {
        if (statsText == null || towerBase == null) return;

        // Prévia: o texto que explica o VERBO do upgrade ("ATRAVESSA até 3 inimigos",
        // "EMPURRA de volta pela rota") existia no código desde sempre e nunca era exibido.
        if (previewPath >= 0)
        {
            UpgradeTier next = towerBase.NextTier(previewPath);
            if (next != null)
            {
                string aviso = towerBase.IsPathLocked(previewPath)
                    ? "\n<color=#FF8080>Trilha travada: a outra já passou do tier 2.</color>"
                    : "";
                statsText.text = $"<b>{next.title}</b>  <color=#FFD34F>${next.cost}</color>\n{next.description}{aviso}";
                return;
            }
        }

        string txt = towerBase.GetStatsText();

        // O que esta torre já sabe fazer. Só os tiers que mudaram COMPORTAMENTO (têm ability):
        // listar os de número puro viraria uma parede de "+50% de dano".
        foreach (UpgradeTier t in towerBase.PurchasedTiers())
        {
            if (string.IsNullOrEmpty(t.ability)) continue;
            txt += $"\n<color=#9CD1FF>▸ {t.title}:</color> <size=85%>{t.description}</size>";
        }

        List<string> synergies =
            SynergyManager.main != null ? SynergyManager.main.ActiveFor(towerBase) : null;
        if (synergies != null && synergies.Count > 0)
            txt += $"\n<color=#7BE38C>⚡ {string.Join(" · ", synergies.ToArray())}</color>";

        statsText.text = txt;
    }

    private void UpdatePathButton(int path, Button button, TMP_Text label)
    {
        if (button == null || towerBase == null) return;

        UpgradeTier tier = towerBase.NextTier(path);
        if (tier == null)
        {
            if (label != null) { label.text = "MÁXIMO"; label.color = Color.gray; }
            button.interactable = false;
            return;
        }

        // Travada pela regra de crosspath: só uma trilha pode fechar, a outra para no tier 2.
        if (towerBase.IsPathLocked(path))
        {
            if (label != null)
            {
                label.text = $"{tier.title}\n<size=65%>Trilha travada</size>";
                label.color = new Color(0.5f, 0.5f, 0.5f);
            }
            button.interactable = false;
            return;
        }

        bool afford = LevelManager.main != null && tier.cost <= LevelManager.main.currency;
        if (label != null)
        {
            label.text = $"{tier.title}\n<size=70%>${tier.cost}</size>";
            label.color = afford ? Color.white : new Color(1f, 0.5f, 0.5f);
        }

        // Continua clicável mesmo sem verba: o clique explica o motivo em vez de não fazer nada.
        button.interactable = true;
    }

    private void RefreshTargetingAndSell()
    {
        if (priorityButton != null)
        {
            bool canTarget = selectedTower is ITargeting;
            priorityButton.gameObject.SetActive(canTarget);
            if (canTarget && priorityLabel != null)
                priorityLabel.text = "Alvo: " + ((ITargeting)selectedTower).GetTargetingLabel();
        }

        if (sellLabel != null)
        {
            MonoBehaviour mb = selectedTower as MonoBehaviour;
            TowerValue tv = mb != null ? mb.GetComponentInParent<TowerValue>() : null;
            sellLabel.text = tv != null ? ("Vender +" + tv.SellValue) : "Vender";
        }
    }

    private void OnPathAClicked() => TryUpgrade(0);
    private void OnPathBClicked() => TryUpgrade(1);

    // Um clique que não pode ser pago precisa DIZER isso — antes o botão só ficava inerte.
    private void TryUpgrade(int path)
    {
        if (towerBase == null) return;

        UpgradeTier tier = towerBase.NextTier(path);
        if (tier != null && LevelManager.main != null && tier.cost > LevelManager.main.currency)
        {
            FloatingText.Spawn(towerBase.transform.position,
                $"Faltam ${tier.cost - LevelManager.main.currency}", new Color(1f, 0.45f, 0.45f));
            return;
        }

        towerBase.UpgradePath(path);
        UpdateUpgradeUI();
    }

    private void OnPriorityClicked()
    {
        if (selectedTower is ITargeting t)
        {
            t.CycleTargeting();
            if (priorityLabel != null) priorityLabel.text = "Alvo: " + t.GetTargetingLabel();
        }
    }

    private void OnSellClicked()
    {
        MonoBehaviour mb = selectedTower as MonoBehaviour;
        if (mb == null) return;

        TowerValue tv = mb.GetComponentInParent<TowerValue>();
        GameObject towerRoot = tv != null ? tv.gameObject : mb.gameObject;

        HideUpgradeUI();

        if (tv != null) tv.Sell();
        else Destroy(towerRoot);
    }
}
