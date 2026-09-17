# Tower Defense

Tower defense isométrico em que **o adversário responde à defesa que você construiu**.

A ameaça não é uma lista de ondas escrita de antemão: um Contra-Comandante lê o tabuleiro
a cada rodada e compõe o ataque contra o que você montou. Se a sua defesa tem um buraco,
ele encontra.

**Unity 2022.3.44f1 · 2D URP · C#**

## O jogo

Tabuleiro isométrico com nove torres, cada uma com um verbo próprio — nenhuma é "a mesma
torre com mais dano". As torres crescem em altura conforme sobem de tier, há aliados
móveis, sinergia por adjacência e poderes ativos. Cinco fases e meta-progressão entre
partidas.

Os quinze inimigos são UFOs, com dois eixos legíveis de longe: **cor** indica o tipo de
ameaça (cinco delas) e **porte** indica a massa (três). Dá para ler uma onda inteira sem
parar para conferir ícone.

O jogo é jogável do início ao fim e vencível.

## O Contra-Comandante

A parte que dá identidade ao projeto:

| Peça | Papel |
|---|---|
| `DefenseReadout.cs` | O olho — mede a defesa construída |
| `CounterCommander.cs` | O cérebro — decide a composição da ameaça |
| `CommanderPlays.cs` | As jogadas dirigidas contra pontos fracos |
| `PlaytestLogger.cs` | Telemetria de cada partida, em `Playtests/*.csv` |

Dois interruptores existem de propósito: `modoSeco` no `CounterCommander` devolve o
adversário à mera observação, e `fatiaDeJogadas` em zero o reduz a só compor a onda. Serve
para medir o quanto cada camada pesa no resultado, em vez de opinar sobre isso.

## Balanceamento medido, não sentido

As decisões de ajuste vêm de partida registrada em CSV, não de impressão. Um exemplo do
que isso pega: com os blocos de torre apenas encostando, uma torre de tier alto chegava a
4,58 unidades de altura e escondia **29% do traçado** — e a comparação com uma rodada de
torres todas no tier 0 (14%) mostrou que mais da metade da oclusão vinha da altura, não da
quantidade de torres. Daí o encaixe de 0,5 entre blocos.

`ROADMAP.md` guarda as medições e o histórico; `PLANO.md`, as decisões travadas e as
armadilhas do terreno.

## Como rodar

1. Abra a pasta `Tower defense/` no **Unity 2022.3.44f1**.
2. Carregue a cena `MainMenu` em `Assets/Scenes`.
3. Play.

As cenas de trabalho são `MainMenu`, `StageSelect` e `Game`.
