using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Tela de seleção de fase. Constrói os cartões em runtime a partir do StageCatalog, então adicionar
// uma fase nova é editar uma lista em código — a UI se ajusta sozinha, sem trabalho no editor.
// Mostra o que está trancado e por quê, que é metade do motivo de o jogador querer voltar.
public class StageSelectUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private Transform cardParent;   // se vazio, usa este próprio objeto
    [SerializeField] private GameObject cardTemplate; // se vazio, os cartões são gerados
    [SerializeField] private TMP_Text headerText;

    private void Start() => Build();

    public void Build()
    {
        Transform parent = cardParent != null ? cardParent : transform;

        // Limpa cartões anteriores (rebuild ao voltar para a tela).
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (child != cardTemplate && child.name.StartsWith("StageCard")) Destroy(child);
        }

        if (headerText != null)
        {
            headerText.text = $"Comandante nível {PlayerProgress.Level}"
                            + $"  ·  XP {PlayerProgress.XPIntoCurrentLevel}/{Mathf.Max(1, PlayerProgress.XPNeededForNext)}"
                            + $"  ·  Melhor rodada: {PlayerProgress.BestRound}";
        }

        foreach (StageDefinition stage in StageCatalog.All)
            CreateCard(parent, stage);
    }

    private void CreateCard(Transform parent, StageDefinition stage)
    {
        bool unlocked = StageCatalog.IsUnlocked(stage);
        int best = PlayerProgress.StageBestRound(stage.id);
        bool cleared = PlayerProgress.StageCleared(stage.id);

        GameObject card;
        if (cardTemplate != null)
        {
            card = Instantiate(cardTemplate, parent);
            card.SetActive(true);
        }
        else
        {
            card = BuildDefaultCard(parent);
        }
        card.name = "StageCard_" + stage.id;

        string status = !unlocked
            ? $"<color=#FF8888>Trancada — nível {stage.requiredLevel}</color>"
            : (cleared ? "<color=#88FF99>CONCLUÍDA</color>" : (best > 0 ? $"Melhor: rodada {best}" : "Nunca jogada"));

        TMP_Text label = card.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = $"<b>{stage.displayName}</b>\n<size=70%>{stage.description}</size>"
                       + $"\n<size=70%>{stage.maxRounds} rodadas · vida {stage.startingHP} · verba ${stage.startingCurrency}"
                       + (stage.enemyHealthMultiplier > 1f ? $" · inimigos +{(stage.enemyHealthMultiplier - 1f) * 100f:0}% vida" : "")
                       + $"\n{status}</size>";
        }

        Button button = card.GetComponent<Button>();
        if (button == null) button = card.GetComponentInChildren<Button>();
        if (button != null)
        {
            button.interactable = unlocked;
            string id = stage.id; // capturado por valor
            button.onClick.AddListener(() => Play(id));
        }

        Image bg = card.GetComponent<Image>();
        if (bg != null)
            bg.color = unlocked ? new Color(0.18f, 0.24f, 0.32f, 0.95f) : new Color(0.14f, 0.14f, 0.16f, 0.9f);
    }

    // Cartão gerado quando não há template montado no editor.
    private GameObject BuildDefaultCard(Transform parent)
    {
        var go = new GameObject("StageCard", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(420f, 130f);

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(14f, 10f);
        trt.offsetMax = new Vector2(-14f, -10f);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;
        tmp.richText = true;

        return go;
    }

    public void Play(string stageId)
    {
        StageCatalog.Select(stageId);
        PlayerPrefs.Save();
        Time.timeScale = 1f;

        if (GameManager.Instance != null) GameManager.Instance.LoadScene(gameSceneName);
        else UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
    }
}
