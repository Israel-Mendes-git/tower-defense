using System.Collections.Generic;
using UnityEngine;

// Sinergia de adjacência: torres vizinhas de tipos que combinam formam um COMBO NOMEADO,
// com bônus real e uma linha ligando as duas. É o que faz o posicionamento virar decisão de build —
// a melhor posição de tiro deixa de ser automaticamente a melhor posição.
public class SynergyManager : MonoBehaviour
{
    public static SynergyManager main;

    [SerializeField] private float linkRange = 2.2f;   // distância máxima para duas torres se ligarem
    [SerializeField] private bool drawLinks = true;

    // Um combo entre dois tipos de torre.
    private class Combo
    {
        public readonly string a, b, name;
        public readonly float damage, rate, range;
        public readonly Color color;

        public Combo(string a, string b, string name, float damage, float rate, float range, Color color)
        {
            this.a = a; this.b = b; this.name = name;
            this.damage = damage; this.rate = rate; this.range = range; this.color = color;
        }

        public bool Matches(string x, string y) => (a == x && b == y) || (a == y && b == x);
    }

    // A tabela é a regra do jogo: combos poucos, nomeados e fortes o bastante para mudar onde você constrói.
    private static readonly Combo[] combos =
    {
        new Combo("IceTurret", "TeslaTurret", "Supercondutor",
            damage: 1.6f, rate: 1f, range: 1f, color: new Color(0.4f, 0.9f, 1f)),

        new Combo("DetectorTurret", "SniperTurret", "Torre de Vigia",
            damage: 1.5f, rate: 1f, range: 1.25f, color: new Color(0.9f, 0.5f, 1f)),

        new Combo("AoETurret", "TachinhaTurret", "Terra Arrasada",
            damage: 1.35f, rate: 1.15f, range: 1f, color: new Color(1f, 0.55f, 0.2f)),

        new Combo("Turret", "MachineGunTurret", "Linha de Fogo",
            damage: 1f, rate: 1.4f, range: 1f, color: new Color(1f, 0.9f, 0.3f)),

        new Combo("FarmTower", "DetectorTurret", "Logística",
            damage: 1.3f, rate: 1f, range: 1f, color: new Color(0.5f, 1f, 0.5f)),

        new Combo("SniperTurret", "TeslaTurret", "Bobina de Carga",
            damage: 1f, rate: 1.5f, range: 1f, color: new Color(0.6f, 0.7f, 1f)),
    };

    // Combos ativos neste frame, por torre — a UI lê daqui para listar no painel.
    private readonly Dictionary<TowerBase, List<string>> activeByTower = new Dictionary<TowerBase, List<string>>();
    private readonly List<LineRenderer> linkPool = new List<LineRenderer>();
    private int linksUsed;

    private void Awake() => main = this;

    // Sinergia é geometria entre torres, e torre não se move nem nasce a cada frame. Rodar isto
    // 60 vezes por segundo era desperdício puro: `FindObjectsOfType` varrendo a cena inteira mais
    // um laço O(n²) sobre as torres, todo frame. Cinco vezes por segundo é imperceptível para o
    // jogador e devolve o custo ao orçamento de quadro.
    private const float IntervaloDeAvaliacao = 0.2f;
    private float proximaAvaliacao;

    private void Update()
    {
        if (Time.time < proximaAvaliacao) return;
        proximaAvaliacao = Time.time + IntervaloDeAvaliacao;

        activeByTower.Clear();
        linksUsed = 0;

        IReadOnlyList<TowerBase> towers = TowerBase.Todas;

        for (int i = 0; i < towers.Count; i++)
        {
            for (int j = i + 1; j < towers.Count; j++)
            {
                TowerBase x = towers[i], y = towers[j];
                if (IsoGrid.CellDistance(x.transform.position, y.transform.position) > linkRange) continue;

                Combo c = Find(x.GetType().Name, y.GetType().Name);
                if (c == null) continue;

                // Buff mútuo — os dois lados ganham. A sinergia é do PAR, não de uma torre sobre a outra.
                x.ApplyBuff(c.damage, c.rate, c.range);
                y.ApplyBuff(c.damage, c.rate, c.range);

                Register(x, c.name);
                Register(y, c.name);

                if (drawLinks) DrawLink(x.transform.position, y.transform.position, c.color);
            }
        }

        HideUnusedLinks();
    }

    private static Combo Find(string a, string b)
    {
        foreach (Combo c in combos)
            if (c.Matches(a, b)) return c;
        return null;
    }

    private void Register(TowerBase t, string name)
    {
        if (!activeByTower.TryGetValue(t, out List<string> list))
        {
            list = new List<string>();
            activeByTower[t] = list;
        }
        if (!list.Contains(name)) list.Add(name);
    }

    // Nomes dos combos ativos numa torre (usado pelo painel de upgrade).
    public List<string> ActiveFor(TowerBase t)
        => activeByTower.TryGetValue(t, out List<string> list) ? list : null;

    // ───────── Linhas de ligação ─────────
    private void DrawLink(Vector3 a, Vector3 b, Color color)
    {
        LineRenderer lr;
        if (linksUsed < linkPool.Count) lr = linkPool[linksUsed];
        else
        {
            var go = new GameObject("SynergyLink");
            go.transform.SetParent(transform, false);
            lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.widthMultiplier = 0.08f;
            lr.numCapVertices = 4;
            // Mesma camada do anel de alcance (ver RangeIndicator): sobreposição informativa presa
            // ao mundo, que precisa ficar acima do chão isométrico — cuja ordem por célula passa
            // de 2000 e engolia o valor fixo de 50 que valia na era top-down.
            lr.sortingLayerName = "Turrets";
            lr.sortingOrder = 0;
            lr.positionCount = 2;
            linkPool.Add(lr);
        }

        linksUsed++;
        lr.gameObject.SetActive(true);
        Color faded = new Color(color.r, color.g, color.b, 0.75f);
        lr.startColor = faded;
        lr.endColor = faded;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }

    private void HideUnusedLinks()
    {
        for (int i = linksUsed; i < linkPool.Count; i++)
            if (linkPool[i] != null) linkPool[i].gameObject.SetActive(false);
    }
}
