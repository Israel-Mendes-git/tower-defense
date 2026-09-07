using UnityEngine;

// Escudeiro — protege os inimigos ao redor, reduzindo o dano que eles recebem.
// Enquanto ele viver, o grupo inteiro fica difícil de derrubar; matá-lo primeiro desmonta a onda.
// É o inimigo que ensina o jogador a usar prioridade de alvo em vez de deixar as torres no automático.
public class Shielder : MonoBehaviour
{
    [SerializeField] private float auraRadius = 2.2f;
    [SerializeField] private int armorGranted = 6;
    [SerializeField] private LayerMask allyEnemyMask = 64; // a layer dos próprios inimigos

    // Buffer COMPARTILHADO entre todos os escudeiros: OverlapCircleAll aloca um array novo a cada
    // chamada, e isto rodava por escudeiro POR FRAME — com 70 deles em campo eram 70 alocações e
    // 70 varreduras de física por quadro. Medido: 3 FPS com 57 inimigos, e picos de frame de até
    // 8,3 segundos. A versão NonAlloc escreve sempre no mesmo buffer.
    private static readonly Collider2D[] buffer = new Collider2D[64];

    // A aura também não precisa de 60 Hz. A armadura concedida dura mais que este intervalo (ver
    // GrantTemporaryArmor), então reaplicar 8 vezes por segundo mantém a proteção contínua e
    // preserva o que importa: ela sumir quando o escudeiro morre.
    private const float IntervaloDaAura = 0.125f;
    private float proximaAura;

    private void Update()
    {
        if (Time.time < proximaAura) return;
        proximaAura = Time.time + IntervaloDaAura;

        int n = Physics2D.OverlapCircleNonAlloc(transform.position,
            IsoGrid.WorldRadiusFor(auraRadius), buffer, allyEnemyMask);

        for (int i = 0; i < n; i++)
        {
            Collider2D c = buffer[i];
            if (c == null) continue;
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
