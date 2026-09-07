using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Uma melhoria (tier) de uma trilha de upgrade — estilo Bloons.
public class UpgradeTier
{
    public readonly string title;
    public readonly string description;
    public readonly int cost;
    public readonly float damageMult;
    public readonly float rangeMult;
    public readonly float rateMult;
    public readonly float scaleMult;
    public readonly bool grantsCamo;

    // Habilidade destravada por este tier (ex: "pushback", "mark"). É o que permite um upgrade
    // MUDAR o que a torre faz em vez de só aumentar números — a base dos tiers transformadores.
    public readonly string ability;

    // Cor aplicada à torre ao comprar o tier: a evolução tem que ser visível no tabuleiro.
    public readonly Color? tint;

    public UpgradeTier(string title, string description, int cost,
        float damageMult = 1f, float rangeMult = 1f, float rateMult = 1f, float scaleMult = 1f, bool grantsCamo = false,
        string ability = null, Color? tint = null)
    {
        this.title = title;
        this.description = description;
        this.cost = cost;
        this.damageMult = damageMult;
        this.rangeMult = rangeMult;
        this.rateMult = rateMult;
        this.scaleMult = scaleMult;
        this.grantsCamo = grantsCamo;
        this.ability = ability;
        this.tint = tint;
    }
}

// Classe-base de todas as torres. Sistema de upgrade com 2 trilhas, tiers nomeados,
// multiplicadores acumulados, evolução visual e stats. Subclasses só definem mira/tiro e as trilhas.
public abstract class TowerBase : MonoBehaviour, IUpgradable, IHasRange
{
    [Header("Alcance & Detecção")]
    [SerializeField] protected LayerMask enemyMask;
    [SerializeField] protected float targetingRange = 5f;
    [SerializeField] protected bool canSeeCamo = false;

    [Header("Alcance visual")]
    [SerializeField] protected Color rangeColor = new Color(0.35f, 0.8f, 1f, 0.55f);

    protected float targetingRangeBase;

    // Trilhas de upgrade (definidas pela subclasse)
    private readonly List<UpgradeTier> pathA = new List<UpgradeTier>();
    private readonly List<UpgradeTier> pathB = new List<UpgradeTier>();
    private int levelA, levelB;

    // Habilidades destravadas pelos tiers comprados (ver UpgradeTier.ability).
    private readonly HashSet<string> abilities = new HashSet<string>();

    // Regra de crosspath (estilo Bloons "5-2-0"): só UMA trilha pode passar deste teto de tiers;
    // a outra fica travada nele. Com 3 tiers por trilha, isso vira "3-2" (uma fecha, a outra para no 2).
    private const int CrosspathCap = 2;

    // Multiplicadores vindos dos tiers comprados
    private float upgradeDamage = 1f, upgradeRange = 1f, upgradeRate = 1f;

    // Buffs externos (torres de suporte, sinergia de vizinhança). Ficam separados dos upgrades e
    // EXPIRAM sozinhos: valem só enquanto a fonte continuar reaplicando a cada frame. Assim, vender
    // a torre de suporte remove o bônus na hora, sem ninguém precisar avisar as vizinhas.
    private float buffDamage = 1f, buffRange = 1f, buffRate = 1f;
    private bool camoFromSupport;
    private int buffFrame = -99;

    // Tolerância de 1 frame para não depender da ordem de execução entre a fonte e o alvo do buff.
    private bool BuffValid => buffFrame >= Time.frameCount - 1;

    // O que as subclasses leem — upgrade e buff combinados.
    // FATOR GLOBAL DE DANO — a alavanca do lado do jogador.
    //
    // Medido: com 10 torres em tier 3, multiplicar o orçamento do adversário por 50 não fazia um
    // inimigo passar de 10% do traçado. O teto de vida que cabe numa onda é estrutural (310
    // vagas, teto de 40% por tipo, inimigo mais gordo com 200 de vida), então nenhuma calibragem
    // do lado da ameaça alcança o poder de fogo. A folga é de ordem de magnitude, não de ajuste.
    //
    // Estático e público para poder ser varrido em runtime durante a calibragem; o valor final
    // fica aqui como default documentado.
    //
    // 0,6 é um PRIMEIRO CORTE, não um número fechado: a varredura mostrou que reduzir o dano
    // sozinho não resolve (nem a 0,12 — oito vezes menos dano — a ameaça passava de 16% do
    // traçado), então este eixo age junto com a vida por rodada, não no lugar dela. Cortar mais
    // que isso de uma vez tornaria as rodadas iniciais penosas antes de a vida escalada existir.
    public static float FatorGlobalDeDano = 0.6f;

