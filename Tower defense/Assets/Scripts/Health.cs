using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("Attributes")]
    [SerializeField] private int hitPoints = 2;
    [SerializeField] private int currencyWorth = 50;

    [Header("Traits (estilo Bloons)")]
    [SerializeField] private bool isCamo = false;   // só torres com detecção (canSeeCamo) acertam
    [SerializeField] private int armor = 0;          // reduz o dano recebido (mín. 1 por acerto)
    [SerializeField] private bool leadArmor = false; // chumbo: imune a dano cortante (dardos/tachinhas)

    private bool isDestroyed = false;
    private bool registered = false;
    private int maxHitPoints;

    // Marcação (verbo da torre Detector): enquanto marcado, TODO dano recebido é amplificado.
    private float markMultiplier = 1f;
    private float markUntil;

    // Flash de acerto: sem ele, tomar dano e não tomar dano são visualmente idênticos.
    private SpriteRenderer sr;
    private Color normalColor;
    private float flashUntil;
    private const float FlashDuration = 0.07f;

    private void Awake()
    {
        // Fases difíceis endurecem os inimigos sem exigir prefabs próprios para cada mapa.
        if (EnemySpawner.main != null && EnemySpawner.main.EnemyHealthMultiplier != 1f)
            hitPoints = Mathf.Max(1, Mathf.RoundToInt(hitPoints * EnemySpawner.main.EnemyHealthMultiplier));

        maxHitPoints = hitPoints;
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) normalColor = sr.color;
    }

    private void Update()
    {
        if (sr == null || flashUntil <= 0f) return;

        if (Time.time >= flashUntil)
        {
            sr.color = IsShielded ? Color.Lerp(normalColor, new Color(0.6f, 0.8f, 1f), 0.5f) : normalColor;
            flashUntil = 0f;
        }
    }

    public int GetHitPoints() => hitPoints;
    public bool IsCamo => isCamo;
    public bool IsMarked => Time.time < markUntil;

    // Marca o alvo: quem atirar nele bate mais forte. Marcas mais fortes substituem as fracas.
    public void Mark(float multiplier, float duration)
    {
        if (isDestroyed) return;
        if (IsMarked && multiplier < markMultiplier) return;
        markMultiplier = multiplier;
        markUntil = Time.time + duration;
    }

    // Armadura emprestada por um inimigo Escudeiro. Expira por frame: quando o escudeiro morre,
    // a proteção deixa de ser renovada e some sozinha, sem ninguém precisar avisar o grupo.
    private int borrowedArmor;
    private int armorFrame = -99;

    private int EffectiveArmor => armor + (armorFrame >= Time.frameCount - 1 ? borrowedArmor : 0);
    public bool IsShielded => armorFrame >= Time.frameCount - 1 && borrowedArmor > 0;

    public void GrantTemporaryArmor(int amount)
    {
        if (armorFrame != Time.frameCount)
        {
            armorFrame = Time.frameCount;
            borrowedArmor = 0;
        }
        borrowedArmor = Mathf.Max(borrowedArmor, amount); // não empilha: vale o escudo mais forte
    }

    // Cura (usado pelo inimigo Regenerador), limitado ao HP máximo.
    public void Heal(int amount)
    {
        if (isDestroyed) return;
        hitPoints = Mathf.Min(maxHitPoints, hitPoints + amount);
    }

    private void OnEnable()
    {
        if (registered) return;
        registered = true;
        EnemySpawner.onEnemySpawn.Invoke();
    }

    public bool LeadArmor => leadArmor;

    public void TakeDamage(int dmg, bool fromDetector = false, bool isSharp = false, bool ignoreArmor = false)
    {
        if (isDestroyed) return;
        if (isCamo && !fromDetector) return;  // camuflado: só detectores acertam
        if (leadArmor && isSharp) return;      // chumbo: imune a dardos/tachinhas (só explosão/energia/sniper)

        if (IsMarked) dmg = Mathf.RoundToInt(dmg * markMultiplier); // alvo marcado recebe dano amplificado

        int reduction = ignoreArmor ? 0 : EffectiveArmor;
        hitPoints -= Mathf.Max(1, dmg - reduction); // armadura reduz, mas sempre tira ao menos 1

        if (sr != null)
        {
            sr.color = Color.white;
            flashUntil = Time.time + FlashDuration;
        }

        if (hitPoints <= 0)
        {
            isDestroyed = true;

            SpawnAfterDead spawnComponent = GetComponent<SpawnAfterDead>();
            if (spawnComponent != null)
            {
                spawnComponent.SpawnEnemiesOnDeath();
            }

            // Ladrão devolve o que roubou ao ser abatido.
            Thief thief = GetComponent<Thief>();
            if (thief != null) thief.ReturnLoot();

            EnemySpawner.onEnemyDestroy.Invoke();
            LevelManager.main.IncreaseCurrency(currencyWorth);

            // Juice: estouro + dinheiro flutuante
            AudioManager.Cue(AudioManager.Sfx.Pop);
            DeathPop.Spawn(GetComponent<SpriteRenderer>());
            FloatingText.Spawn(transform.position, "+" + currencyWorth, new Color(1f, 0.9f, 0.3f));

            Destroy(gameObject);
        }
    }

}