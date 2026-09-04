# Plano — Tower Defense

Documento de andamento. Ponto de entrada de qualquer sessão nova: leia isto antes
de tocar em código. Os números medidos e o histórico de execução ficam em
`ROADMAP.md`; aqui está só o que guia decisão.

---

## Onde o projeto está

Unity 2022.3.44f1, 2D URP. Tabuleiro isométrico com torres que crescem em altura
por tier e 15 inimigos como UFOs (5 cores = eixo de ameaça, 3 portes = massa).
Nove torres com verbos próprios, aliados móveis, sinergia por adjacência, poderes
ativos, 5 fases, meta-progressão. Cinco cenas de trabalho: `MainMenu`,
`StageSelect`, `Game`.

O jogo é jogável do início ao fim. O que falta não é conteúdo — é **dificuldade
que faça perguntas**.

---

## Decisões travadas (não reabrir sem motivo novo)

- **Isométrico** com ordem de desenho por célula. Camadas: `Default` = mundo,
  `Turrets` = sobreposição informativa (anel, sinergia), `UI` = texto.
- **Alcance medido em CÉLULAS**, não em unidades de mundo. Já implementado nos 10
  pontos que medem alcance.
- **Recompensa por inimigo** segue `3 + 0,55 × HP^0,85`, com +30% para quem exige
  resposta específica. Receita das 40 rodadas: $62k.
- **A dificuldade NÃO vem de HP exponencial** (o caminho do Bloons). Vem de um
  adversário que reage à defesa construída — ver o problema central abaixo.
- **A ameaça pode reagir ao jogador. O jogador quer ser exigido durante a onda.
  Torre pode ser perdida.** As três respostas que destravam o desenho.
- **O tema nasce do adversário**, não é escolhido antes. Foi adiado de propósito
  desde 2026-08-07, quando nove propostas de tema decorativo foram recusadas.

---

## O problema central

O poder do jogador é a **integral** do dinheiro: uma torre comprada na rodada 10
segue rendendo na 40 sem nenhuma decisão nova. A ameaça é **instantânea**: o que
vem naquela rodada. Integral contra instantâneo faz a folga crescer sozinha —
medido, entre 10x e 47x mesmo depois de cortar a economia em 4,2x.

O Bloons resolve empurrando o instantâneo para cima com HP exponencial. Custo
disso: a pergunta de toda rodada continua sendo "tenho DPS?", e é por isso que as
torres parecem todas iguais — contra HP puro, elas são.

**Duas saídas honestas, e este projeto usa as duas:** a ameaça também vira
integral (orçamento indexado ao poder acumulado) e o poder deixa de ser permanente
(torre pode cair).

### O sistema, em três eixos

1. **Intensidade indexada ao poder.** Orçamento do adversário =
   `base + k × investido^0,7`. Expoente sublinear: ficar mais forte sempre
   compensa, só não trivializa.
2. **Vocabulário por anulação.** O orçamento compra composição, nunca gordura.
   Cortante demais → chumbo. Explosão demais → cerâmica. Sem detecção → camo.
   Defesa amontoada → sabotador com escudeiro.
3. **Exigência por jogadas telegrafadas.** Parte do orçamento age durante a onda:
   área marcada para sabotagem, reforço injetado, torre cara eleita como alvo.

**REGRA DE OURO:** ele só pode fazer perguntas que o jogador tem como responder.
Cobrar resposta a camo de quem não tem acesso a detecção é armadilha, não
dificuldade. Por isso o olho mede o espaço de resposta, não só a defesa montada.

**REGRA DA PERDA:** toda ameaça a uma torre é telegrafada, evitável e nunca
aleatória; torre destruída devolve parte do investimento como sucata. A perda é de
posição e tempo, não do dinheiro todo.

---

## Fases

### Fase A — Cérebro em modo seco — FEITO (2026-09-03)

`CounterCommander.cs`, no LevelManager, disparado pelo `onWaveComplete`. Lê o
retrato do olho, decide o que compraria e **só relata**. Verificado: defesa 100%
cortante → compra chumbo; defesa 90% explosiva → cerâmica vira compra nº 1 e o
chumbo é descartado; jogador sem caixa → a cerâmica é barrada pela regra de ouro.

Dois defeitos corrigidos durante a validação, ambos dignos de nota:

- **Ordenar por custo-benefício estava errado.** Eficácia ÷ preço faz o barato
  ganhar sempre: contra defesa 69% explosiva ele comprava 23 Ladrões e o orçamento
  acabava antes da Cerâmica. Agora ordena por eficácia pura; o teto por tipo é
  quem impede a monocultura.
