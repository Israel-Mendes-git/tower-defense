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
