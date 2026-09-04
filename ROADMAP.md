# Roadmap por etapas — Tower Defense

Estado em 2026-09-03. Cada etapa se divide em partes com escopo fechado: o que é,
onde mexe, os passos, o critério de "feito" e a armadilha já conhecida daquele
terreno. As armadilhas não são teoria — todas já custaram tempo neste projeto.

Convenção: os agentes citados são os de `.claude/agents/` deste repositório
(autônomos por design; escalam só decisão de rumo).

---

## Etapa 1 — Fechar a virada visual

O tabuleiro virou isométrico e as torres passaram a crescer em altura por tier,
mas os inimigos continuam sendo os sprites antigos. É a dissonância mais visível
que sobrou.

### 1.1 UFOs nos 15 inimigos — FEITO (`iso-builder`, 2026-09-03)

**Onde:** `Assets/Art/Prefabs/*Enemy*.prefab` + `Trojan Horse.prefab`;
`Assets/Isometric Tower defence pack/.../Sprites/UFO/` (5 PNG); `IsoSorter.cs`.

**Passos**

1. Mapear os 15 tipos em 5 cores × 3 portes. Cor = eixo de ameaça, porte = massa.
2. Aplicar sprite, escala coerente com o tile (2 × 1) e altura de voo por prefab.
3. `IsoSorter` com `moves:true` em cada um.
4. Conferir que layer, colliders e `enemyMask` (64) sobreviveram à edição.
5. Play Mode: percorrem o traçado, tomam dano, ordem de desenho certa.

**Feito quando:** os 15 prefabs usam UFO, console 0 erros / 0 warnings, e uma
rodada inteira roda sem sprite antigo em campo.

**Armadilha:** trocar ou re-adicionar componente no Unity zera campos
serializados (foi assim que `enemyMask` virou 0 ao migrar Detector e MachineGun).
A URP ignora `Camera.transparencySortAxis`; a ordenação válida é o eixo
customizado em `GraphicsSettings` (0.49, -1, 0.49), definido no commit 8ca61d92.

### 1.2 Marcadores dos traços que hoje se leem por tint — FEITO (`iso-builder`, 2026-09-03)

Cor e porte gastam os dois eixos disponíveis do UFO, mas os traços que exigem uma
resposta específica do jogador precisam continuar legíveis.

**Fechamento:** estáticos (badge de Lead/Armored, alfa do Camo, pulso do Regen) já
tinham saído numa rodada anterior. Nesta, os dinâmicos:
- `Health.IsShielded` tinha um bug de raiz: o blend de cor só era recalculado no
  instante em que um flash de dano expirava — um inimigo escudado sem apanhar
  ficava sem nenhum aviso, e um que perdesse o escudo bem depois do último flash
  continuava mostrando o azulado pra sempre. `Health.Update` agora reavalia todo
  frame fora da janela de flash. Confirmado lendo sobre UFO roxo, vermelho e cinza
  (comparação lado a lado, escudado vs. controle).
- `Health.IsMarked` não tinha NENHUM feedback — mecânica real, zero fiação até a
  tela. Criado `EnemyStatusFX.cs` (anexado centralmente pelo `EnemySpawner`, como
  o `IsoSorter`): anel vermelho pulsante acima do UFO enquanto marcado, testado em
  jogo real com o Detector marcando inimigos de verdade.
- Lento/congelado do Gelo (`EnemyMovement.UpdateSpeed`) também não tinha feedback.
  `EnemyMovement` ganhou `IsSlowed`/`IsFrozen` (leitura pura, nenhum número de
  jogo mudou) e `EnemyStatusFX` desenha um véu translúcido gelo por cima do corpo
  inteiro — canal diferente do blend de escudo, não colidem.
- Empurrão do Gelo (`PushBack`) não ganhou efeito de propósito: já é um evento
  instantâneo e visível (o inimigo anda pra trás na hora), não um estado contínuo.
- `TowerBase.Disable` (Saboteur): achado e corrigido um bug real na restauração.
  `ShowDisabledTint` já apagava a pilha inteira certo (não só a raiz — os blocos
  são filhos do mesmo transform da `TowerBase`), mas `RestoreOriginalColors` usava
  um array indexado por posição capturado no `Awake`, ANTES de os blocos da pilha
  existirem (`TowerStack` só monta no primeiro `LateUpdate`) — o índice desalinhava
  e os blocos ficavam cinza pra sempre depois da primeira sabotagem. Trocado por
  `Dictionary<SpriteRenderer, Color>` por referência, com caso explícito pra bloco
  de pilha (cor de fábrica é sempre branca, `ApplyTint` nunca a define). Verificado
  end-to-end: sabotar uma torre de 5 blocos apaga os 5, e eles voltam a branco
  puro quando a sabotagem expira.

