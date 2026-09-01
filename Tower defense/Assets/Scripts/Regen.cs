using System.Collections;
using UnityEngine;

// Inimigo regenerador: cura HP periodicamente (estilo "regrow" do Bloons).
// Força o jogador a matá-lo rápido, senão ele se recupera.
[RequireComponent(typeof(Health))]
public class Regen : MonoBehaviour
{
    [SerializeField] private int healPerTick = 1;
    [SerializeField] private float tickInterval = 0.6f;

    private Health health;

    private void Start()
    {
        health = GetComponent<Health>();
        StartCoroutine(RegenLoop());
    }

    private IEnumerator RegenLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(tickInterval);
            if (health != null) health.Heal(healPerTick);
        }
    }
}
