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
- **Altura de torre: blocos se ENCAIXAM, não se empilham** (`TowerStack.Encaixe = 0,5`).
  Medido em 2026-09-04 contra uma partida de 29 rodadas: com os blocos só encostando,
  uma torre de tier alto chegava a 4,58 unidades e escondia **29% do traçado** — e a
  comparação com a rodada 6 (mesmas torres, todas tier 0, 14%) mostra que **mais da
  metade da oclusão vinha da altura, não da quantidade de torres**. Com encaixe 0,5 a
  altura cai para 2,93 e o traçado escondido para 18%, sem distorcer a arte e sem
  perder a leitura de poder (tier 0 = 1,64, tier 5 = 3,01). Tabela completa da
  varredura no comentário do `TowerStack`.
- **Torre que tapa torre cede passagem** (`TowerOverlapFade`, no LevelManager). Plots
  vizinhos em profundidade ficam a 1,0 unidade e a torre tem ~2,9 de altura: quando
  duas caem alinhadas, a da frente engole dois terços da de trás e as duas viram uma
  coluna só com duas armas. **Altura não resolve isto** — medido, encolher a torre em
  16% tirou só 8 pontos do pior caso (66% → 58%); zerar exigiria a torre caber em 1,0.
  A saída é de apresentação: a da frente vai a alpha 0,72. Dois testes decidem quem
  cede — área coberta ≥ 25% **e** alinhamento horizontal ≥ 55%. O segundo é essencial:
  só com área, 5 de 10 torres ficavam translúcidas, porque a torre é alta e fina e duas
  vizinhas cruzam área só por estarem perto. Com os dois, 2 de 12 — exatamente as que
  tapam de verdade.
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

### Fase D — Jogadas na onda e perda de torre — FEITO (2026-09-04)

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

**Jogadas dirigidas: prontas (2026-09-04).** `CommanderPlays.cs`, anexado ao
LevelManager. O Sabotador comprado era pressão *reativa*; agora existe decisão
anunciada. Uma fatia do orçamento (`fatiaDeJogadas`, 25%) deixa de comprar volume e
passa a comprar intenção — uma onda que marca a sua Sniper é uma onda **menor**.

| Jogada | Contra o quê | O que muda na onda |
|---|---|---|
| **MARCAÇÃO** | o pilar | elege a torre de maior investimento; sabotadores a caçam num raio de 5 células e dobram o dano estrutural nela |
| **CERCO** | o amontoado | elege o ponto mais denso do traçado; ali dentro a sabotagem vira dano de **área** em vez de pegar só a mais próxima |
| **REFORÇO** | a atenção | guarda ~30% da composição e injeta a 55% dos spawns, anunciado antes e na hora |

As três regras da perda valem inteiras: telegrafada (manchete antes + anel pulsante
no tabuleiro, `MarcadorDeJogada.cs`), evitável (o executor é sempre um Sabotador
visível, e matá-lo interrompe), nunca aleatória (alvo por critério declarado, nunca
por sorteio). A regra de ouro também: marcação exigiria defender aquele ponto, então
ela é **barrada** se nenhuma torre que atira alcança a torre marcada; cerco exige
amontoado real; e nada disso antes de o roteiro ter apresentado o Sabotador.

Duas decisões que a medição forçou:

- **Os executores são comprados com orçamento, não em número fixo.** O Sabotador tem
  12 de vida: dois deles numa rodada 20 morrem antes de chegar ao alvo, e a jogada
  anunciada não acontece — anúncio sem consequência ensina o jogador a ignorar o
  anúncio. Agora vêm entre 2 e 5, conforme o caixa.
- **O reforço é aparado, não descartado.** Ele é a jogada mais cara e chega por
  último na fila da eficácia; tudo-ou-nada fazia dele algo que quase nunca aconteceria.
  Metade de uma marcação não marca nada, mas uma leva menor continua sendo uma leva.

