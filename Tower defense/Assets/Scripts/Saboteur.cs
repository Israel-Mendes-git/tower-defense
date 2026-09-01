using UnityEngine;

// Sabotador — inimigo que ATACA A SUA DEFESA em vez de só andar até o fim.
// Ao passar perto, desliga a torre por alguns segundos. Obriga o jogador a matá-lo com prioridade,
// não a simplesmente ter DPS suficiente: ignorá-lo abre um buraco na linha bem onde ele passou.
public class Saboteur : MonoBehaviour
{
    [SerializeField] private float sabotageRadius = 1.6f;
    [SerializeField] private float disableSeconds = 5f;
    [SerializeField] private float interval = 3f;   // tempo entre sabotagens

    private float nextSabotage;

    private void Update()
    {
        if (Time.time < nextSabotage) return;
        nextSabotage = Time.time + interval;

        TowerBase alvo = null;
        float melhor = float.MaxValue;

        // Sabota a torre ativa mais próxima — a que estava efetivamente atrapalhando a passagem.
        foreach (TowerBase t in FindObjectsOfType<TowerBase>())
        {
            if (t.IsDisabled) continue;
            float d = IsoGrid.CellDistance(t.transform.position, transform.position);
            if (d <= sabotageRadius && d < melhor) { melhor = d; alvo = t; }
        }

        if (alvo == null) return;

        alvo.Disable(disableSeconds);
        FloatingText.Spawn(alvo.transform.position, "SABOTADA!", new Color(1f, 0.4f, 0.9f));
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, sabotageRadius);
    }
#endif
}