**Traços estáticos:** `isCamo` (só torres com detecção acertam), `leadArmor`
(imune a dano cortante), `armor > 0`, regeneração.

**Traços dinâmicos (ligam e desligam em runtime):** `Health.IsShielded` (escudo do
Shielder, expira por frame), `Health.IsMarked` (marcado pelo Detector),
congelado/empurrado pelo Gelo, e os anti-torre em ação (Saboteur, Thief).

**Passos**

1. Fixar o vocabulário visual: cor = ameaça, porte = massa, badge = traço estático,
   efeito = estado dinâmico. Um traço nunca deve usar dois canais ao mesmo tempo.
2. Componente de badge pendurado no inimigo, lendo `Health` — sem tint que apague
   a identidade do UFO.
3. Estados dinâmicos com feedback que aparece e some junto com o estado.

**Feito quando:** dá para nomear o tipo e o traço de um inimigo em campo sem
clicar nele nem consultar a loja.

**Quem:** `iso-builder` com revisão de `td-ui`.

### 1.3 Coesão do que orbita o inimigo — FEITO (`iso-builder`, 2026-09-03)

Trocar o inimigo desalinha tudo que aparece junto dele.

**Onde:** `DeathPop.cs`, `FloatingText.cs`, `Bullet.cs`, `AoEBullet.cs`,
`SpikeField.cs`, `Ally.cs`.

**Passos**

1. Conferir `sortingOrder` de cada um contra a ordem que o `IsoSorter` produz.
2. Reescalar efeitos para a nova massa visual dos UFOs.
3. Conferir contraste do texto flutuante e da barra de vida sobre a paleta escura.

**Feito quando:** nenhum elemento parece "colado por cima" do tabuleiro numa
sequência de screenshots de uma rodada movimentada.

**Fechamento — achados por arquivo:**
- `Bullet.cs`/`AoEBullet.cs`/`TachinhaBullet.cs`: os 5 pontos onde uma torre
  instancia bala (`Turret`, `MachineGunTurret`, `SniperTurret`, `AoETurret`,
  `TachinhaTurret`) nunca chamavam `IsoSorter.Attach` — todo projétil vivia com o
  `m_SortingOrder: 1` fixo do prefab, abaixo de praticamente qualquer tile de chão
  (que começa em 0 e sobe por célula). Bala nascia e ficava atrás do tabuleiro o
  jogo inteiro. Confirmado por conta (célula que exigia ordem 650, prefab dava 1)
  e corrigido nos 5 pontos.
- `SpikeField.cs`: o mesmo defeito, pior — `sortingOrder = -1` fixo "porque fica
  sob os inimigos", mas o chão isométrico nunca é negativo (`(col+row)*100`), então
  -1 ficava embaixo do PRÓPRIO TERRENO: o campo de espinhos da Tachinha era
  invisível desde a conversão pra iso. Trocado por
  `IsoGrid.SortingOrderAt(posição, origin) - 1` (relativo à célula onde nasce, não
  mais um número fixo) — visível por cima do chão, por baixo de quem pisa nele.
- `Ally.cs`/`AllyManager.cs`: mesmo padrão de novo — `Ally Scout.prefab` tem
  `sortingOrder: 5` fixo e nunca recebia `IsoSorter.Attach` ao ser recrutado. Um
  aliado que patrulhasse longe da origem (o normal, é o trabalho dele) ficava
  atrás de quase todo o tabuleiro. Corrigido em `AllyManager.PlaceAt`.
