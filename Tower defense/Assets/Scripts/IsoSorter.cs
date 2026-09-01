using UnityEngine;

// Mantém a ordem de desenho de um objeto coerente com onde ele está no tabuleiro isométrico.
//
// O chão usa uma ordem fixa por célula (IsoGrid.SortingOrder). Quem fica SOBRE o chão precisa de
// uma ordem derivada da posição: uma torre resolve isso uma vez, mas um inimigo anda entre células
// e, sem reavaliar, passaria por trás de um tile que já deveria estar tapando.
public class IsoSorter : MonoBehaviour
{
    [SerializeField] private bool moves = true;

    private SpriteRenderer[] renderers;
    private Vector3 origin;
    private int lastOrder = int.MinValue;

    // Usado por quem instancia (spawner de inimigos, plot ao construir, recrutamento de aliado).
    public static void Attach(GameObject go, bool moves)
    {
        if (go == null) return;

        IsoSorter s = go.GetComponent<IsoSorter>();
        if (s == null) s = go.AddComponent<IsoSorter>();
        s.moves = moves;
        s.Init();
        s.Apply();
    }

    private void Start()
    {
        if (renderers == null) Init();
        Apply();
    }

    private void Init()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        origin = IsoBoard.main != null ? IsoBoard.main.Origin : Vector3.zero;
    }

    private void LateUpdate()
    {
        if (moves) Apply();
    }

    private void Apply()
    {
        if (renderers == null) return;

        int order = IsoGrid.SortingOrderAt(transform.position, origin);
        if (order == lastOrder) return; // parado: não repinta a cada frame
        lastOrder = order;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;
            if (sr.GetComponent<RangeIndicator>() != null) continue; // tem ordem própria, fica por cima
            sr.sortingOrder = order;
        }
    }
}
