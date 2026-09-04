using UnityEngine;

// Integridade estrutural de uma torre: quanto castigo ela aguenta antes de ser destruída.
//
// POR QUE ISTO EXISTE. O poder do jogador era permanente: uma torre comprada na rodada 10
// continuava rendendo na 40 sem nenhuma decisão nova, enquanto a ameaça só crescia em
// quantidade. Poder acumulado (integral) contra ameaça instantânea faz a folga crescer sozinha
// — medido entre 10x e 47x. Se a torre pode cair, o acumulado para de crescer sozinho e a
// defesa passa a precisar ser defendida: posição e cobertura mútua viram decisões reais.
//
// AS TRÊS REGRAS DA PERDA, que valem mais que qualquer número aqui:
//
//   1. TELEGRAFADA. A torre escurece e racha progressivamente, e o Sabotador que a ataca é
//      visível e matável. Ninguém perde uma torre sem ter visto vir.
//   2. EVITÁVEL. Matar o Sabotador interrompe o dano na hora, e a integridade se recupera
//      sozinha fora de combate. Perder é consequência de ignorar, não de azar.
//   3. NUNCA ALEATÓRIA. Dano fixo por golpe, sem sorteio.
//
// E a perda é de POSIÇÃO E TEMPO, não do dinheiro todo: a torre destruída devolve parte do
// investimento como sucata (ver SucataAoCair).
public class TowerIntegrity : MonoBehaviour
{
    // Integridade proporcional ao investimento: uma torre cara aguenta mais que uma básica.
    // Sem isto, derrubar a peça central da defesa custaria o mesmo que derrubar a mais barata,
    // e o jogador seria punido justamente por concentrar investimento — o oposto do que uma
    // trilha de upgrade profunda pede.
    [SerializeField] private float integridadePorDolar = 0.30f;
    [SerializeField] private int integridadeMinima = 90;

    // Fora de combate ela se recupera: uma sabotagem sobrevivida não deixa cicatriz permanente,
    // senão o desgaste viraria uma segunda economia invisível que o jogador não pode administrar.
    [SerializeField] private float regeneracaoPorSegundo = 6f;
    [SerializeField] private float esperaParaRegenerar = 4f;

    [SerializeField, Range(0f, 1f)] private float sucataAoCair = 0.5f;

    private int maxima;
    private float atual;
    private float ultimoDano = -999f;
    private TowerBase torre;
    private TowerValue valor;

    public float Fracao { get { Garantir(); return maxima > 0 ? Mathf.Clamp01(atual / maxima) : 1f; } }
    public bool Ferida => Fracao < 0.999f;

    private bool pronto;

    private void Start() => Garantir();

    // Inicialização sob demanda em vez de só no Start.
    //
    // O componente é adicionado em runtime pelo Plot no instante em que a torre nasce, e Start
    // só roda no frame seguinte. Qualquer dano no mesmo frame pegaria maxima=0 e sumiria em
    // silêncio — que é a assinatura dos bugs deste projeto. Com isto, a ordem de execução deixa
    // de importar.
    private void Garantir()
    {
        if (pronto) return;
        pronto = true;
        torre = GetComponentInParent<TowerBase>();
        valor = GetComponentInParent<TowerValue>();
        Recalcular();
        atual = maxima;
    }

    // O investimento muda a cada upgrade, então a integridade máxima acompanha.
    private void Recalcular()
    {
        int investido = valor != null ? valor.Invested : 0;
        int novo = Mathf.Max(integridadeMinima, Mathf.RoundToInt(investido * integridadePorDolar));
        if (novo == maxima) return;
        float fracao = maxima > 0 ? atual / maxima : 1f;
        maxima = novo;
        atual = maxima * fracao; // sobe proporcional: comprar upgrade não cura nem fere
    }

    private void Update()
    {
        Garantir();
        Recalcular();
        if (atual >= maxima) return;
        if (Time.time - ultimoDano < esperaParaRegenerar) return;

        atual = Mathf.Min(maxima, atual + regeneracaoPorSegundo * Time.deltaTime);
        AtualizarAparencia();
    }

    public void Danificar(int quanto)
    {
        Garantir();
        if (quanto <= 0 || maxima <= 0) return;
        ultimoDano = Time.time;
        atual -= quanto;
        AtualizarAparencia();

        if (atual <= 0f) Cair();
    }

    private void AtualizarAparencia()
    {
        if (torre == null) return;
        // Reaproveita o mesmo canal visual da sabotagem (ver TowerBase.ShowDisabledTint): a
        // torre vai escurecendo conforme perde integridade. Um canal só para "esta torre está
        // sofrendo" é mais legível que dois efeitos disputando a mesma silhueta.
        torre.SetIntegrityTint(Fracao);
    }

    private void Cair()
    {
        int sucata = 0;
        if (valor != null)
        {
            sucata = Mathf.RoundToInt(valor.Invested * sucataAoCair);
            if (LevelManager.main != null && sucata > 0) LevelManager.main.IncreaseCurrency(sucata);
        }

        FloatingText.Spawn(transform.position, "DESTRUÍDA  +$" + sucata, new Color(1f, 0.5f, 0.3f));
        AudioManager.Cue(AudioManager.Sfx.Explosion);

        // Some pelo mesmo caminho da venda: libera o plot e sai do registro do BuildManager,
        // senão a torre morta continuaria contando como defesa para o DefenseReadout.
        if (valor != null) valor.Demolir();
        else Destroy(gameObject);
    }
}