- **Opção que não cabia no orçamento sumia do relatório.** Virou linha explícita
  ("queria comprar, 90% de eficácia, mas sobraram $4") — informação que some é o
  vício deste projeto.

Achado: `Lead`, `Ceramic` e `MOAB` não estão em `enemyUnlocks` — existem só dentro
de rodadas roteirizadas (11, 17, 27), uma aparição cada no jogo inteiro. O cérebro
precisou varrer o `WaveScript` para enxergá-los. Quando ele ganhar poder (Fase C),
as imunidades passam a ser cobradas de verdade pela primeira vez.

### Fase B — Acesso gradual — FEITO (2026-09-03)

`Unlocks.cs`: tabela de nível de comandante por peça, com `ConfereRegraDeOuro()`
para validar a tabela sempre que ela mudar.

```
nv 1   Basic, Tachinha, Bomba, Detector + poder Bombardeio
nv 3   + Metralhadora, aliados
nv 5   + Gelo, poder Congelar
nv 6   + terceiro tier de todas as trilhas
nv 7   + Tesla
nv 10  + Sniper, poder Reparo
nv 12  + Gerador
```

Generoso na base e fiel à regra de ouro: camo chega na rodada 5 e o Detector está
no nível 1; chumbo na 11 e a Bomba está no nível 1; cerâmica na 17 e
Basic/Tachinha estão no nível 1. O Gerador fica por último de propósito — economia
nas mãos de quem ainda não sabe gastar só antecipa a bola de neve.

Ligado em cinco pontos: `BuildManager.ToggleBuildMode` (recusa e avisa),
`BuildManager.CatalogoResumido` (torre travada não conta como resposta
disponível), `TowerBase.UpgradePath` (terceiro tier), `AbilityManager.Use`,
`AllyManager.BeginRecruit`. Na loja, o botão travado mostra `Nv 7` no lugar do
preço e continua visível — ver o que ainda vem é metade do motivo de voltar.

**Fecha o ciclo com a Fase A**, verificado: nível 1, caixa $200, defesa 100%
cortante → o adversário enxerga a lacuna e **barra o chumbo**, porque a Bomba
custa $350 e Tesla e Sniper ainda estão travadas. Cobra o camo, que o Detector a
$120 resolve.


### Fase C — Dar poder ao adversário — FEITO (2026-09-03)

O cérebro monta a onda de verdade em `EnemySpawner.StartWave`, onde
`WaveScript.BuildQueue` devolve `null`. Rodadas roteirizadas seguem intocadas.
A manchete da HUD anuncia a intenção: *"Rodada 18 — ele mandou Camo — sua detecção
cobre 0% do traçado"*. Memória de 2 rodadas ligada (`memoriaEmRodadas`), e o
interruptor `modoSeco` no inspetor devolve ele à observação a qualquer momento.

**Duas correções de rumo que só a medição pegou:**

1. **Preço ∝ ameaça, não recompensa.** A primeira versão cobrava `CurrencyWorth`
   pela simetria bonita de "o que você ganha é o que ele pagou". Medido: a
   recompensa é sublinear em vida de propósito (ver 2.1), então 200 Enemies de 1
   de vida custavam o mesmo que 12 MOABs e entregavam 12x menos ameaça — ele
   enchia a onda de lixo barato por construção. Com preço = vida (+40% para quem
   exige resposta específica), o orçamento passa a ser literalmente *quanta vida
   ele coloca em campo*, e trazer counter significa trazer menos inimigos.
2. **Teto por tipo precisava valer em contagem, não só em orçamento.** Contra
   defesa sem detecção, o Camo cabia inteiro dentro dos 40% de orçamento e
   sozinho esgotava a onda: 180 camuflados e mais nada. A trava existia, na moeda
   errada. Agora a composição sai variada (72 Camo, 72 Chumbo, 27 Escudeiro, 9
   Sabotador), intercalada ao longo da fila.

Calibragem medida contra a curva normal: em vida total, a onda dele fica em
0,6–0,8x da rodada equivalente, com teto de contagem de 1,25x. Fica de propósito
um pouco abaixo em volume porque compensa em qualidade — o ajuste fino é da Fase E.

### Fase D — Jogadas na onda e perda de torre — FEITO em parte (2026-09-03)

**Perda de torre: pronta.** `TowerIntegrity.cs`, anexado pelo `Plot` junto do
`TowerValue` (não pelo prefab — assim vale para toda torre construída sem depender
de alguém lembrar). O Sabotador deixou de apenas desligar e passou a corroer:
25 de dano estrutural por golpe.