O CSV do `PlaytestLogger` ganhou a coluna `jogada_do_adversario`, amarrada ao número
da rodada — sem ela não dá para separar "a rodada 22 dói" de "a rodada 22 dói quando
ele marca a sua torre principal", que pedem correções opostas.

### Correções da varredura de 2026-09-05

Vieram de um CSV de partida real (`playtest-20260904-040839`, serpente, nível 1), não de leitura
de código.

- **O teto de sabotadores matava a Fase D inteira.** Ele comparava o teto com o total da onda, e
  contra defesa amontoada a composição compra dezenas de sabotadores — então "já tem 55, teto 5"
  barrava até o REFORÇO, que não usa sabotador nenhum. Medido: nove rodadas do Contra-Comandante
  na partida dele, **zero jogadas dirigidas**. Agora o teto só se aplica a quem ACRESCENTA
  sabotador, e a jogada passou a ser uma ORDEM em vez de uma compra: se a onda já traz executor,
  marcar custa só o prêmio da direção. Provado: `REFORÇO` + `MARCAÇÃO` acontecendo, com 45
  inimigos injetados no meio da onda.
- **A composição não fazia pergunta nenhuma.** Ordenar por eficácia pura consertou o vício antigo
  (lixo barato ganhando por ser barato) e criou o primo dele: na rodada 24 ele comprava 55
  Sabotadores, 33 Ladrões e 30 Escudeiros e **descartava o Chumbo por faltarem $2** — a única
  resposta ali que o jogador teria de responder. Aquela onda de 118 unidades causou 0 de dano,
  0 torres desligadas e deu **+$1.060 de lucro** ao jogador. Agora quem tem imunidade
  (camo/chumbo/cerâmica) **pergunta** e compra primeiro, dentro de um teto próprio
  (`tetoDasPerguntas`, 65%); o resto é tempero e disputa o que sobra. Depois: `96x Lead, 37x
  Shielder, 69x Saboteur, 4x Thief`.
- **Abandonar a partida dava 0 XP.** `RecordRun` só rodava na morte ou vitória, e uma partida
  completa passa de 10 minutos. Medido: 29 rodadas jogadas, nenhum XP, comandante ainda no nível
  1 — e o nível 1 tranca o tier 3 e cinco das nove torres, então ele voltava para a mesma partida
  sem perigo e com dinheiro sem destino ($55.674 parados). Agora cada rodada sobrevivida credita
  na hora (`PlayerProgress.CreditRound`); o fim de partida paga só o bônus de vitória.
- **Defaults do `EnemySpawner` estavam desatualizados** em relação à cena (`difScalingFactor`
  0,75 vs 1, `enemiesPerSecond` 0,5 vs 1, `waveCompletionBonus` 100 vs 35). Não quebrava a cena
  atual — mas faria qualquer fase nova nascer com a curva antiga.

### A torneira de dinheiro — FECHADA (2026-09-05)

Partida humana completa (vitória, 144/150) rendeu **$160.951**, contra os **$62k** que o ROADMAP
previa para 40 rodadas. Medindo ganho por rodada contra o tamanho da onda, o culpado ficou óbvio
— e **não era o Contra-Comandante**, que era a suspeita inicial:

| rodadas | quem monta | $/inimigo |
|---|---|---|
| 18,19,21,24,26,28,29,31,… | Contra-Comandante | ~$8, estável |
| 25,27,30,33,35,38,40 | WaveScript (roteirizadas) | $23 a **$143** |

**Causa:** `Group.Escalavel` era só `count > 3`, e o fator de escala é
`(tamanhoDaOnda − fixos) ÷ escaláveis`. Como a curva de contagem é LINEAR na rodada (8 × rodada),
o fator chegava a **10,2** na rodada 40 — os 6 chefes escritos no roteiro viravam 61. A regra foi
escrita quando a onda era menor e envelheceu junto com `difScalingFactor` indo a 1.

