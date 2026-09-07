using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class LevelManager : MonoBehaviour
{
    public static LevelManager main;
    public Transform startPoint;
    public int playerHP;
    public Transform[] path;
    [SerializeField] private TMP_Text HPText;
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private GameObject victoryPanel; // se vazio, é procurado por nome ("Victory Panel")

    public bool isDead;
    public int currency;

    [SerializeField] private int startingCurrency = 175;
    // Ao repetir uma rodada avançada o jogador perde todas as torres; sem uma verba proporcional
    // ao progresso, retomar na rodada 20 com a verba inicial seria impossível de vencer.
    // Verba por rodada alcançada ao REPETIR a rodada (ver RestartWave). Era 120, e nessa altura
    // repetir a rodada pagava mais do que o jogador tinha no bolso ao morrer — medido numa partida
    // completa: na rodada 10 ele morria com $1.082 e recomeçava com $1.310. Morrer virava lucro, e
    // é isso que se lê na tela como "o dinheiro continuou".
    //
    // O critério é que a verba seja SOCORRO, não prêmio: nunca acima do caixa que ele perdeu. Pela
    // curva medida (bolso de $1.082 na rodada 10, $2.680 na 17), o teto fica perto de 95; 60 deixa
    // a verba em ~70% do bolso na rodada 10 e continua bancando um recomeço parcial nas altas,
    // onde ela já cobria só uma fração do investido.
    [SerializeField] private int retryCurrencyPerWave = 60;

    private int startingHP; // capturado da cena no Awake — o valor de origem da vida

    // Fase em curso (definida pelo StageLoader) — usada para registrar progresso por mapa.
    public string StageId { get; set; } = "vale";

    // Ajustado pela fase escolhida e pelos bônus permanentes do jogador.
    public void SetStartingCurrency(int amount)
    {
        startingCurrency = amount;
        currency = amount;
        onCurrencyChanged.Invoke();
    }

    public static UnityEvent onCurrencyChanged = new UnityEvent();

    public void IncreaseCurrency(int amount)
    {
        currency += amount;
        onCurrencyChanged.Invoke(); // ← Dispara evento
    }

    public bool SpendCurrency(int amount)
    {
        if (amount <= currency)
        {
            currency -= amount;
            onCurrencyChanged.Invoke(); // ← Dispara evento
            return true;
        }
        return false;
    }

    private void Awake()
    {
        main = this;
        startingHP = playerHP;
    }

    private void Start()
    {
        RefreshHP();
        currency = startingCurrency;
    }

    // Vida no HUD. Muda de COR conforme o estoque cai: até aqui o número mudava de valor sem
    // nunca mudar de aparência, e dava para sangrar até quase zero sem notar no meio da onda.
    private void RefreshHP()
    {
        if (HPText == null) return;

        HPText.text = "Vida: " + playerHP;

        float frac = startingHP > 0 ? (float)playerHP / startingHP : 1f;
        HPText.color = frac <= 0.15f ? new Color(1f, 0.3f, 0.3f)     // crítico
                     : frac <= 0.40f ? new Color(1f, 0.75f, 0.3f)    // atenção
                     : Color.white;
    }

    // Ponto da rota mais próximo de uma posição. Usado por efeitos que precisam ser plantados
    // SOBRE o caminho (campos de espinhos, armadilhas) em vez de na posição da torre.
    public Vector3 ClosestPointOnPath(Vector3 from)
    {
        if (path == null || path.Length == 0) return from;

        Vector3 best = path[0].position;
        float bestDist = float.MaxValue;
        Vector3 prev = startPoint != null ? startPoint.position : path[0].position;

        for (int i = 0; i < path.Length; i++)
        {
            Vector3 cur = path[i].position;
            Vector3 seg = cur - prev;
            float len2 = seg.sqrMagnitude;
            Vector3 point = len2 < 0.0001f
                ? cur
                : prev + seg * Mathf.Clamp01(Vector3.Dot(from - prev, seg) / len2);

            float d = (point - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = point; }
            prev = cur;
        }
        return best;
    }

    // Painel de vitória: usa a referência do inspetor ou acha por nome ao lado do painel de morte.
    private GameObject VictoryPanel()
    {
        if (victoryPanel != null) return victoryPanel;
        if (deathPanel != null && deathPanel.transform.parent != null)
        {
            Transform vp = deathPanel.transform.parent.Find("Victory Panel");
            if (vp != null) victoryPanel = vp.gameObject;
        }
        return victoryPanel;
    }

    // Recupera vida (habilidade de Reparo). Não passa da vida com que a fase começou.
    public void RepairPlayer(int amount)
    {
        if (isDead) return;
        playerHP = Mathf.Min(startingHP, playerHP + amount);
        RefreshHP();
    }

    public void TakePlayerDamage(int amount)
    {
        if (isDead) return; // dois vazamentos no mesmo frame não podem disparar a derrota duas vezes

        playerHP -= amount;
        RefreshHP();
        AudioManager.Cue(AudioManager.Sfx.PlayerHurt);

        if (playerHP <= 0)
        {
            AudioManager.Cue(AudioManager.Sfx.Defeat);
            SaveAndShowHighscore();
            deathPanel.SetActive(true);
            isDead = true;
            Time.timeScale = 0f;
        }
    }

    // Fecha a partida: credita XP permanente, salva o recorde e mostra o resultado no painel.
    private void SaveAndShowHighscore(bool victory = false)
    {
        int reached = EnemySpawner.main != null ? EnemySpawner.main.CurrentWave : 0;
        int levelBefore = PlayerProgress.Level;
        int xpGanho = PlayerProgress.RecordRun(reached, victory, StageId);
        bool subiuDeNivel = PlayerProgress.Level > levelBefore;

        // O XP das rodadas já foi creditado uma a uma durante a partida (ver
        // PlayerProgress.CreditRound); o painel mostra o total da partida para o jogador não
        // achar que só ganhou o bônus de vitória.
        int xpDasRodadas = Mathf.Max(0, reached - 1) * PlayerProgress.XPPorRodada;
        string resumo = $"Rodada alcançada: {reached}\nRecorde: {PlayerProgress.BestRound}"
                      + $"\n+{xpDasRodadas + xpGanho} XP  ·  Comandante nível {PlayerProgress.Level}"
                      + (subiuDeNivel ? "  (SUBIU DE NÍVEL!)" : "");

        // O painel exibido é o de vitória ou o de morte, conforme o desfecho.
        GameObject painel = victory ? VictoryPanel() : deathPanel;
        if (painel == null) painel = deathPanel;
        if (painel == null) return;

        Transform t = painel.transform.Find("BestRoundText");
        if (t != null)
        {
            TMP_Text txt = t.GetComponent<TMP_Text>();
            if (txt != null) txt.text = resumo;
        }
    }

    // Vitória: sobreviveu a todas as rodadas.
    public void Win()
    {
        if (isDead) return;
        isDead = true;
        Time.timeScale = 0f;
        AudioManager.Cue(AudioManager.Sfx.Victory);
        SaveAndShowHighscore(victory: true);

        GameObject vp = VictoryPanel();
        if (vp != null) vp.SetActive(true);
        else if (deathPanel != null) deathPanel.SetActive(true); // fallback
    }

    public void BackToMenu(string scene)
    {
        GameManager.Instance.LoadScene(scene);
        Time.timeScale = 1f;
    }

    public void RestartGame(string scene)
    {
        // A cena recarrega do zero, então basta destravar o tempo — Awake/Start restauram o resto.
        Time.timeScale = 1f;
        isDead = false;
        GameManager.Instance.LoadScene(scene);
    }

    public void ExitGame()
    {
        GameManager.Instance.ExitGameBtn();
        Time.timeScale = 1f;
    }

    // Repete a rodada atual sem recarregar a cena (botão dos painéis de morte/vitória).
    public void RestartWave()
    {
        int wave = EnemySpawner.main != null ? EnemySpawner.main.CurrentWave : 1;

        if (BuildManager.main != null) BuildManager.main.ResetAllTowers();
        if (AllyManager.main != null) AllyManager.main.ClearAll();
        if (EnemySpawner.main != null) EnemySpawner.main.RestartCurrentWave();
        if (UIManager.main != null) UIManager.main.HideUpgradeUI();

        playerHP = startingHP;
        RefreshHP();

        // Verba proporcional à rodada: sem as torres perdidas, a defesa precisa ser remontada do zero.
        currency = startingCurrency + Mathf.Max(0, wave - 1) * retryCurrencyPerWave;
        onCurrencyChanged.Invoke();

        if (deathPanel != null) deathPanel.SetActive(false);
        GameObject vp = VictoryPanel();
        if (vp != null) vp.SetActive(false);

        isDead = false;
        Time.timeScale = 1f;
        if (GameSpeedController.main != null) GameSpeedController.main.ResetSpeed();
    }
}