As três regras da perda, implementadas e verificadas:

- **Telegrafada** — a torre avermelha progressivamente conforme perde integridade;
  o Sabotador é visível e matável.
- **Evitável** — matar o Sabotador interrompe na hora, e a integridade regenera
  sozinha após 4s fora de combate.
- **Nunca aleatória** — dano fixo, sem sorteio.

A integridade é proporcional ao investimento (0,30 por dólar, mínimo 90), então
concentrar investimento compra resistência além de dano. Medido: torre básica de
$100 resiste a 3 golpes e cai no 4º (12s de aviso); Sniper de $1.000 aguenta 12.
Quem cai devolve 50% do investido como sucata — **a perda é de posição e tempo,
não do dinheiro todo**.

**Bug de robustez corrigido no caminho:** o componente é criado em runtime e
`Start` só roda no frame seguinte, então dano no mesmo frame pegava `maxima = 0` e
sumia em silêncio. Virou inicialização sob demanda.

**O que falta da Fase D:** as jogadas telegrafadas do adversário *dirigidas por
ele* — marcar uma área antes da onda, injetar reforço no meio, eleger a torre mais
cara como alvo. Hoje a pressão vem do Sabotador comprado, que é reativo mas não é
uma decisão anunciada. `← PRÓXIMA`

### Fase D — Jogadas na onda e perda de torre

Ações telegrafadas durante a rodada; torre pode ser destruída sob as regras da
perda.

**Pronto quando:** dá para perder uma torre e entender exatamente por que, e uma
partida inteira sem prestar atenção custa caro sem ser injusta.

### Fase E — Calibragem com jogo real

Playtest humano com o `PlaytestLogger` (já existe, já está fiado na cena, grava
CSV por rodada em `Playtests/`). Aqui também entra a recalibragem de $/DPS das
torres, que hoje está invertida — Sniper e Bomba são as mais poderosas E as mais
baratas por DPS, enquanto Basic e Metralhadora pioram a cada tier.

**Pronto quando:** o CSV mostra dinheiro sendo restrição real além da rodada 25, e
nenhuma torre é escolha obviamente dominante.

### Fase F — Tema e acabamento

O tema nasce do adversário e se aplica em nomes, loja, manchetes e menu — o texto
já tem onde morar (`UpgradeTier.description`, `WaveScript.Headline`). Acabamento:
sons reais (os campos de override já existem), mais tipos de aliado, onboarding
(nada hoje ensina camo, chumbo, armadura ou sinergia), ícones de loja.

---

## Regras deste projeto (cada uma custou tempo)

- **O prefab vence o default do script.** Mudar `[SerializeField] private int x = 5`
  não muda nada se o prefab serializa outro valor. Um commit inteiro de balance já
  foi perdido assim. Balance se aplica no prefab **e** no default.
- **Fiação morta é o defeito característico daqui.** Código que existe e nunca
  chega ao jogador já apareceu mais de dez vezes. Toda entrega termina provando em
  Play Mode que a mudança chega à tela.
- **Constante de ordenação é suspeita.** O chão isométrico vai de -200 a 2200; todo
  `sortingOrder` fixo escrito antes da conversão estava enterrado.
- **`runInBackground` tem que ser setado FORA do Play Mode**, via
  `PlayerSettings`. Dentro do Play ele funciona por um tempo e depois para de
  valer, e os testes passam a mentir.
- **`execute_code` compila em C# 6** (CodeDom): sem interpolação `$`, sem `?.` em
  objetos Unity. `Object` e `Random` são ambíguos — qualificar com `UnityEngine.`.
- **Nunca `??` com tipo do Unity.** O operador não respeita a sobrecarga de `==` e
  devolve "fake null" — já deixou 4 de 9 sons mudos. Usar `!= null`.
- **`TowerBase.UpgradePath` recusa em silêncio** se faltar dinheiro. Medir tiers
  sem encher o caixa antes produz resultado falso.
- **A cena ativa não é a que você acha.** Confirmar antes de editar ou salvar.

---

## Onde estão as coisas

- Números medidos, tabelas e histórico de execução: `ROADMAP.md`
- Agentes de projeto (autônomos, com o contexto embutido): `.claude/agents/`
- O olho: `Assets/Scripts/DefenseReadout.cs`, pendurado no LevelManager
- Telemetria de partida: `Assets/Scripts/PlaytestLogger.cs` → `Playtests/*.csv`
