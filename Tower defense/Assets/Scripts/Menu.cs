using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Menu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] TextMeshProUGUI currencyUI;
    [SerializeField] Animator anim;

    // Era OnGUI, que roda várias vezes por frame (o loop de eventos da IMGUI) só para escrever um
    // texto — e sem guarda nenhuma: lançava NullReferenceException a cada evento sempre que
    // LevelManager.main ainda não existisse, o caso normal durante um domain reload.
    private void Update()
    {
        if (currencyUI != null && LevelManager.main != null)
            currencyUI.text = LevelManager.main.currency.ToString();
    }

    private bool isMenuOpen = true;

    private void Start()
    {
        // Garante que o painel do shop comece no estado certo (aberto), sincronizando o Animator.
        if (anim != null) anim.SetBool("MenuOpen", isMenuOpen);
    }

    public void ToggleMenu()
    {
        if (LevelManager.main.isDead == true) return;

        isMenuOpen = !isMenuOpen;
        anim.SetBool("MenuOpen", isMenuOpen);
    }

    public void SetSelected()
    {

    }
}
