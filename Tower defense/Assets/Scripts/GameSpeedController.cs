using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Controla a velocidade do jogo (1x/2x/3x) estilo Bloons.
public class GameSpeedController : MonoBehaviour
{
    [SerializeField] private float[] speeds = { 1f, 2f, 3f };
    [SerializeField] private string buttonName = "FastForwardButton";

    private Button speedButton;
    private TMP_Text speedLabel;
    private int index = 0;

    public static GameSpeedController main;
    public float CurrentSpeed => speeds[index];

    private void Awake() { main = this; }

    private void Start()
    {
        GameObject go = GameObject.Find(buttonName);
        if (go != null)
        {
            speedButton = go.GetComponent<Button>();
            speedLabel = go.GetComponentInChildren<TMP_Text>();
            if (speedButton != null) speedButton.onClick.AddListener(Cycle);
        }
        Apply();
    }

    public void Cycle()
    {
        index = (index + 1) % speeds.Length;
        Apply();
    }

    // Volta pra 1x (usado ao reiniciar / iniciar o jogo).
    public void ResetSpeed()
    {
        index = 0;
        Apply();
    }

    private void Apply()
    {
        // Só mexe no timeScale se o jogo não estiver pausado/morto
        if (LevelManager.main == null || !LevelManager.main.isDead)
            Time.timeScale = speeds[index];

        if (speedLabel != null) speedLabel.text = speeds[index].ToString("0") + "x";
    }
}
