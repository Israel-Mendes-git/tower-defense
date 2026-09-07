using System.Collections.Generic;
using UnityEngine;

// Sabotador — inimigo que ATACA A SUA DEFESA em vez de só andar até o fim.
// Ao passar perto, desliga a torre por alguns segundos. Obriga o jogador a matá-lo com prioridade,
// não a simplesmente ter DPS suficiente: ignorá-lo abre um buraco na linha bem onde ele passou.
//
// Ele é também o EXECUTOR das jogadas dirigidas do adversário (ver CommanderPlays). Sozinho ele é
// pressão reativa — ataca o que estiver por perto. Sob uma jogada, ele passa a ter ordem: caça a
// torre marcada mesmo de longe, ou espalha o estrago dentro da área cercada. É a mesma criatura;
// o que muda é que agora existe uma decisão anunciada por trás dela.
public class Saboteur : MonoBehaviour
{
    [SerializeField] private float sabotageRadius = 1.6f;
    [SerializeField] private float disableSeconds = 5f;
    [SerializeField] private float interval = 3f;   // tempo entre sabotagens
    [SerializeField] private int danoEstrutural = 25; // corrói a torre; ver TowerIntegrity

    private float nextSabotage;

    private void Update()
    {
        if (Time.time < nextSabotage) return;
        nextSabotage = Time.time + interval;

        CommanderPlays jogadas = CommanderPlays.main;

        // ── CERCO: dentro da área anunciada, a sabotagem vira dano de ÁREA ──
        // Fora dela nada muda. É a punição direta ao amontoamento: empilhar seis torres no mesmo
        // ponto continua sendo forte, mas deixa de ser de graça — e o jogador foi avisado onde.
        if (jogadas != null && jogadas.NoCerco(transform.position))
        {
            var atingidas = new List<TowerBase>();
            // Registro em vez de varrer a cena: com mais de cem sabotadores em campo, cada um
            // chamando FindObjectsOfType a cada 3s, isso sozinho já custava frames.
            foreach (TowerBase t in TowerBase.Todas)
            {
                if (t.IsDisabled) continue;
                if (IsoGrid.CellDistance(t.transform.position, transform.position) <= sabotageRadius)
                    atingidas.Add(t);
            }
            if (atingidas.Count == 0) return;
            foreach (TowerBase t in atingidas) Sabotar(t, danoEstrutural);
            return;
        }

        TowerBase alvo = null;
        float melhor = float.MaxValue;
        float multiplicador = 1f;

        // ── MARCAÇÃO: ele tem ordem de ir atrás de UMA torre ──
        // O raio de caça é maior que o de sabotagem de propósito: é isso que faz ele passar batido
        // por torres no caminho para chegar na que foi anunciada. O jogador tem a onda inteira
        // para defender aquele ponto — e sabe exatamente qual é.
        if (jogadas != null && jogadas.AlvoMarcado != null)
        {
            TowerBase marcada = jogadas.AlvoMarcado;
            if (!marcada.IsDisabled
                && IsoGrid.CellDistance(marcada.transform.position, transform.position) <= jogadas.RaioDeCaca)
            {
                alvo = marcada;
                multiplicador = jogadas.MultiplicadorNoAlvo;
            }
        }

        // Sem ordem (ou com a torre marcada fora de alcance): sabota a torre ativa mais próxima —
        // a que estava efetivamente atrapalhando a passagem.
        if (alvo == null)
        {
            // Registro em vez de varrer a cena: com mais de cem sabotadores em campo, cada um
            // chamando FindObjectsOfType a cada 3s, isso sozinho já custava frames.
            foreach (TowerBase t in TowerBase.Todas)
            {
                if (t.IsDisabled) continue;
                float d = IsoGrid.CellDistance(t.transform.position, transform.position);
                if (d <= sabotageRadius && d < melhor) { melhor = d; alvo = t; }
            }
        }

        if (alvo == null) return;
        Sabotar(alvo, Mathf.RoundToInt(danoEstrutural * multiplicador));
    }

    private void Sabotar(TowerBase alvo, int dano)
    {
        alvo.Disable(disableSeconds);

        // Além de desligar, corrói a estrutura (ver TowerIntegrity). É isto que torna a torre
        // PERDÍVEL e, com ela, o poder acumulado deixa de ser permanente. Continua obedecendo às
        // três regras da perda: o dano é visível na cor da torre, é fixo (nunca sorteado), e matar
        // este Sabotador interrompe tudo — a integridade se recupera sozinha depois.
        TowerIntegrity estrutura = alvo.GetComponentInParent<TowerIntegrity>();
        if (estrutura == null) estrutura = alvo.GetComponentInChildren<TowerIntegrity>();
        if (estrutura != null) estrutura.Danificar(dano);

        FloatingText.Spawn(alvo.transform.position, "SABOTADA!", new Color(1f, 0.4f, 0.9f));
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, sabotageRadius);
    }
#endif
}