    // ───────── Registro de torres vivas ─────────
    //
    // `FindObjectsOfType<TowerBase>()` varre a cena INTEIRA e aloca um array a cada chamada. Ele
    // estava sendo chamado TODO FRAME pelo SynergyManager, e a cada sabotagem por cada Sabotador
    // em campo (havia 116 numa onda). Medido: o jogo cai de 112 FPS com 0 inimigos para 3 FPS com
    // 57, com picos de frame de até 8,3 SEGUNDOS.
    //
    // Uma lista mantida por OnEnable/OnDisable custa zero e responde na hora. Quem precisa de
    // "todas as torres" usa isto; quem precisa uma vez por rodada pode continuar com Find.
    private static readonly List<TowerBase> vivas = new List<TowerBase>();
    public static IReadOnlyList<TowerBase> Todas => vivas;

    protected virtual void OnEnable() { if (!vivas.Contains(this)) vivas.Add(this); }
    protected virtual void OnDisable() { vivas.Remove(this); }

    public float DamageMult => upgradeDamage * (BuffValid ? buffDamage : 1f) * FatorGlobalDeDano;
    public float RangeMult => upgradeRange * (BuffValid ? buffRange : 1f);
    public float RateMult => upgradeRate * (BuffValid ? buffRate : 1f);

    public bool IsBuffed => BuffValid && (buffDamage > 1.001f || buffRate > 1.001f || buffRange > 1.001f);

    // Aplicado por torres de suporte, todo frame.
    public void ApplyBuff(float damage = 1f, float rate = 1f, float range = 1f)
    {
        if (buffFrame != Time.frameCount) // primeiro buff do frame: recomeça a coleta
        {
            buffFrame = Time.frameCount;
            buffDamage = buffRate = buffRange = 1f;
            camoFromSupport = false;
        }
        buffDamage = Mathf.Max(buffDamage, damage); // buffs não empilham: vale o mais forte
        buffRate = Mathf.Max(buffRate, rate);
        buffRange = Mathf.Max(buffRange, range);
    }

    // Detecção de camuflados emprestada por uma torre de suporte.
    public void GrantCamoDetection()
    {
        ApplyBuff(); // marca o frame e reinicia a coleta se necessário
        camoFromSupport = true;
    }

    // O que as subclasses devem consultar: detecção própria (comprada) OU emprestada.
    protected bool SeesCamo => canSeeCamo || (BuffValid && camoFromSupport);

    private void RefreshRange() => targetingRange = targetingRangeBase * RangeMult;

    private float fireTimer;
    private bool isSelected;
    private RangeIndicator rangeVisual;
    private Vector3 baseLocalScale;

    // Se a torre tiver uma pilha de blocos (ver TowerStack), a evolução visual já é a ALTURA da
    // pilha — deixar o scaleMult acumulado dos tiers agir por cima ficaria redundante (compra dois
    // tiers e a torre cresce em bloco E em escala) e exagerado (blocos maiores destoando do
    // tamanho fixo da célula). Torres sem TowerStack continuam no comportamento antigo.
    private TowerStack stack;

    protected virtual void Awake()
    {
        targetingRangeBase = targetingRange;
        baseLocalScale = transform.localScale;
        stack = GetComponent<TowerStack>();

        // Guarda as cores originais para poder restaurá-las depois de uma sabotagem. Por
        // REFERÊNCIA, não por índice: os blocos da pilha (TowerStack) nascem depois deste Awake
        // (só no primeiro LateUpdate — ver TowerStack.Rebuild), então um array paralelo por
        // posição desalinha assim que a pilha aparece (índice N deixa de ser o mesmo renderer).
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Dictionary<SpriteRenderer, Color>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++) originalColors[renderers[i]] = renderers[i].color;

