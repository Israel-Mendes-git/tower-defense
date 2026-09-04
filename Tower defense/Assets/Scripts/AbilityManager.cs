using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Poderes acionáveis durante a onda, com cooldown. Existem para que o jogador tenha o que FAZER
// enquanto a rodada corre — e para que uma rodada quase perdida ainda possa ser salva por uma decisão,
// não só pelo que já estava construído. Os botões são procurados por nome no Canvas (Ability0..N).
public class AbilityManager : MonoBehaviour
{
    public static AbilityManager main;

    private class Ability
    {
        public string name;
        public string hint;
        public float cooldown;
        public float readyAt;
        public System.Action cast;
        public Button button;
        public TMP_Text label;
        public Image image;

        public bool Ready => Time.time >= readyAt;
        public float Remaining => Mathf.Max(0f, readyAt - Time.time);
    }

    [Header("Bombardeio")]
    [SerializeField] private int strikeDamage = 250;
    [SerializeField] private float strikeCooldown = 45f;

    [Header("Congelamento")]
    [SerializeField] private float freezeDuration = 4f;
    [SerializeField] private float freezeCooldown = 40f;

    [Header("Reparo")]
    [SerializeField] private int repairAmount = 15;
    [SerializeField] private float repairCooldown = 90f;

    [SerializeField] private LayerMask enemyMask = 64;

    private readonly List<Ability> abilities = new List<Ability>();
    private readonly KeyCode[] hotkeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

    private void Awake()
    {
        main = this;

        abilities.Add(new Ability { name = "Bombardeio", hint = "[1]", cooldown = strikeCooldown, cast = CastStrike });
        abilities.Add(new Ability { name = "Congelar",   hint = "[2]", cooldown = freezeCooldown, cast = CastFreeze });
        abilities.Add(new Ability { name = "Reparo",     hint = "[3]", cooldown = repairCooldown, cast = CastRepair });

        BindButtons();
    }

    private void BindButtons()
    {
        for (int i = 0; i < abilities.Count; i++)
        {
            GameObject go = GameObject.Find("Ability" + i);
            if (go == null) continue;

            Ability a = abilities[i];
            a.button = go.GetComponent<Button>();
            a.image = go.GetComponent<Image>();
            a.label = go.GetComponentInChildren<TMP_Text>(true);

            int index = i; // capturado por valor para o listener
            if (a.button != null) a.button.onClick.AddListener(() => Use(index));
        }
    }

    private void Update()
    {
        for (int i = 0; i < abilities.Count && i < hotkeys.Length; i++)
            if (Input.GetKeyDown(hotkeys[i])) Use(i);

        RefreshUI();
    }

    public void Use(int index)
    {
        if (index < 0 || index >= abilities.Count) return;
        if (LevelManager.main == null || LevelManager.main.isDead) return;

        // Poder ainda não conquistado (ver Unlocks). O primeiro nasce liberado de propósito:
        // são os poderes que respondem ao "fico sem fazer nada durante a onda", então trancar
        // todos seria piorar exatamente o que eles existem para resolver.
        if (!Unlocks.PoderLiberado(index)) return;

        Ability a = abilities[index];
        if (!a.Ready) return;

        a.cast();
        a.readyAt = Time.time + a.cooldown;
        AudioManager.Cue(AudioManager.Sfx.Ability);
    }

    private void RefreshUI()
    {
        foreach (Ability a in abilities)
        {
            if (a.label != null)
                a.label.text = a.Ready ? $"{a.name}\n<size=70%>{a.hint}</size>"
                                       : $"{a.name}\n<size=70%>{a.Remaining:0}s</size>";
            if (a.button != null) a.button.interactable = a.Ready;
            if (a.image != null)
                a.image.color = a.Ready ? new Color(0.25f, 0.45f, 0.65f, 0.95f)
                                        : new Color(0.15f, 0.17f, 0.22f, 0.9f);
        }
    }

    // ───────── Efeitos ─────────

    // Dano pesado em todos os inimigos em campo. O botão de pânico clássico.
    private void CastStrike()
    {
        int atingidos = 0;
        foreach (EnemyMovement e in FindObjectsOfType<EnemyMovement>())
        {
            Health h = e.GetComponent<Health>();
            if (h == null) continue;
            // vê camo, ignora armadura E ignora a imunidade a explosivo da cerâmica de propósito: é
            // trunfo de emergência com 45s de recarga, não dano de torre — virar botão inútil bem na
            // rodada em que socorre seria punição sem leitura nenhuma para o jogador.
            h.TakeDamage(strikeDamage, true, false, true);
            atingidos++;
        }
        Announce($"BOMBARDEIO! {atingidos} atingidos", new Color(1f, 0.6f, 0.2f));
    }

    // Paralisa a onda inteira: compra tempo para reposicionar aliados e vender/comprar torres.
    private void CastFreeze()
    {
        foreach (EnemyMovement e in FindObjectsOfType<EnemyMovement>())
            StartCoroutine(FreezeOne(e));
        Announce("CONGELADO!", new Color(0.5f, 0.9f, 1f));
    }

    private System.Collections.IEnumerator FreezeOne(EnemyMovement e)
    {
        if (e == null) yield break;
        e.UpdateSpeed(0f);
        yield return new WaitForSeconds(freezeDuration);
        if (e != null) e.ResetSpeed();
    }

    private void CastRepair()
    {
        LevelManager.main.RepairPlayer(repairAmount);
        Announce($"+{repairAmount} de vida", new Color(0.5f, 1f, 0.6f));
    }

    private void Announce(string text, Color color)
    {
        Camera cam = Camera.main;
        Vector3 pos = cam != null ? cam.transform.position + Vector3.forward * 10f : Vector3.zero;
        pos.z = 0f;
        FloatingText.Spawn(pos, text, color);
    }
}
