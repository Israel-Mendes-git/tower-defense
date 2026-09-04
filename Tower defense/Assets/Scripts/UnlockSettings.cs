using UnityEngine;

// Interruptor de desenvolvimento para o acesso gradual (ver Unlocks.cs).
//
// Ligado: todas as torres, tiers, poderes e aliados ficam disponíveis, independentemente do
// nível de comandante. Serve para testar o jogo inteiro sem precisar subir de nível e SEM
// mexer no save real — o progresso em PlayerPrefs continua intocado, então dá para alternar
// entre "jogar como jogador novo" e "testar com tudo aberto" à vontade.
//
// [DefaultExecutionOrder(-200)]: precisa valer ANTES de qualquer loja, torre ou UI consultar
// o Unlocks. O IsoBoard usa -150 e o StageLoader -100, então -200 fica na frente dos dois.
[DefaultExecutionOrder(-200)]
public class UnlockSettings : MonoBehaviour
{
    [Header("Desenvolvimento")]
    [Tooltip("Libera todo o conteúdo, ignorando o nível de comandante. Não altera o save.")]
    [SerializeField] private bool liberarTudo = false;

    private void Awake() => Aplicar();
    private void OnEnable() => Aplicar();

    // Permite ligar e desligar com o jogo rodando, direto no inspetor.
    private void OnValidate() => Aplicar();

    private void Aplicar() => Unlocks.LiberarTudo = liberarTudo;

    // Sem isto, o valor estático sobreviveria a uma saída do Play Mode e contaminaria a
    // próxima sessão com "tudo liberado" mesmo com o interruptor desligado — o tipo de estado
    // fantasma que faz um teste mentir.
    private void OnDisable() => Unlocks.LiberarTudo = false;
}