        DefinePaths(pathA, pathB);
        Recalculate();
    }

    private Dictionary<SpriteRenderer, Color> originalColors;
    private bool tierTinted; // true quando um tier comprado já definiu a cor da torre

    protected virtual void Update()
    {
        RefreshRange(); // o alcance depende de buffs que expiram por frame

        if (IsDisabled) return; // sabotada: não mira nem atira (a cor é resolvida em AtualizarCor)

        Tick();

        fireTimer += Time.deltaTime;
        float interval = FireInterval();
        if (fireTimer >= interval)
        {
            if (TryFire())
            {
                fireTimer = 0f;
                AudioManager.Cue(AudioManager.Sfx.Shoot, 0.35f); // baixo: muitas torres atiram juntas
            }
            else fireTimer = interval;
        }
    }

    // ───────── Sabotagem ─────────
    private float disabledUntil;

    public bool IsDisabled => Time.time < disabledUntil;

    // Desliga a torre por alguns segundos (inimigos sabotadores). Não acumula: vale o prazo mais longo.
    public void Disable(float seconds)
    {
        disabledUntil = Mathf.Max(disabledUntil, Time.time + seconds);
    }

    // Avermelha a torre conforme ela perde integridade estrutural (ver TowerIntegrity). É o
    // "telegrafado" da regra da perda: dá para ver de longe qual torre está prestes a cair.
    // Aqui só REGISTRA o valor; quem pinta é AtualizarCor.
    public void SetIntegrityTint(float fracao) => fracaoIntegridade = Mathf.Clamp01(fracao);

    // ───────── Cor da torre, num lugar só ─────────
    //
    // POR QUE CENTRALIZADO. Três coisas querem pintar a torre: o tint do tier comprado, o vermelho
    // de integridade perdida e o cinza de sabotagem. Antes cada uma escrevia `sr.color` no seu
    // próprio ponto do frame, uma desfazendo a outra — que é a receita de PISCAR. Medido frame a
    // frame: a torre alternava entre branco e (0.35, 0.35, 0.40) várias vezes por onda, porque a
    // sabotagem acabava, o LateUpdate restaurava a cor original, e o sabotador seguinte
    // redesligava logo depois. Cada escrita estava certa sozinha; o conjunto cintilava.
    //
    // Agora existe uma única passada, no fim do frame, que combina os três em ordem fixa. E a
    // sabotagem entra por TRANSIÇÃO, não por corte: ligar e desligar a cor de uma vez é o que o
    // olho lê como falha, mesmo quando a mecânica está correta.
    private const float TransicaoDeSabotagem = 0.30f; // segundos para acender/apagar o cinza

    // TETO DE VARIAÇÃO POR FRAME, e ele é o que faz a suavização funcionar de verdade.
    //
    // Só dividir por deltaTime não basta: com a onda cheia o jogo cai de 125 para menos de 10
    // FPS, e uma transição de tempo fixo passa a caber em dois frames — vira corte de novo.
    // Medido: mesmo depois de suavizar, o degrau de brilho entre frames ainda batia em 0,396,
    // bem acima do ~0,15 em que o olho lê piscada. Com o teto, a transição gasta pelo menos
    // ~9 frames aconteça o que acontecer com a taxa de quadros.
    private const float PassoMaximoPorFrame = 0.11f;

    private Color corDoTier = Color.white;
    private float fracaoIntegridade = 1f;
    private float pesoSabotagem;          // 0 = normal, 1 = totalmente apagada
    private SpriteRenderer[] cacheDeRenderers;

    // O TowerStack cria e destrói os blocos da pilha a cada tier comprado, então a lista de
    // sprites muda embaixo daqui. Ele avisa por este método (mesma lição do cache de ordenação
    // do IsoSorter: quem muda a LISTA invalida o cache).
    public void InvalidarCacheDeCor() => cacheDeRenderers = null;

    private void LateUpdate()
    {
        float passo = Mathf.Min(Time.deltaTime / TransicaoDeSabotagem, PassoMaximoPorFrame);
        pesoSabotagem = Mathf.MoveTowards(pesoSabotagem, IsDisabled ? 1f : 0f, passo);
        AtualizarCor();
    }

    private void AtualizarCor()
    {
        if (originalColors == null) return;
        if (cacheDeRenderers == null) cacheDeRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < cacheDeRenderers.Length; i++)
        {
            SpriteRenderer sr = cacheDeRenderers[i];
            if (sr == null) { cacheDeRenderers = null; return; } // pilha remontada: recaptura já
            if (sr.GetComponent<RangeIndicator>() != null) continue; // o anel tem cor própria

            // 1) cor base. O bloco da pilha nasceu depois do Awake, então não está em
            //    originalColors — e o tint de tier o ignora de propósito, para não lavar as cores
            //    do pacote (ver ApplyTint). A base dele é branco puro.
            Color cor;
            if (sr.GetComponent<TowerStackBlock>() != null) cor = Color.white;
            else if (tierTinted) cor = corDoTier;
            else
            {
                Color original;
                cor = originalColors.TryGetValue(sr, out original) ? original : Color.white;
            }

            // 2) integridade perdida avermelha.
            if (fracaoIntegridade < 0.999f)
                cor = Color.Lerp(new Color(0.95f, 0.30f, 0.25f), cor, fracaoIntegridade);

            // 3) sabotagem apaga por cima — a torre desligada precisa gritar isso primeiro,
            //    porque a resposta do jogador é outra (matar o sabotador, não defender a torre).
            if (pesoSabotagem > 0.001f)
                cor = Color.Lerp(cor, new Color(0.35f, 0.35f, 0.4f), pesoSabotagem);

            // O alpha é de quem cuida de oclusão (ver TowerOverlapFade), não daqui.
            Color atual = sr.color;
            cor.a = atual.a;
            if (atual != cor) sr.color = cor;
        }
    }

    // ───────── Hooks das subclasses ─────────
    protected virtual void DefinePaths(List<UpgradeTier> pathA, List<UpgradeTier> pathB) {}
    protected virtual void Tick() {}
    protected abstract float FireInterval();
    protected abstract bool TryFire();

    // Stats mostrados no painel (subclasse detalha dano/cadência).
    public virtual string GetStatsText() => $"Alcance {targetingRange:0.0}";

    // ───────── Trilhas / upgrade ─────────
    public int PathCount(int path) => (path == 0 ? pathA : pathB).Count;
    public int PathLevel(int path) => path == 0 ? levelA : levelB;

    public UpgradeTier NextTier(int path)
    {
        List<UpgradeTier> list = path == 0 ? pathA : pathB;
        int lvl = path == 0 ? levelA : levelB;
        return lvl < list.Count ? list[lvl] : null;
    }

    // True se o próximo tier desta trilha está bloqueado pela regra de crosspath
    // (a OUTRA trilha já passou do teto). Bloqueio estrutural — diferente de "sem dinheiro".
    public bool IsPathLocked(int path)
    {
        int next = PathLevel(path) + 1;
        int other = PathLevel(path == 0 ? 1 : 0);
        return next > CrosspathCap && other > CrosspathCap;
    }

    public void UpgradePath(int path)
    {
        UpgradeTier t = NextTier(path);
        if (t == null) return;
        if (IsPathLocked(path)) return; // regra de crosspath

        // O terceiro degrau é o salto de identidade da torre e fica atrás de nível de comandante
        // (ver Unlocks). PathLevel é 0-based, então o próximo tier é PathLevel+1.
        if (!Unlocks.TierLiberado(PathLevel(path) + 1))
        {
            FloatingText.Spawn(transform.position, "Nível " + Unlocks.NivelParaTierMaximo + " para liberar",
                new Color(1f, 0.45f, 0.45f));
            return;
        }

        if (LevelManager.main == null || t.cost > LevelManager.main.currency) return;

        LevelManager.main.SpendCurrency(t.cost);
        GetComponentInParent<TowerValue>()?.AddInvestment(t.cost);
        AudioManager.Cue(AudioManager.Sfx.Upgrade);

        if (path == 0) levelA++; else levelB++;
        if (t.grantsCamo) canSeeCamo = true;

        Recalculate();

        if (isSelected)
        {
            ShowRange();
            if (UIManager.main != null) UIManager.main.UpdateUpgradeUI();
        }
    }

    private void Recalculate()
    {
        upgradeDamage = 1f; upgradeRange = 1f; upgradeRate = 1f;
        abilities.Clear();
        float scale = 1f;
        Color? tint = null;

        for (int i = 0; i < levelA; i++) Accumulate(pathA[i], ref scale, ref tint);
        for (int i = 0; i < levelB; i++) Accumulate(pathB[i], ref scale, ref tint);

        RefreshRange();
        // Com pilha de blocos, a altura já conta a evolução — trava em (1,1,1) puro, IGNORANDO
        // baseLocalScale (ver o comentário no campo "stack"). Alguns prefabs antigos (Ice, Tesla,
        // Tachinha, Bomb) vieram de primitivas de debug do Unity com escala não-1 — inclusive
        // NÃO-UNIFORME (ex: Bomb Turret em 0.6871x0.612) — pra fazer o placeholder parecer do
        // tamanho certo. Os blocos novos são filhos desse mesmo transform e já nascem no tamanho
        // certo em unidades de mundo (ver TowerStack), então herdar essa escala old iria
        // distorcer/encolher os sprites do pacote. Sem pilha, mantém o comportamento antigo.
        transform.localScale = stack != null ? Vector3.one : baseLocalScale * scale;

        tierTinted = tint.HasValue;
        if (tint.HasValue) ApplyTint(tint.Value);
    }

    private void Accumulate(UpgradeTier t, ref float scale, ref Color? tint)
    {
        upgradeDamage *= t.damageMult;
        upgradeRange *= t.rangeMult;
        upgradeRate *= t.rateMult;
        scale *= t.scaleMult;
        if (!string.IsNullOrEmpty(t.ability)) abilities.Add(t.ability);
        if (t.tint.HasValue) tint = t.tint; // vale o tier mais recente
    }

    // Os tiers já comprados, na ordem. A UI usa isto para mostrar o que a torre FAZ hoje:
    // as descrições dos upgrades existiam desde sempre no código e nunca chegavam ao jogador.
    public List<UpgradeTier> PurchasedTiers()
    {
        List<UpgradeTier> list = new List<UpgradeTier>();
        for (int i = 0; i < levelA; i++) list.Add(pathA[i]);
        for (int i = 0; i < levelB; i++) list.Add(pathB[i]);
        return list;
    }

    // True se algum tier comprado destravou esta habilidade.
    public bool HasAbility(string id) => abilities.Contains(id);

    // Decisão: o tint de tier NÃO pinta os blocos da pilha (ver TowerStack). Empilhar blocos já
    // coloridos do pacote e depois lavar tudo com uma cor só apagaria a diferença entre os temas
    // (fortaleza larga vs. torre fina) que é o ponto inteiro do empilhamento. O tint continua
    // valendo só como um realce na ARMA (o SpriteRenderer de "Gun") — um brilho de "tier alto",
    // não uma repintura da torre inteira.
    // Só registra: a pintura acontece em AtualizarCor, que combina este tint com integridade e
    // sabotagem. Escrever aqui direto era um dos lados da disputa que fazia a torre piscar.
    private void ApplyTint(Color c) => corDoTier = c;

    // ───────── Mira compartilhada ─────────
    protected Transform AcquireTarget(TargetingPriority priority)
        => Targeting.FindTarget(transform.position, targetingRange, enemyMask, priority, SeesCamo);

    public bool CanSeeCamo => SeesCamo;
    public float CurrentRange => targetingRange;

    // ───────── Clique ─────────
    private void OnMouseDown() => ToggleUpgradeUI();

    public void ToggleUpgradeUI()
    {
        if (UIManager.main != null && UIManager.main.IsHoveringUI()) return;
        if (isSelected) CloseUpgradeUI();
        else OpenUpgradeUI();
    }

    public void OpenUpgradeUI()
    {
        isSelected = true;
        ShowRange();
        if (UIManager.main != null) UIManager.main.ShowUpgradeUI(this);
    }

    public void CloseUpgradeUI()
    {
        isSelected = false;
        HideRange();
        if (UIManager.main != null) UIManager.main.HideUpgradeUI();
    }

    // Chamado pelo UIManager ao fechar o painel (ex: botão Fechar): reseta seleção e esconde o alcance.
    public void MarkDeselected()
    {
        isSelected = false;
        HideRange();
    }

    // ───────── IHasRange ─────────
    public virtual void ShowRange()
    {
        if (rangeVisual == null) rangeVisual = RangeIndicator.Create(transform);
        rangeVisual.Show(targetingRange, rangeColor);
    }

    public void HideRange()
    {
        if (rangeVisual != null) rangeVisual.Hide();
    }

    // ───────── IUpgradable (compat; a UI usa a API de trilhas acima) ─────────
    public int GetCurrentLevel() => levelA + levelB;
    // Máximo REALMENTE alcançável sob o crosspath: fechar uma trilha + o teto na outra.
    public int GetMaxLevel()
    {
        int a = pathA.Count, b = pathB.Count;
        return Mathf.Max(a + Mathf.Min(b, CrosspathCap), b + Mathf.Min(a, CrosspathCap));
    }
    public int CalculateNextCost()
    {
        UpgradeTier a = NextTier(0), b = NextTier(1);
        if (a == null) return b?.cost ?? 0;
        if (b == null) return a.cost;
        return Mathf.Min(a.cost, b.cost);
    }
    public void Upgrade() => UpgradePath(NextTier(0) != null ? 0 : 1);
    public virtual string GetUpgradeDescription() => "";

    public Sprite GetIcon()
    {
        // Com pilha, o bloco mais alto é arte de verdade do pacote — melhor ícone que o
        // placeholder do Unity (Triangle/Square/Capsule) que sobrava embaixo dele (ver TowerStack).
        if (stack != null)
        {
            Sprite top = stack.TopBlockSprite();
            if (top != null) return top;
        }
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.sprite : null;
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        Handles.color = new Color(rangeColor.r, rangeColor.g, rangeColor.b, 0.7f);
        Handles.DrawWireDisc(transform.position, Vector3.forward, targetingRange);
    }
#endif
}
