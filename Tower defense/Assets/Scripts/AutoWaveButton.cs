using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Botão de auto-iniciar ondas: liga/desliga o auto-start do EnemySpawner e mostra o estado.
public class AutoWaveButton : MonoBehaviour
{
    [SerializeField] private string buttonName = "AutoWaveButton";

    private Button button;
    private TMP_Text label;
    private Image image;
    private bool lastState;

    private void Start()
    {
        GameObject go = GameObject.Find(buttonName);
        if (go != null)
        {
            button = go.GetComponent<Button>();
            image = go.GetComponent<Image>();
            label = go.GetComponentInChildren<TMP_Text>();
            if (button != null) button.onClick.AddListener(OnClick);
        }
        Refresh();
    }

    private void OnClick()
    {
        if (EnemySpawner.main != null) EnemySpawner.main.ToggleAutoStart();
        Refresh();
    }

    private void Update()
    {
        if (EnemySpawner.main != null && EnemySpawner.main.AutoStart != lastState) Refresh();
    }

    private void Refresh()
    {
        if (EnemySpawner.main == null) return;
        lastState = EnemySpawner.main.AutoStart;
        if (label != null) label.text = lastState ? "Auto: ON" : "Auto: OFF";
        if (image != null) image.color = lastState ? new Color(0.2f, 0.5f, 0.3f, 0.95f) : new Color(0.15f, 0.17f, 0.22f, 0.92f);
    }
}
