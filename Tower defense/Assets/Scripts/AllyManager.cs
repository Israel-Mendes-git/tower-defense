using System.Collections.Generic;
using UnityEngine;

// Recrutamento e comando dos aliados móveis.
// Fluxo: clicar no botão de recrutar entra em modo de posicionamento; o próximo clique no mapa
// coloca o aliado ali. Depois, clicar num aliado o seleciona e o clique seguinte no mapa manda
// ele patrulhar naquele ponto — a ordem pode ser dada NO MEIO da onda, e é isso que dá ao jogador
// algo para fazer enquanto a rodada corre.
public class AllyManager : MonoBehaviour
{
    public static AllyManager main;

    [System.Serializable]
    public class AllyType
    {
        public string name = "Aliado";
        public int cost = 300;
        public GameObject prefab;
    }

    [SerializeField] private List<AllyType> allyTypes = new List<AllyType>();

    private int pendingRecruit = -1;   // índice do aliado esperando ser posicionado
    private Ally selected;
    private readonly List<Ally> recruited = new List<Ally>();

    public bool IsPlacing => pendingRecruit >= 0;
    public Ally Selected => selected;

    private void Awake() => main = this;

    public AllyType GetType(int index)
        => (index >= 0 && index < allyTypes.Count) ? allyTypes[index] : null;

    public int TypeCount => allyTypes.Count;

    // Chamado pelo botão da UI.
    public void BeginRecruit(int index)
    {
        AllyType t = GetType(index);
        if (t == null || LevelManager.main == null || LevelManager.main.isDead) return;
        if (t.cost > LevelManager.main.currency) return;

        pendingRecruit = index;
        Deselect();
    }

    public void CancelRecruit() => pendingRecruit = -1;

    public void Select(Ally ally)
    {
        if (selected != null && selected != ally) selected.HideZone();
        selected = ally;
        if (selected != null) selected.ShowZone();
    }

    public void Deselect()
    {
        if (selected != null) selected.HideZone();
        selected = null;
    }

    private void Update()
    {
        if (LevelManager.main == null || LevelManager.main.isDead) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (UIManager.main != null && UIManager.main.IsHoveringUI()) return;

        Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;

        if (IsPlacing) { PlaceAt(world); return; }

        // Com um aliado selecionado, clicar no mapa é uma ORDEM de patrulha.
        // Clique sobre o próprio aliado não conta (senão a seleção viraria ordem no mesmo clique).
        if (selected != null && Vector2.Distance(world, selected.transform.position) > 0.5f)
            selected.SetZone(world);
    }

    private void PlaceAt(Vector3 world)
    {
        AllyType t = GetType(pendingRecruit);
        pendingRecruit = -1;
        if (t == null || t.prefab == null) return;
        if (!LevelManager.main.SpendCurrency(t.cost)) return;

        GameObject go = Instantiate(t.prefab, world, Quaternion.identity);
        Ally ally = go.GetComponentInChildren<Ally>();
        if (ally != null)
        {
            ally.AllyName = t.name;
            ally.SetZone(world);
            recruited.Add(ally);
            Select(ally);
        }
    }

    // Usado pelo restart: some com todos os aliados recrutados.
    public void ClearAll()
    {
        Deselect();
        foreach (Ally a in recruited)
            if (a != null) Destroy(a.gameObject);
        recruited.Clear();
        pendingRecruit = -1;
    }
}