- `DeathPop.cs`: sem bug — já copia sprite/cor/**escala** (`lossyScale`) do
  `SpriteRenderer` de origem no instante da morte, então o estouro já nasce do
  tamanho certo em qualquer porte (1.2 a 3.3), sem precisar de código novo.
- `FloatingText.cs`: dois problemas reais. (a) Nascia sempre 0.3 unidades acima da
  ORIGEM do inimigo — que agora é o "pé" do UFO (o pivô ficou mais baixo de
  propósito, ver 1.1, pra dar altura de voo), não o centro do corpo — então um
  texto de morte num MOAB (escala 3.3) nascia dentro/abaixo do próprio corpo.
  `Spawn` ganhou parâmetro opcional `scale` (default 1 = comportamento de sempre)
  que multiplica a subida; os pontos de chamada ligados a inimigo (`Health`,
  `Thief`, `Ally`) passam `transform.localScale.x`. (b) Sem contorno: texto claro
  (o "+$" verde-claro do Farm/Ladrão) sumia sobre a grama clara do tabuleiro —
  cor pensada pra a paleta escura da UI falhava bem no lugar onde a maioria destes
  textos nasce. `outlineWidth`/`outlineColor` fixos (contorno escuro) resolvem
  para qualquer fundo, claro ou escuro.
- Barra de vida: não existe barra de vida POR INIMIGO neste projeto (varredura
  confirmou — só o contador "Vida" do HUD do jogador, que é UI Canvas em
  screen-space, não afetado pela troca de sprite nem por `sortingOrder` de mundo).
  Nada a fazer aqui além de registrar que a checagem foi feita.
- Efeito colateral do flash de dano (fora da lista original, mas do mesmo
  problema): `Health.TakeDamage` clareava pra branco puro — funcionava com o
  placeholder branco antigo (branco×cor = clareia), mas branco×branco não muda
  nada sobre um UFO já colorido. Corrigido junto com a 1.1 (`normalColor * 1.8f`,
  estouro de brilho por multiplicação, funciona em qualquer cor).

### 1.4 Imunidade a explosão do Ceramic (`td-gameplay`)

Decidido pelo Israel em 2026-09-03. O Ceramic entrou na família cinza ("seu dano
rende menos") mas não tinha imunidade nenhuma — só 40 de HP. Ganha imunidade a
dano explosivo, espelhando o chumbo, que é imune a cortante. O par fica simétrico:
chumbo pede explosivo, cerâmica pede cortante.

**Onde:** `Health.cs` (campo novo + parâmetro em `TakeDamage`), `AoEBullet.cs`,
`Ceramic Enemy.prefab`, `EnemyTraitBadge` para o selo.

**As 8 fontes de dano do jogo, para decidir o que conta como explosão:**

| Fonte | Arquivo | Hoje |
|---|---|---|
| Dardo de torre | `Bullet.cs:102` | `isSharp` configurável |
| Tachinha | `TachinhaBullet.cs:29` | cortante |
| Campo de espinhos | `SpikeField.cs:66` | cortante |
| Napalm da Bomba | `AoEBullet.cs:108` → `SpikeField` | `isSharp:false`, "fogo queima até chumbo" |
| Explosão da Bomba | `AoEBullet.cs:99` e `:134` | sem marcação |
| Raio do Tesla | `TeslaTurret.cs:67` | energia |
| Aliado | `Ally.cs:130` | corpo a corpo |
| Bombardeio (poder) | `AbilityManager.cs:118` | ignora armadura |

**Passos**

1. `isExplosive` em `TakeDamage`, no mesmo padrão de `isSharp` — não inventar
   sistema novo de tipos de dano.
2. Marcar como explosão apenas `AoEBullet` (os dois pontos de dano).
3. O napalm continua ferindo cerâmica: é fogo, não explosão. Isso deixa a Bomba
   com saída pela própria trilha de upgrade em vez de virar inútil na rodada 17.
4. O Bombardeio é trunfo de emergência com 45s de recarga e já ignora armadura —
   deve continuar ferindo cerâmica.
5. Selo próprio no `Ceramic Enemy.prefab`, agora que a imunidade existe de fato.

**Feito quando:** bomba não tira vida do Ceramic, dardo e napalm tiram, e o selo
distingue Ceramic de Lead em campo.

**Impacto para a Etapa 2:** a Bomba perde eficácia da rodada 17 em diante. Entra
como variável na régua de $/DPS (2.4) e na revalidação da curva (2.3).

### 1.5 Varredura anti-fiação-morta — FEITO (2026-09-03)

**O padrão dominante confirmou-se de novo: constantes de ordenação da era top-down.**
Medido em Play Mode — o chão isométrico ordena por célula e vai de -200 a 2200;
numa célula mediana está em 1000. Qualquer valor fixo abaixo disso fica enterrado.

| Achado | Valor antigo | Consequência | Correção |
|---|---|---|---|
| `FloatingText` | `sortingOrder 200` | todo "+$", "-$" do Ladrão e "SABOTADA!" invisível fora da quina do mapa | camada `UI`, ordem 100 |
| `RangeIndicator` | `sortingOrder 100` | anel de alcance enterrado sob o tabuleiro | camada `Turrets`, ordem 10 |
| `SynergyManager` | `sortingOrder 50` | linhas de sinergia enterradas | camada `Turrets`, ordem 0 |
| 7 das 9 torres | sorting layer `Turrets` | layer vence ordem: essas torres desenhavam por cima de qualquer inimigo, anulando o `IsoSorter`. Ice e Tesla, no `Default`, obedeciam — ninguém escolhe isso para 7 de 9 | todas no `Default` |
| `Menu.OnGUI` | sem guarda | `NullReferenceException` por evento de IMGUI quando `LevelManager.main` não existe (domain reload) | virou `Update` com guardas |
| `BuildManager.SelectTower`/`DeselectTower` | sem chamador | segundo mecanismo morto para mostrar alcance; era ele que prendia o anel quando chamado à força | removidos |

**Regra de camadas que ficou estabelecida** (era implícita e inconsistente):
`Default` = mundo, com ordem por célula do `IsoSorter` · `Turrets` = sobreposição
informativa presa ao mundo (anel, linhas) · `UI` = texto que precisa ser sempre legível.

**Falsos positivos descartados com medição:** os 75 campos serializados nulos da
cena são 71 `Plot.towerObj` (plot vazio), 4 `AudioManager.*Override` (vazios por
design, ver 6.1) e `LevelManager.victoryPanel`, que tem fallback por
`transform.parent.Find("Victory Panel")` — testado em runtime, resolve certo.

**Verificado em Play Mode:** 10/10 inimigos com `EnemyStatusFX` e `IsoSorter`;
bala em voo com ordem dinâmica 1147; torre em 1550 sobre chão 1500; inimigos em
150/329/508 atualizando enquanto andam; manchetes das rodadas 11 e 17 corretas;
badges e `blastArmor` corretos nos 4 prefabs. Console 0 erros / 0 warnings.

O defeito característico deste projeto é código que existe e nunca chega ao
jogador — já aconteceu quatro vezes (`SetHoveringState` nunca chamada,
`UpgradeTier.description` nunca lida, dois botões órfãos).

**Passos**

1. Varrer campos `[SerializeField]` nulos na cena Game.
2. Varrer listeners de UI apontando para métodos inexistentes.
3. Varrer sprites/prefabs atribuídos mas nunca instanciados.

**Feito quando:** relatório sem achado crítico, ou achados corrigidos.

---

## Etapa 2 — Recalibrar sobre a geometria nova

Os números foram calibrados quando o tabuleiro era top-down e os plots distavam
~1 unidade. A célula isométrica tem 2 de largura por 1 de altura. Nada disso foi
remedido depois da conversão.

### 2.1 Régua de recompensa por inimigo — FEITO (2026-09-03)

**Régua escolhida pelo Israel: `3 + 0,55 × HP^0,85`, com +30% para quem exige
resposta específica** (camo, chumbo, cerâmica, armadura). Um MOAB passa a valer 17
grunts — o chefe continua sendo evento sem que o enxame deixe de pagar.

| Inimigo | HP | antes | agora |
|---|---|---|---|
| Enemy | 1 | 10 | 4 |
| Fast | 2 | 20 | 4 |
| Camo | 2 | 25 | 5 |
| Lead | 3 | 25 | 6 |
| Tank | 8 | 30 | 6 |
| 2Tank | 10 | 50 | 7 |
| Thief | 10 | 35 | 7 |
| Saboteur | 12 | 40 | 8 |
| Regen | 15 | 40 | 8 |
| Shielder | 22 | 55 | 11 |
| Armored | 25 | 45 | 15 |
| Trojan Horse | 30 | 60 | 13 |
| Ceramic | 40 | 80 | 20 |
| Boss | 80 | 150 | 34 |
| MOAB | 200 | 300 | 68 |

Receita acumulada nas 40 rodadas: **$261.272 → $62.065** (conferido rodando a
simulação contra as filas reais depois de aplicar). Curva nova: $67 na rodada 1,
$431 na 10, $864 na 20, $6.087 na 40.

**Ajuste de arrasto:** o Ladrão roubava $15 por tique numa economia que encolheu
4,2x, o que o tornaria 4x mais forte sem ninguém decidir isso. Foi para $4 — no
prefab **e** no default do script, para não repetir o erro da Farm abaixo.

**Bug encontrado no caminho (e corrigido):** o commit 42fc8d17 "Freia a bola de
neve dos juros da Farm" mudou `interestRate` 0.05 → 0.02 e `interestCap` 300 → 100
**apenas no default do script**. O valor serializado no prefab manda, e o prefab
tinha os valores antigos — o freio nunca chegou ao jogo. Regra que fica: mexer em
número de balance exige tocar o prefab, não o default do `[SerializeField]`.

**Motivação original:** a razão valor/HP variava 12x sem critério declarado:

| Inimigo | HP | $ | $/HP |
|---|---|---|---|
| Camo | 2 | 25 | 12,5 |
| Fast | 2 | 20 | 10,0 |
| Lead | 3 | 25 | 8,3 |
| Regen | 15 | 40 | 2,7 |
| Shielder | 22 | 55 | 2,5 |
| Ceramic | 40 | 80 | 2,0 |
| MOAB | 200 | 300 | 1,5 |

**Onde:** campo `currencyWorth` em `Health.cs`, serializado nos 15 prefabs.

**Passos**

1. Declarar a fórmula: base por HP + prêmio pelo traço que exige resposta
   específica (camo, chumbo, armadura) + prêmio de chefe.
2. Simular a receita por rodada usando as filas reais do `WaveScript`, não a média.
3. Aplicar nos prefabs e medir uma partida.

**Feito quando:** tabela dos 15 com $/HP dentro de uma faixa justificada e
projeção de receita por rodada de 1 a 40.

**Armadilha:** `currencyWorth` é também o valor que o `Thief` devolve ao morrer —
mexer nele mexe em duas mecânicas. E a árvore de trabalho desta sessão tinha
cinco inimigos valendo $0 por uma edição em massa não medida; foi revertido, mas
mostra que mexer em prefab passa despercebido se ninguém medir depois.

**Quem:** `td-balance` (mede antes de opinar).

### 2.2 Alcance em espaço de células — JÁ ESTAVA FEITO (verificado 2026-09-03)

**Esta parte do plano nasceu errada.** Eu a escrevi supondo que o alcance ainda
fosse um círculo no mundo (logo, elipse em células). Não é: a conversão já foi
feita e a decisão já tinha sido tomada pelo Israel. `IsoGrid.CellDistance` achata
cada eixo pelo tamanho da célula antes de medir, e o comentário no código diz
textualmente que "alcance 4" tem que ser 4 células para qualquer lado.

O padrão é `OverlapCircleAll(origin, IsoGrid.WorldRadiusFor(range))` como
pré-filtro barato, seguido de descarte fino por `CellDistance > range`.
**Verificado nos 10 pontos que medem alcance** — `Targeting`, `AoEBullet` (x2),
`DetectorTurret`, `IceTurret`, `TeslaTurret`, `TachinhaTurret` (x2), `SpikeField`,
`Shielder`, `Ally`: todos têm o corte fino. O `RangeIndicator` desenha a elipse
2:1 correspondente, que no chão isométrico lê como um círculo de células.

Alcances atuais, agora em CÉLULAS: Tachinha 2,8 · MachineGun 3,0 · Turret 3,5 ·
Tesla 4,0 · Bomba 4,5 · Detector 4,5 · Gelo 5,5 · Sniper 7,0.

**Sobra desta parte:** apenas conferir se esses 9 números fazem sentido como
cobertura de tabuleiro (entra em 2.4), e um detalhe cosmético de editor — o gizmo
`Handles.DrawWireDisc` em `TowerBase.cs:421` ainda desenha círculo de mundo, então
mente para quem inspeciona pelo editor, embora não afete o jogo.

### 2.3 Revalidar a curva — FEITO, e o veredito é estrutural (2026-09-03)

**O HP dos inimigos não escala por rodada.** `enemyHealthMultiplier` é definido uma
única vez por FASE em `ConfigureStage` e nunca mais muda (`Health.cs:34`). Um
Enemy da rodada 40 tem o mesmo 1 de vida que tinha na rodada 1. A ameaça cresce
só por quantidade (`8 × rodada`) e por mistura de tipos.

O poder do jogador, porém, cresce com o **acumulado** de receita — a integral de
uma receita crescente, ou seja, aproximadamente quadrático. Ameaça linear contra
poder quadrático: a folga cresce sozinha, e nenhuma calibragem de recompensa
resolve isso — só adia.

Medido depois de aplicar a régua nova (DPS comprável ÷ DPS necessário):

| Rodada | 4 | 8 | 12 | 20 | 28 | 36 | 40 |
|---|---|---|---|---|---|---|---|
| Folga | 17,7x | 6,0x | 44,0x | 47,7x | 23,1x | 33,9x | 10,3x |

Entre as rodadas 24 e 36 o HP total mal se mexe (1.932 → 2.898) enquanto o caixa
acumulado quase triplica. É aí que o jogo deixa de fazer perguntas.

**Isto é insumo direto da Etapa 4:** o Contra-Comandante ataca o sintoma certo
(composição que responde à defesa), mas se o HP continuar fixo por rodada ele vai
comprar ondas cada vez mais irrelevantes com o mesmo orçamento.

### 2.3b Parâmetros de referência (medidos)

Dois ajustes grandes entraram em 01/09 e nenhum foi medido numa partida completa:
juros da Farm 5% → 2% (teto $300 → $100) e `difScalingFactor` 0,85 → 1.

**Valores que valem hoje** (os da cena Game, que vencem os defaults do script):
`baseEnemies 8` · `enemiesPerSecond 1` · `difScalingFactor 1` ·
`enemiesPerSecondCap 15` · `waveCompletionBonus 35` · `maxRounds 40`.

**Feito quando:** CSV de uma partida de 1 a 40 com dano recebido e caixa por
rodada, e um veredito escrito sobre onde a curva afrouxa.

### 2.4 Régua de $/DPS por tier — MEDIDO (2026-09-03), recalibragem em aberto

$ por ponto de DPS, com o custo acumulado da torre + trilha A:

| Torre | base | A1 | A2 | A3 | tendência |
|---|---|---|---|---|---|
| Sniper | **17** | 14 | 14 | **12** | melhora |
| Bomba | 23 | 21 | 20 | **19** | melhora |
| Tesla | 31 | 30 | 30 | 25 | melhora pouco |
| Tachinha | 30 | 33 | 37 | 36 | estável |
| Basic Turret | 33 | 40 | 51 | **55** | **piora** |
| Metralhadora | 36 | 40 | 44 | **41** | **piora** |

**A régua está invertida.** Sniper (alcance global, ignora armadura) e Bomba (dano
em área, portanto DPS efetivo bem maior que o nominal medido aqui) são as duas
torres mais poderosas em função E as mais baratas por DPS E as que mais melhoram
com investimento. As duas torres básicas de alvo único ficam *piores* a cada tier:
subir o Basic Turret até A3 custa $790 para sair de 33 para 55 $/DPS.

Consequência para o jogador competente: encher o mapa de Sniper e Bomba domina, e
investir nas outras é armadilha. Isso alimenta o incômodo nº 1 do diagnóstico
original ("as torres são todas iguais") por outro caminho — elas não são iguais,
são desigualmente eficientes.

**Por que não recalibrei ainda:** o DPS aqui é NOMINAL (dano × cadência). Bomba e
Tachinha acertam vários inimigos por disparo, então o efetivo delas é um múltiplo
desconhecido do nominal. Mexer no preço da Bomba sem medir o efetivo em área seria
trocar um número errado por outro. A medição do efetivo precisa de alvos reais em
fila — é trabalho da Etapa 3, com o jogo rodando.

**Armadilha da medição (paguei para aprender):** `TowerBase.UpgradePath` recusa em
silêncio se faltar dinheiro (`t.cost > currency`). Medir DPS por tier sem encher o
caixa antes faz os tiers 2 e 3 falharem sem aviso e o resultado sugere, falsamente,
que os upgrades não fazem nada.

### 2.4b Régua antiga, para referência

A régua antiga era 3 DPS por $100, com $/DPS entre 17 e 36.

---

## Etapa 3 — Playtest humano instrumentado

A minha IA de teste constrói burramente (só perto do caminho) e ainda assim
chegou à rodada 26 perdendo 13% de vida. A suspeita é que para um jogador
competente o jogo seja fácil demais — mas suspeita não é medida.

### 3.1 Preparar a sessão

O `PlaytestLogger` já existe e já está fiado na cena Game. Grava
`Playtests/playtest-<data>.csv`, uma linha por rodada, com: `rodada`,
`vida_inicio`, `vida_fim`, `dano_recebido`, `dinheiro`, `torres`, `aliados`,
`nivel_medio`, `segundos`, `fase`, `nivel_comandante`, `composicao`,
`construiu_nesta_rodada`. É observador puro: se removido, o jogo se comporta igual.

**Passos:** confirmar `ativo = true` na cena; arquivar CSVs antigos; conferir que
a pasta `Playtests/` segue ignorada pelo git.

### 3.2 Protocolo da sessão

1. Três partidas em fases de espaço bem diferente: Vale (81 plots livres),
   Serpente (49), Labirinto (54).
2. Jogar para vencer, sem reiniciar rodada.
3. Anotar o momento exato em que algo incomodou — a anotação é o que o CSV não
   captura.

### 3.3 Leitura dos dados

Perguntas que o CSV responde sozinho:

- Em que rodada o dinheiro deixa de ser restrição (caixa cresce e você não gasta)?
- Quantas rodadas seguidas passam com `dano_recebido = 0`? Isso é o tédio medido.
- A `composicao` converge para as mesmas duas torres? Então as outras sete são decoração.
- `segundos` por rodada cresce? Rodada longa sem ameaça é o pior tipo de tédio.

### 3.4 Lista priorizada e correções curtas

Separar o que é conserto de número (volta para a Etapa 2) do que é falta de
decisão para o jogador tomar (vira insumo da Etapa 4).

**Feito quando:** lista priorizada escrita, com cada item apontando para a etapa
que o resolve.

---

## Etapa 4 — Contra-Comandante (redesenhada em 2026-09-03)

Depois do veredito da 2.3, esta etapa deixou de ser "um adversário que compõe
ondas" e virou **o sistema de dificuldade do jogo**. Decidido em conversa com o
Israel, que respondeu sim às três perguntas que destravam o desenho: a ameaça pode
reagir ao que ele construiu, ele quer ser exigido durante a onda, e torre pode ser
perdida.

**O problema que este desenho resolve.** O poder do jogador é a INTEGRAL do
dinheiro (uma torre da rodada 10 continua rendendo na 40 sem decisão nova); a
ameaça é INSTANTÂNEA (o que vem naquela rodada). Integral contra instantâneo faz a
folga crescer sozinha — é por isso que o Bloons precisa de HP exponencial. Só há
duas saídas honestas: a ameaça também virar integral, ou o poder deixar de ser
permanente. Este desenho usa as duas.

**Os três eixos, e o que cada um faz:**

1. **Intensidade indexada ao poder.** O orçamento do adversário deriva do valor
   investido na defesa (`base + k × investido^0,7`), não da rodada. Casa integral
   com integral. O expoente sublinear é o que impede virar rubber banding
   punitivo: ficar mais forte sempre compensa, só não trivializa.
2. **Vocabulário por anulação, não por volume.** O orçamento compra COMPOSIÇÃO,
   nunca gordura. Muito dano cortante na mesa → chumbo. Muita explosão →
   cerâmica. Pouca detecção → camo. Defesa amontoada → sabotador com escudeiro.
   Os 15 tipos e os 7 traços que já existem bastam; o que falta é o cérebro.
3. **Exigência por jogadas telegrafadas.** Parte do orçamento fica para agir
   DURANTE a onda: marcar uma área que será sabotada, injetar reforço, eleger a
   torre mais cara como alvo. Regra que mantém isso justo — **sempre telegrafado,
   sempre evitável, nunca aleatório**. A diferença entre "posso agir" e "tenho que
   agir sem parar" fica do lado certo.

**Perda de torre.** É o que quebra o poder permanente: se uma torre pode cair, o
acumulado para de crescer monotonicamente e a defesa passa a precisar ser
defendida — posição e cobertura mútua viram decisões reais. Três regras para não
virar punição: ameaça sempre visível com antecedência, sempre existe resposta
(matar o responsável, usar poder, aceitar a perda), e a torre destruída devolve
parte do investimento como sucata. **A perda é de posição e tempo, não do dinheiro
todo.**

**A REGRA DE OURO, que vale para o sistema inteiro:** ele só pode fazer perguntas
que o jogador tem como responder. Cobrar resposta a camo de quem ainda não tem
acesso a detecção não é dificuldade, é armadilha. Por isso o olho (4.1) mede
também o espaço de resposta disponível, e não só a defesa montada.

### 4.0 O leque do jogador — pré-requisito, não extra

Levantado pelo Israel: um sistema que se adapta exige que o jogador tenha com que
se adaptar de volta, e de forma gradual. Sem isso, adaptação vira beco sem saída.

**RETRATAÇÃO (2026-09-03).** Eu havia registrado aqui que "as trilhas são o mesmo
tier repetido três vezes". **É falso.** A listagem que gerou essa conclusão foi
feita com o caixa vazio: `TowerBase.UpgradePath` recusa em silêncio quando falta
dinheiro, o nível não subia, e `NextTier` devolvia sempre o primeiro tier. O mesmo
erro de medição já tinha produzido a conclusão falsa de que upgrades não davam DPS
(ver 2.4). Regra que fica: **encher o caixa antes de qualquer medição de upgrade.**

O que existe de verdade, verificado com caixa cheio: cada trilha tem três degraus
distintos, com nome, custo crescente e habilidade própria — Basic Turret vai de
"Dardos Afiados" a "Ponta de Aço" (`pierce3`) a "LANÇA PERFURANTE" (`pierce8`); a
Bomba vai a "BOMBA CACHO" (`cluster`) numa trilha e "MAR DE CHAMAS" (`firestorm`)
na outra. Além disso há regra de crosspath (`CrosspathCap = 2`): não se leva as
duas trilhas ao topo, então escolher uma é abrir mão da outra.

**A lacuna real é gradualidade, não profundidade.** Tudo está disponível desde a
rodada 1 — medido, os sete `DamageKind` compráveis logo de saída — e a
meta-progressão do `PlayerProgress` entrega só número (+2% dano/nível, +$10 de
verba inicial, +5 de vida a cada 5 níveis). Nada é descoberto, nada é conquistado.

Duas camadas ainda por explorar:

- **Entre partidas:** destravar OPÇÕES em vez de números. É o que dá gradualidade
  real e alimenta a regra de ouro — o que está destravado define o que pode ser
  cobrado.
- **Entre torres:** as 6 sinergias de adjacência do `SynergyManager`, hoje fixas.

### 4.1 O olho — FEITO (2026-09-03)

`DefenseReadout.cs`, pendurado no LevelManager da cena Game e disparado pelo
`EnemySpawner.onWaveComplete` (mesmo hook e mesma disciplina do `PlaytestLogger`).
**Nesta etapa ele só observa**: não gasta, não decide, não altera regra nenhuma —
dá para validar as leituras contra partidas reais antes de qualquer adversário
depender delas.

O que mede, por rodada: investimento total e **distribuição do investimento por
tipo de dano** (peso melhor que DPS nominal, porque Bomba e Tachinha acertam
vários alvos e o nominal delas mente — ver 2.4); cobertura do traçado por
amostragem de 120 pontos; cobertura de camo; sobreposição média (1 = fila
indiana, 3+ = amontoado, que é o que faz o Sabotador valer a pena); número de
buracos contíguos sem cobertura; e o espaço de resposta — caixa, plots livres e
quais tipos de dano o jogador **consegue comprar agora**.

Leitura real de um teste com 4 torres:

```
torres=4  investido=$880  caixa=$2295  plots livres=67
investimento por tipo:  Sharp=28%  Explosive=40%  Energy=32%
cobertura do traçado=59%  camo=0%  sobreposição média=2,7  buracos=3
pode comprar agora: Sharp, Control, Explosive, Piercing, Support, Energy, Economy
```

Sensibilidade verificada: ao construir dois Detectores, camo foi de 0% para 88%,
cobertura de 59% para 91%, buracos de 3 para 2 e Support apareceu com 21% do
investimento. Console 0 erros / 0 warnings.

**Decisão de projeto:** `DamageKind` é a ponte entre os dois lados do sistema — as
imunidades dos inimigos já são declaradas nesses mesmos termos (chumbo ignora
Sharp, cerâmica ignora Explosive), então "que pergunta a defesa não sabe
responder" virou conta, não opinião. Torre nova que ninguém classificar cai em
`Unknown` **de propósito**, e `Unknown` aparece no relatório: a lacuna grita em vez
de sumir.

### 4.1b Especificação escrita antes do código

### 4.1 Especificação escrita antes do código

Definir: de onde vem o orçamento por rodada, o que ele pode comprar, o que ele lê
da sua defesa, e o que ele nunca pode fazer.

### 4.2 Orçamento e catálogo

**Onde:** `EnemySpawner.StartWave`, exatamente onde `WaveScript.BuildQueue`
devolve `null` — as rodadas roteirizadas continuam intocadas.

**Passos:** orçamento crescente por rodada; preço por tipo de inimigo derivado do
custo real de matá-lo (sai da tabela da Etapa 2.1, não de um número novo).

### 4.3 Leitura da defesa

O que ele observa: quantas torres veem camo, quanto dano cortante versus
explosivo você tem, onde estão os buracos de cobertura, quais sinergias estão ativas.

**Armadilha:** ler a defesa a cada frame é caro e desnecessário — a leitura
acontece uma vez, entre rodadas.

### 4.4 Transparência

O jogador precisa ver o que ele comprou e por quê, antes da onda começar. Um
adversário que reage em segredo é indistinguível de dificuldade injusta.

**Quem:** `td-ui`.

### 4.5 Travas anti-frustração

Teto de concentração de um mesmo tipo por onda; memória curta (lê a defesa de
duas rodadas atrás, não a atual, para não punir a compra que você acabou de
fazer); rodadas roteirizadas preservadas como âncoras de ritmo.

### 4.6 Ajuste com dados

Repetir o protocolo da Etapa 3 com o adversário ligado e comparar os dois CSVs.

---

## Etapa 5 — Tema

Adiado de propósito desde 07/08. O motivo continua valendo: nove propostas de
tema foram recusadas porque eram skin decorativa. O tema tem que nascer das
peças depois que elas tiverem personalidade.

### 5.1 Inventário das peças com personalidade

UFOs de cinco cores, torres que crescem em altura, nove verbos distintos
(empurrar, plantar, marcar, energizar, atravessar armadura), aliados que sobem de
nível e obedecem ordens, sinergias por adjacência, e — se a Etapa 4 entrar — um
adversário que compra as ondas.

### 5.2 Três premissas candidatas

Cada uma precisa mudar estrutura, não pintura: o que ela faz o jogador decidir
que ele não decide hoje?

### 5.3 Aplicação

Nomes das torres, texto da loja, manchetes de rodada, menu. O texto já tem onde
morar: `UpgradeTier.description` e `WaveScript.Headline` já aparecem na tela.

---

## Etapa 6 — Acabamento

### 6.1 Sons reais

Os 9 efeitos são sintetizados em runtime (`AudioManager.cs`) e já existem campos
de override no inspetor para substituir um a um, sem tocar em código.

**Quem:** `asset-scout` procura, o Israel decide, `td-ui` liga.

**Armadilha:** o `??` do C# não respeita a sobrecarga de `==` do Unity e devolve
"fake null" — foi o que deixou 4 dos 9 sons mudos. Usar `!= null` explícito.

### 6.2 Mais tipos de aliado

Só existe o Batedor ($300). O `AllyManager` já aceita vários; falta o que
diferencia um do outro — o mesmo problema do "todas as torres são iguais" que
começou esta virada. Cada aliado precisa de um verbo próprio.

### 6.3 Onboarding

Nada hoje ensina camo, chumbo, armadura ou sinergia de adjacência. Um jogador
novo descobre por derrota.

### 6.4 Ícones de loja

Os botões da loja mostram preço; falta a silhueta que ligue o botão à torre que
aparece no tabuleiro.