**Correção:** Boss e MOAB nunca escalam (`Marcante`), e o fator ganhou teto de 6×
(`TetoDeEscalonamento`). Medido depois: receita final **$69.497** — dentro do previsto. As rodadas
comuns seguem em ~$8,8/inimigo, intocadas.

| rodada | antes | depois |
|---|---|---|
| 27 | $14.886 | $3.530 |
| 30 | $34.345 | $6.065 |
| 40 | $28.173 | $11.835 |

Junto entrou uma trava de **recompensa sublinear no tamanho da onda** (`EnemySpawner`,
expoente 0,5): impede que inflar a contagem gere renda. Funciona (medido: fator 0,894 numa onda
25% acima da curva), mas tem efeito pequeno no problema real — o teto de contagem já limitava a
inflação a 1,25×. Fica como trava, não como solução.

**O que NÃO mudou:** a partida seguinte foi vencida com **150/150 de vida, dano zero**. Fechar a
torneira era condição para calibrar, não a calibragem.

### Performance — RESOLVIDA (2026-09-07). A causa era FÍSICA, não código.

O jogo trava com onda cheia. Medido no Editor, com 10 torres:

| inimigos | FPS |
|---|---|
| 0 | 110–120 |
| 25 | ~45 |
| 43–50 | **1–5** |

Mais picos isolados de frame de **até 13 segundos**. O profiler aponta a causa: **655 MB de heap
gerenciado** e **33.219 alocações num único frame**. A renderização está saudável (290 draw calls,
176 SetPass) — não é gargalo de desenho, é coletor de lixo.

**Corrigido (tudo verificável por leitura, e cada um reduz alocação ou varredura):**

- `TowerBase.Todas` — registro de torres vivas por `OnEnable/OnDisable`. Elimina
  `FindObjectsOfType<TowerBase>()` do `SynergyManager` (era TODO FRAME), do `Saboteur` (era por
  sabotador, e havia 116 numa onda) e do `TowerOverlapFade`. Com `Object Count` em ~13.000, cada
  uma dessas varreduras custava caro.
- `Targeting.FindTarget` → `OverlapCircleNonAlloc`. É o caminho mais quente do jogo: por torre,
  por frame, com centenas de colliders dentro. Junto veio `Targeting.Overlap`, usado agora por
  Ice, Tesla, Tachinha, Detector e SpikeField.
- `Shielder` — `NonAlloc` + aura a 8 Hz em vez de 60. Exigiu mudar a armadura emprestada de
  validade por FRAME para validade por TEMPO (`Health.ValidadeDaArmadura`), senão o escudo
  piscaria entre as reavaliações.
- `SynergyManager` — de todo frame para 5 Hz. Sinergia é geometria entre torres, e torre não se
  move.
- `FloatingText` e `DeathPop` — teto por frame (8 e 12). Uma explosão que mata cinquenta inimigos
  criava cinquenta GameObjects com TextMeshPro no mesmo quadro.

**A CAUSA REAL ERA OUTRA, e as cinco otimizações acima não a tocavam.**

`EnemyMovement.Update` lançava **NullReferenceException por inimigo, por frame**: `LevelManager.main`
já destruído, e `main.path` num objeto morto lança NRE (o "fake null" do Unity). Cinquenta inimigos
em campo = cinquenta exceções por quadro, cada uma montando stack trace e alocando. O mesmo em
`Health.TakeDamage`, disparado por cada bala que acertava.

Corrigido com guarda de null nos dois pontos. Medido: **33.219 → 533 alocações por frame**, e
2,4 MB → 105 KB.

**A lição que fica: antes de otimizar, ler o console.** O perfil de alocação de uma exceção
repetida é indistinguível do de código mal escrito — o profiler dizia "GC" e estava certo, mas a
origem não era o código que eu estava otimizando. Cinco rodadas de otimização legítima não moveram
o ponteiro porque atacavam o lugar errado.

#### A causa real: os inimigos colidiam entre si (2026-09-07)

Duas hipóteses da sessão anterior caíram, e as duas custaram tempo por serem plausíveis:

