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
    [SerializeField] private bool blastArmor = false; // cerâmica: imune a dano explosivo (só a explosão da Bomba — napalm é fogo, não conta)

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
        if (EnemySpawner.main != null)
        {
            // Fases difíceis endurecem os inimigos sem exigir prefabs próprios para cada mapa,
            // e a rodada endurece por cima disso (ver MultiplicadorDeVidaDaRodada).
            float mult = EnemySpawner.main.EnemyHealthMultiplier
                       * EnemySpawner.main.MultiplicadorDeVidaDaRodada;

            // O executor de uma jogada dirigida vem reforçado, para conseguir CUMPRIR o que foi
            // anunciado (ver CommanderPlays.MultiplicadorDoExecutor).
            if (GetComponent<Saboteur>() != null && CommanderPlays.main != null)
                mult *= CommanderPlays.main.MultiplicadorDoExecutor;

            if (mult != 1f) hitPoints = Mathf.Max(1, Mathf.RoundToInt(hitPoints * mult));
        }

        maxHitPoints = hitPoints;
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) normalColor = sr.color;
    }

    private static readonly Color ShieldTint = new Color(0.6f, 0.8f, 1f);

    private void Update()
    {
        if (sr == null) return;

        // O flash de acerto manda por cima enquanto durar (ver TakeDamage) — ele mesmo se
        // resolve, aqui só decide QUANDO o flash acabou de expirar.
        if (flashUntil > 0f)
        {
            if (Time.time < flashUntil) return;
            flashUntil = 0f;
        }

        // Antes o blend de escudo só era recalculado no instante em que um flash expirava — um
        // inimigo escudado que não tomasse dano ficava sem NENHUM aviso visual, e um que ficasse
        // sem escudo bem depois do último flash continuava mostrando o azulado antigo pra sempre.
        // Reavaliar todo frame (fora do flash) faz o efeito aparecer e sumir junto com o estado de
        // verdade, sem depender de o inimigo ter apanhado.
        Color target = IsShielded ? Color.Lerp(normalColor, ShieldTint, 0.5f) : normalColor;
        if (sr.color != target) sr.color = target;
    }

    public int GetHitPoints() => hitPoints;

    // O que o jogador ganha por matar este inimigo — e, do outro lado do tabuleiro, o preço que
    // o CounterCommander paga para colocá-lo em campo. Usar o MESMO número nos dois lados é
    // deliberado: o esforço que ele te impõe é o esforço que ele comprou.
    public int CurrencyWorth => currencyWorth;
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

    // A armadura emprestada vale por TEMPO, não por frame.
    //
    // Antes ela expirava no frame seguinte, o que obrigava o Escudeiro a varrer a física TODO
    // FRAME para manter o escudo aceso — e com 70 escudeiros em campo isso derrubava o jogo para
    // 3 FPS. Com validade em tempo, a aura pode ser reavaliada algumas vezes por segundo e o
    // escudo continua contínuo. A janela é curta de propósito: o que importa da mecânica é o
    // escudo sumir quando o escudeiro morre, e 0,3s depois é imperceptível.
    private const float ValidadeDaArmadura = 0.3f;
    private float armorUntil = -99f;

    private int EffectiveArmor => armor + (Time.time <= armorUntil ? borrowedArmor : 0);
    public bool IsShielded => Time.time <= armorUntil && borrowedArmor > 0;

    public void GrantTemporaryArmor(int amount)
    {
        // Expirou desde a última concessão: começa do zero, senão o valor de um escudeiro morto
        // continuaria servindo de piso para o próximo.
        if (Time.time > armorUntil) borrowedArmor = 0;

        armorUntil = Time.time + ValidadeDaArmadura;
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
    public bool BlastArmor => blastArmor;

    public void TakeDamage(int dmg, bool fromDetector = false, bool isSharp = false, bool ignoreArmor = false, bool isExplosive = false)
    {
        if (isDestroyed) return;
        if (isCamo && !fromDetector) return;  // camuflado: só detectores acertam
        if (leadArmor && isSharp) return;      // chumbo: imune a dardos/tachinhas (só explosão/energia/sniper)
        if (blastArmor && isExplosive) return; // cerâmica: a explosão da Bomba não racha o corpo cozido (dardo, napalm e Bombardeio continuam furando)

        if (IsMarked) dmg = Mathf.RoundToInt(dmg * markMultiplier); // alvo marcado recebe dano amplificado

        int reduction = ignoreArmor ? 0 : EffectiveArmor;
        hitPoints -= Mathf.Max(1, dmg - reduction); // armadura reduz, mas sempre tira ao menos 1

        if (sr != null)
        {
            // Clarear para branco só piscava quando o sprite base era um placeholder branco tingido.
            // Os UFOs já vêm com a cor própria no PNG — sr.color=branco não muda nada visualmente.
            // Multiplicar por >1 estoura o brilho por cima de qualquer cor (funciona em Linear/URP).
            sr.color = normalColor * 1.8f;
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

            // A recompensa encolhe quando a onda vem inflada (ver EnemySpawner.FatorDeRecompensa).
            // Sem isso, o adversário financiava o jogador: quanto mais unidades ele comprava, mais
            // rico ficava quem estava se defendendo. O mínimo de 1 existe para nenhuma morte valer
            // zero — inimigo que não paga nada deixa de ser alvo interessante.
            int premio = currencyWorth;
            if (EnemySpawner.main != null)
                premio = Mathf.Max(1, Mathf.RoundToInt(currencyWorth * EnemySpawner.main.FatorDeRecompensa));

            // Mesma guarda do EnemyMovement, mesma razão: uma bala em voo pode acertar depois de o
            // LevelManager ter sido destruído, e aí cada acerto lançava exceção. Com dez torres
            // atirando isso vira centenas de exceções por segundo.
            if (LevelManager.main != null) LevelManager.main.IncreaseCurrency(premio);

            // Juice: estouro + dinheiro flutuante
            AudioManager.Cue(AudioManager.Sfx.Pop);
            DeathPop.Spawn(GetComponent<SpriteRenderer>());
            FloatingText.Spawn(transform.position, "+" + premio, new Color(1f, 0.9f, 0.3f), transform.localScale.x);

            Destroy(gameObject);
        }
    }

}