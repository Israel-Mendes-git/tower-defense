using UnityEngine;

// Escudeiro — protege os inimigos ao redor, reduzindo o dano que eles recebem.
// Enquanto ele viver, o grupo inteiro fica difícil de derrubar; matá-lo primeiro desmonta a onda.
// É o inimigo que ensina o jogador a usar prioridade de alvo em vez de deixar as torres no automático.
public class Shielder : MonoBehaviour
{
    [SerializeField] private float auraRadius = 2.2f;
    [SerializeField] private int armorGranted = 6;
    [SerializeField] private LayerMask allyEnemyMask = 64; // a layer dos próprios inimigos

    private void Update()
    {
        // Reaplicado todo frame: a proteção acaba no instante em que o escudeiro morre.
        foreach (Collider2D c in Physics2D.OverlapCircleAll(transform.position, IsoGrid.WorldRadiusFor(auraRadius), allyEnemyMask))
        {
            if (IsoGrid.CellDistance(transform.position, c.transform.position) > auraRadius) continue;

            Health h = c.GetComponent<Health>();
            if (h == null || h.gameObject == gameObject) continue;
            h.GrantTemporaryArmor(armorGranted);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, auraRadius);
    }
#endif
}
