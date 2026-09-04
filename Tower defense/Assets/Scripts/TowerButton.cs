using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TowerButton : MonoBehaviour
{
    [SerializeField] private int towerIndex = 0;
    [SerializeField] private GameObject aura; // highlight / borda / glow
    [SerializeField] private TMP_Text costTxt;

    private Button button;
    private bool costSet = false; // o custo é fixo: escreve uma vez só
    private int lastAfford = -1;  // -1 = ainda não avaliado; evita mexer no ColorBlock todo frame

    // Paleta escura da UI: o preço em âmbar sobre botão escuro. Antes era branco sobre botão
    // claro — o valor existia mas era invisível na tela.
    private static readonly Color Affordable = new Color32(0xE8, 0xA3, 0x3D, 0xFF);   // âmbar
    private static readonly Color TooExpensive = new Color32(0xC0, 0x5A, 0x4A, 0xFF); // vermelho
    private static readonly Color IconNormal = new Color32(0x27, 0x36, 0x3C, 0xFF);   // botão
    private static readonly Color IconDimmed = new Color32(0x1B, 0x24, 0x28, 0xFF);   // sem verba
    private static readonly Color Locked = new Color32(0x6E, 0x7B, 0x82, 0xFF);       // travado: cinza frio
    private static readonly Color IconLocked = new Color32(0x14, 0x1A, 0x1D, 0xFF);   // mais apagado que "sem verba"

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);

        if (aura != null)
            aura.SetActive(false);
    }

    private void Update()
    {
        if (BuildManager.main == null || LevelManager.main == null) return;

        Tower tower = BuildManager.main.GetTower(towerIndex);
        if (tower == null) return;

        // Torre ainda não conquistada (ver Unlocks): o botão mostra o NÍVEL que falta em vez do
        // preço. Ele continua na loja de propósito — ver o que ainda vem é metade do motivo de
        // voltar a jogar; esconder transformaria progressão em surpresa.
        bool liberada = Unlocks.TorreLiberada(towerIndex);
        if (!liberada)
        {
            if (costTxt != null)
            {
                costTxt.text = "Nv " + Unlocks.NivelExigidoPelaTorre(towerIndex);
                costTxt.color = Locked;
                costSet = false; // ao destravar, o preço precisa ser reescrito
            }
            if (button != null && lastAfford != 2)
            {
                lastAfford = 2;
                ColorBlock cb = button.colors;
                cb.normalColor = IconLocked;
                button.colors = cb;
            }
            if (aura != null) aura.SetActive(false);
            return;
        }

        if (costTxt != null && !costSet)
        {
            costTxt.text = tower.cost.ToString();
            costSet = true;
        }

        // A cor precisa ser reavaliada TODO frame, inclusive naquele em que o custo foi escrito.
        // Antes isto vivia no else do if acima, então o primeiro frame nunca coloria.
        bool afford = tower.cost <= LevelManager.main.currency;
        if (costTxt != null)
            costTxt.color = afford ? Affordable : TooExpensive;

        // O botão inteiro apaga quando não dá para pagar: só o preço em vermelho passava
        // despercebido numa loja com nove torres. Vai pelo ColorBlock do Button, e não por
        // Image.color, senão a própria transição de ColorTint sobrescreveria no primeiro hover.
        int afford01 = afford ? 1 : 0;
        if (button != null && lastAfford != afford01)
        {
            lastAfford = afford01;
            ColorBlock cb = button.colors;
            cb.normalColor = afford ? IconNormal : IconDimmed;
            button.colors = cb;
        }

        bool isSelected = BuildManager.main.GetSelectedTowerIndex() == towerIndex;
        if (aura != null)
            aura.SetActive(isSelected);
    }

    private void OnClick()
    {
        if (BuildManager.main == null) return;
        BuildManager.main.ToggleBuildMode(towerIndex);
    }
}