- **O heap de ~600 MB não era resíduo de nada.** Medido num Editor recém-aberto: 593 MB com o
  jogo PARADO, antes de qualquer Play. É o baseline do Editor. O heap do jogo oscila 578–656 MB
  e volta — ele coleta e devolve, não vaza.
- **Metade do travamento era a própria bancada.** O `AutoTeste` roda com `Time.timeScale = 12`, e
  timeScale alto não só não acelera quando o FPS cai: ele MULTIPLICA o custo do frame, porque o
  `FixedUpdate` roda até `maximumDeltaTime / fixedDeltaTime` = **16,7 vezes por quadro**. Medido
  na mesma cena, mesmas torres, mudando só o timeScale: **34 inimigos a 2 FPS viravam 35 inimigos
  a 87 FPS**. O "trava com 25 inimigos" nunca existiu.

**A causa verdadeira estava no `Physics2D`.** Com um probe que separa o frame em script / física /
render (`PerfProbe`), a resposta veio numa linha: com 100 inimigos e 10 torres, o frame era de
**9292 ms, dos quais 8440 ms eram física** — e o script, 2,8 ms. Os "20 MB alocados por frame"
que o profiler atribuía ao GC eram contatos de física, não lixo do jogo.

Dois defeitos de configuração, ambos invisíveis no código:

| | era | virou | efeito |
|---|---|---|---|
| matriz de colisão 2D | `Enemy × Enemy` colidindo | ignorado | física 8440 ms → ~5 ms |
| `collisionDetectionMode` | `Continuous` nos 19 prefabs | `Discrete` nos 15 inimigos | física ~5 ms → **0,1 ms** |

Os inimigos andam em FILA pelo mesmo traçado — ficam colados por design —, e cada par colidindo
gerava contato. `Continuous` ainda fazia sweep test em cada um. **Nenhum código do jogo reagia a
essa colisão**: só `Bullet`, `AoEBullet` e `TachinhaBullet` têm handler de colisão. Eram 8
segundos por frame produzindo exatamente zero efeito de jogo.

As balas continuam em `Continuous` de propósito: são rápidas, e `Discrete` arriscaria tunneling.
São poucas em campo, então não pesam.

**Depois (10 torres, `timeScale` 1, Editor):**

| inimigos | 50 | 100 | 200 | 400 | 800 |
|---|---|---|---|---|---|
| FPS | 90 | 87 | 78 | 59–72 | 27 |
| física (ms) | 0,1 | 0,2 | 0,3 | 0,7–1,3 | 8,3–10,7 |

Com 100 inimigos: de **0 FPS para 87**. O jogo agora aguenta 400 inimigos acima de 60 FPS, e as
ondas reais não passam disso.

**A correção de código que sobrou** foi pequena e do mesmo padrão já conhecido:
`DetectorTurret.LendDetectionToNeighbours` fazia `FindObjectsOfType<TowerBase>()` no `Tick`, ou
seja todo frame e por Detector — o último sobrevivente do padrão que já tinha saído do
`SynergyManager`, do `Saboteur` e do `TowerOverlapFade`. Passou a ler `TowerBase.Todas`.

**Pooling de projéteis deixou de ser prioridade.** Era o próximo alvo do plano antigo; com o
script custando 1 ms de um frame de 11 ms, não há o que ganhar ali.

### Fase E — Calibragem com jogo real  `← PRÓXIMA`

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

- **Antes de otimizar, DIVIDIR O FRAME — script, física, render.** Duas sessões seguidas foram
  gastas otimizando código de jogo enquanto a causa estava fora dele: primeiro uma exceção por
  frame, depois a física. Nas duas vezes o profiler dizia "GC" e estava certo sobre o sintoma e
  inútil sobre a origem. `PerfProbe` responde isso em uma linha; rodá-lo é o primeiro passo, não
  o último. Com 100 inimigos o script custava 2,8 ms de um frame de 9292 ms — nenhuma otimização
  de código jamais moveria aquele ponteiro.
