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

    public bool isDead;
    public int currency;

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
    }

    private void Start()
    {
        HPText.text = "Vida: " + playerHP.ToString();
        currency = 100;
    }

    public void TakePlayerDamage(int amount)
    {
        playerHP -= amount;
        HPText.text = "Vida: " + playerHP.ToString();

        if (playerHP <= 0)
        {
            deathPanel.SetActive(true);
            isDead = true;
            Time.timeScale = 0f;

            // Aqui você pode chamar o restart da wave
            // EnemySpawner.main.RestartCurrentWave();  ← descomente quando quiser restart automático
        }
    }

    public void BackToMenu(string scene)
    {
        GameManager.Instance.LoadScene(scene);
        Time.timeScale = 1f;
    }

    public void RestartGame(string scene)
    {
        GameManager.Instance.LoadScene(scene);
        Time.timeScale = 1f;
        isDead = false;
        currency = 100;
        playerHP = 150;
    }

    public void ExitGame()
    {
        GameManager.Instance.ExitGameBtn();
        Time.timeScale = 1f;
    }

    // Novo: Reiniciar a wave atual (chamado do botão no deathPanel)
    public void RestartWave()
    {
        // Chama o reset das torretas
        if (BuildManager.main != null)
        {
            BuildManager.main.ResetAllTowers();
        }

        // Chama o reset da wave no spawner
        if (EnemySpawner.main != null)
        {
            EnemySpawner.main.RestartCurrentWave();
        }

        // Restaura vida do jogador (opcional)
        playerHP = 150; // ou o valor inicial da wave
        HPText.text = "Vida: " + playerHP.ToString();
        currency = 100;
        

        // Fecha painel de morte e volta o tempo
        deathPanel.SetActive(false);
        isDead = false;
        Time.timeScale = 1f;
    }
}