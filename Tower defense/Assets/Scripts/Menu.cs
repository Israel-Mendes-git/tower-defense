using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Menu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] TextMeshProUGUI currencyUI;
    [SerializeField] Animator anim;

    private void OnGUI()
    {
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