- **Configuração de física não aparece em code review.** A colisão `Enemy × Enemy` e o
  `Continuous` dos prefabs não estavam em nenhum arquivo `.cs`: estavam na matriz de layers do
  ProjectSettings e num enum do prefab. Custaram 8 segundos por frame sem produzir efeito nenhum
  de jogo. Ao caçar performance, olhar a cena e as settings, não só o código.
- **`Time.timeScale` alto FALSIFICA medição de performance.** Ele não acelera o jogo quando o FPS
  está baixo (o `maximumDeltaTime` capa o passo) e ainda multiplica o custo do quadro, porque o
  `FixedUpdate` roda até 16,7 vezes nele. Medir performance é sempre a `timeScale = 1`.

- **O prefab vence o default do script.** Mudar `[SerializeField] private int x = 5`
  não muda nada se o prefab serializa outro valor. Um commit inteiro de balance já
  foi perdido assim. Balance se aplica no prefab **e** no default.
- **Fiação morta é o defeito característico daqui.** Código que existe e nunca
  chega ao jogador já apareceu mais de dez vezes. Toda entrega termina provando em
  Play Mode que a mudança chega à tela.
- **Constante de ordenação é suspeita.** O chão isométrico vai de -200 a 2200; todo
  `sortingOrder` fixo escrito antes da conversão estava enterrado.
- **Uma propriedade visual, um dono.** A cor da torre era escrita por três sistemas em pontos
  diferentes do frame (tint de tier, vermelho de integridade, cinza de sabotagem), cada um
  desfazendo o outro — e isso é o que o olho lê como PISCAR, mesmo com cada escrita correta
  sozinha. Medido frame a frame: degrau de brilho de **0,644** entre frames consecutivos
  (branco ↔ cinza de sabotagem). Agora existe um `AtualizarCor` único, no fim do frame, que
  combina os três em ordem fixa. Corrigido em 2026-09-04.
- **Transição por tempo não basta: precisa de teto POR FRAME.** Suavizar a sabotagem em 0,18 s
  parecia resolver, e não resolvia — com a onda cheia o jogo cai de 125 para menos de 10 FPS e a
  transição passa a caber em dois frames, virando corte de novo (degrau ainda em 0,396). Com um
  teto de 0,11 de variação por frame, o degrau caiu para **0,139**, abaixo do ~0,15 em que o
  olho percebe. Sempre limitar a variação por frame, não só por segundo.
- **Trocar de seleção precisa deselecionar a anterior.** `UIManager.ShowUpgradeUI` sobrescrevia a
  referência da torre selecionada sem avisar a antiga, e o anel de alcance dela ficava preso na
  tela para sempre. O `BuildManager` não salvava: ele dá `return` assim que o raycast acerta algo
  com `IHasRange`, então o caminho de "clique no vazio" nunca rodava.
- **Cache invalida quando a LISTA muda, não só quando o valor muda.** O `IsoSorter`
  guardava `lastOrder` para não repintar a cada frame, e isso anulava a reanexação
  que o `TowerStack` faz de propósito ao montar a pilha: os renderers novos eram
  capturados, mas o `Apply` voltava no early-return porque a torre não tinha se
  movido. Resultado: **as pilhas das torres ficaram invisíveis, enterradas sob o
  tabuleiro** — só apareciam as da borda do mapa, que não têm tile na frente. O
  código da reanexação existia, com comentário explicando por que era necessário, e
  não fazia nada. Achado em 2026-09-04.
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
- O cérebro: `Assets/Scripts/CounterCommander.cs` (`modoSeco` devolve à observação)
- As jogadas dirigidas: `Assets/Scripts/CommanderPlays.cs` (`fatiaDeJogadas` a zero,
  no CounterCommander, devolve ele a só composição)
- Telemetria de partida: `Assets/Scripts/PlaytestLogger.cs` → `Playtests/*.csv`
