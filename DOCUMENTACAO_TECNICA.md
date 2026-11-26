# Documentação técnica — Astro Crawler

> Este arquivo não existia no repositório quando foi exigido pelo `AGENTS.md`;
> foi criado em 26/11/2026 junto com o Modo Esquadrão. Por enquanto ele cobre
> apenas os modos de jogo; as demais áreas do projeto ainda precisam ser
> documentadas aqui.

## Modos de jogo

O modo selecionado fica em `RunController.mode` (`RunMode` em
`Assets/AstroCrawler/Scripts/Data/AstroEnums.cs`):

| Modo | `RunMode` | Nave | Ondas | Pontuação |
|---|---|---|---|---|
| Campanha | `Campaign` | a escolhida no hangar | 10 por nível, chefe na 10ª | — |
| Modo Infinito | `Endless` | a escolhida no hangar | infinitas (ciclos de 5) | recorde local + ranking (Play Games / Game Center) |
| Modo Esquadrão | `Squadron` | sempre a padrão (`ShipUnlocks.FreeShipId` = `vanguard`) | infinitas (mesmas do Infinito) | recorde local próprio, sem ranking |

Propriedades auxiliares do `RunController`:

- `isEndless`: apenas o Modo Infinito (ranking, ícone ∞).
- `isSquadron`: apenas o Modo Esquadrão.
- `isInfinite`: Infinito **ou** Esquadrão. Usado em tudo que depende de "as
  ondas não acabam": gerador de ondas (`StageCatalog.GetEndlessWave`), escala
  de vida, escala de XP (`EndlessXpScale`), limite de nível 12, pontuação por
  onda/abate, créditos pagos a cada onda, ausência de reviver e de vitória, e
  a UI (HUD, pausa, tela de fim, warp, menu).

O modo não é salvo entre sessões (igual ao comportamento anterior do Infinito).

## Modo Esquadrão

### Regras implementadas

- A run usa sempre a nave padrão (Vanguarda-7), independentemente da nave
  selecionada no hangar. `RunController.BeginRun` troca a definição, e
  `GameUI.StartSelectedShip` já passa a nave padrão para o warp.
- O modo está sempre liberado (não depende de concluir níveis).
- O nível 1 continua dando uma melhoria normal.
- A cada `RunController.SquadronPickEvery` (= 3) níveis, enquanto houver
  posição livre, a escolha do nível vira **nave aliada**. Com 3 posições, isso
  acontece nos níveis 3, 6 e 9; o nível 12 volta a dar melhoria normal.
  1. escolher uma de até 3 naves oferecidas, sorteadas entre as naves
     elegíveis (`RunController.IsWingmanEligible`) sem repetir as que já estão
     na ala. Se todas as elegíveis já estiverem, repetições são permitidas.
     **As naves de Natal (`noel`, `arvore`, `neve`) nunca são oferecidas**,
     porque ainda não estão prontas. A regra usa
     `ShipUnlocks.IsAvailableForAcquisition`, a mesma que as tira da loja.
     Hoje são elegíveis `vanguard`, `bastion`, `spectre`, `aurora` e `orion`;
  2. escolher uma das **3 posições em triângulo** livres ao redor da nave
     principal (0 = em cima, 1 = embaixo à esquerda, 2 = embaixo à direita).
- Com as 3 posições ocupadas, as escolhas voltam a ser só melhorias.
- Cada aliada traz a **habilidade especial** da sua nave: um botão a mais na
  fileira do canto inferior direito, à esquerda do botão da nave principal
  (até 4 habilidades no total). Ver "Habilidades das aliadas".
- Cada aliada traz também as **cartas exclusivas** da sua nave para o
  sorteio de melhorias (ver "Cartas das aliadas").
- As naves oferecidas não dependem de o jogador possuí-las no hangar.
- A pontuação usa as mesmas regras do Infinito (`EndlessScore`), mas o recorde
  fica em `PlayerPrefs` na chave `AstroCrawler.Squadron.v1.best`
  (`RunController.SquadronBest`) e **não** é enviado ao ranking. Runs em
  `DebugMode` não registram recorde.

### Naves aliadas (`Combat/SquadronWing.cs`)

- Criado pelo `RunController.BeginRun` no modo Esquadrão e destruído no
  `TearDown`.
- Cada aliada fica parada num vértice de um triângulo equilátero centrado na
  nave: a 1,5 unidade dela (`SquadronWing.SlotRadius`), nos ângulos 90°, 210°
  e 330° (`SlotOffset`). Não recebe dano e não colide.
- Dispara sozinha no inimigo mais próximo dentro do alcance
  (`CombatWorld.NearestInRange`, a partir da própria posição), com:
  - dano = `damagePerShot` da nave × `DamageShare` (0,55) × multiplicador de
    dano da run; bônus contra chefe e chance de crítico (×2) das cartas;
  - intervalo = `fireInterval` da nave × `FireIntervalScale` (1,25) ×
    `fireIntervalScale` da run;
  - `baseProjectiles` da nave em faixas paralelas; perfuração se a run tiver.
- Não herda melhorias passivas do hangar nem os especiais/assinaturas da nave.

### Habilidades das aliadas (`Combat/PlayerShip.Special.cs`, `UI/ShipAbilityButton.cs`)

- A habilidade roda na nave principal (as aliadas só atiram). `PlayerShip`
  guarda uma lista `WingSpecial` (dona, definição, recarga, tempo ativo e VFX
  próprio), preenchida por `AddWingSpecial` quando a aliada é posicionada.
  Naves sem especial (`ShipSpecialKind.None`) não ganham botão.
- A aliada entra com a habilidade já carregada. A recarga de cada uma é
  independente e corre só durante a onda (`TickSpecial`).
- `SpecialIs(kind)` considera a habilidade própria **e** as emprestadas: assim
  Sobrecarga (cadência), Amplificar (dano), Égide (proteção), Estase
  (congelamento) e Reparo (cura) funcionam vindo de qualquer aliada. Com duas
  do mesmo tipo ativas, vale a mais forte.
- Cartas que mexem no especial também valem para as emprestadas: redução de
  recarga, duração extra, Paradoxo (`ReduceSpecialCooldown`) e Sobrecarga
  Furiosa (`ExtendSpecial`). `EffectRunner.OnSpecialActivated` é chamado a
  cada ativação. A assinatura visual (`OnSignatureSpecial`) segue só a
  habilidade da nave principal.
- UI: `GameUI.Special` instancia o mesmo prefab `ShipAbilityButton` mais 3
  vezes (`WingAbility0..2`, um por posição, `SquadronWing.SlotCount`), com `Bind(run, índice)`. Cada botão fica 172 px à
  esquerda do anterior, em escala 0,8, na cor da nave dona, e permanece oculto
  até a aliada correspondente entrar. O clique chama
  `RunController.ActivateWingSpecial(i)`, com as mesmas travas da habilidade
  principal (onda em andamento, jogo sem pausa, sem anúncio ou bloqueio de
  atualização).

### Cartas das aliadas (`Cards/CardSystem.cs`)

- `CardSystem.AddShipCards` adiciona ao pool da run as cartas exclusivas do
  deck da nave aliada que ainda não estão nele. É chamado por
  `RunController.ConfirmWingman`.
- O desbloqueio segue a regra normal (`CardUnlocks.IsUnlocked`) usando o nível
  de upgrade **da nave aliada**. Com essa nave sem upgrades, entram as 3
  cartas de `unlockLevel` 0. As de nível 10/30/60 exigem ter evoluído essa
  nave no hangar.

### Fluxo de escolha (`Game/RunController.cs`)

- `PrepareNextPick()` decide entre escolha de aliada (`isWingmanPick`, ofertas
  em `WingmanOffers`) e cartas normais. É usado pelo `OpenLevelPick` e pela
  sequência de escolhas pendentes.
- `ConfirmWingman(ship, slot)` posiciona a aliada e retoma a luta pelo mesmo
  caminho das melhorias (`ResumeAfterPick`). `ConfirmUpgrade` é recusado
  enquanto `isWingmanPick` estiver ativo.
- `claimingLevel` informa o nível que a escolha em tela está pagando.

### Interface

- `UI/GameUI.Squadron.cs`: tela `WingmanScreen`, construída em tempo de
  execução na mesma camada da `EvolutionScreen`. Passo 1: cartões das naves
  (casco, nome, dano, tiros/s, DPS). Passo 2: a nave principal no centro e
  as 3 posições em triângulo (uma acima, duas abaixo dos lados), como na
  arena; posições ocupadas aparecem com a nave e desativadas;
  botão "TROCAR NAVE" volta ao passo 1. `GameUI.HandlePhaseChanged` mostra
  essa tela em vez da de melhorias quando `run.isWingmanPick`.
- `UI/GameUI.Modes.cs`: terceiro cartão "MODO ESQUADRÃO" na página de modos
  (retrato: três cartões empilhados de 540 px e o título no topo da página;
  paisagem: três cartões de 580 px lado a lado). O cartão não tem botão
  secundário; JOGAR ocupa a largura. A linha de modo do menu e o ícone
  (`ic_esquadrao`, com o sprite `ship` como alternativa) também tratam o modo.

## Ícones das cartas

- O ícone de cada carta é pintado com a **cor exata da carta**, a mesma do
  anel, da régua e dos marcadores de nível (`CardWidget.Bind`): Fósforo no
  nível 1, Ciano no 2, Magenta no 3; nas pré-visualizações, a cor da
  categoria. Antes era usada uma versão clareada (35% da cor + 65% de branco).
- O tingimento da UI multiplica a cor pela arte, então a arte precisa ser
  branca. `CardIconFactory.Get` passa toda arte importada por `Monochrome`:
  se a arte for colorida (saturação média acima de 0,08), gera uma cópia em
  tons de branco que mantém o alfa e o sombreado interno. A leitura dos pixels
  passa por uma RenderTexture, então a textura não precisa de Read/Write. Arte
  branca é usada como está. Hoje só `ShieldIcon.png` (carta Defesa) é colorida
  (bege, média #C3AE90); as demais 19 artes de carta e os glifos
  procedurais já são brancos.
- **Espessura padrão:** todo traço de ícone de carta tem **4,7% da largura do
  ícone** (`CardIconFactory.Stroke` = 0,047 de meia-largura no espaço -1..1 dos
  glifos). Esse é o valor mediano das artes de linha `ic_*`, o estilo
  dominante.
  - **Artes de carta:** estão em `Resources/AstroCrawler/Art/CardIcons/card_<original>.png`
    (128 px no maior lado, brancas, com antialiasing) e são carregadas por
    `ArtLibrary.EffectIcon` via `CardArt`. As artes de linha foram redesenhadas
    a partir da linha central do traço original. As artes preenchidas (coração
    `PictoIcon_Life`, ímã `PictoIcon_Magnet`, caveira `PictoIcon_EnemyLevel`,
    raio `PictoIcon_Energy` e escudo `ShieldIcon`) viraram contorno da própria
    silhueta.
  - **Originais preservados:** os arquivos originais continuam onde estavam,
    porque também servem ao resto da UI (botões de habilidade, ícone do Modo
    Infinito, menus). Se uma arte de carta faltar, `CardArt` usa a original.
  - **Glifos desenhados por código:** são gerados em 128 px com 3×3 amostras
    por pixel (antialiasing). Todos os traços usam a mesma espessura `t`; os
    multiplicadores antigos (0,5× a 1,4×) foram removidos. Cruzes e barras que
    eram retângulos preenchidos viraram traços (`Plus`, `OutArrow`), e `Box`
    passou a centralizar o traço na borda, como `Line` e `Ring`. Pontos
    preenchidos (discos) seguem como detalhes.
  - **Centralização:** os offsets de `CardIconFactory.CenterArtwork` foram
    medidos de novo nas artes de carta (centro ponderado pelo alfa > 16/255,
    em pixels de textura).
  - **Ferramentas:** os scripts de reprocessamento e da folha de contato foram
    rodados fora do projeto (Python com Pillow, SciPy e scikit-image) e não
    fazem parte do repositório. As folhas de antes e depois estão na raiz:
    `icones_cartas_antes.png` e `icones_cartas_traco_uniforme.png`.
- O HUD (ícones das melhorias adquiridas) já usava a cor direta e passa a
  receber a arte branca. A silhueta preta da "próxima carta exclusiva" e das
  cartas misteriosas continua preta.

## Menu principal (retrato)

- O grupo da nave no menu (brilho, anel, nave, base, botão de troca, nome e
  chip "NÍVEL") fica dentro de `HeroZone/HeroContent`. O componente
  `UI/HeroFit.cs`, na `HeroZone`, reduz a escala do `HeroContent` (nunca
  aumenta) quando a altura livre entre o logo e o bloco de ações é menor que a
  do grupo. A escala gira em torno do centro visual do grupo, e na escala 1 as
  posições são as mesmas de antes.

## Histórico

### 26/11/2026 — Naves de Natal fora do sorteio de aliadas

- **Alteração:** `noel`, `arvore` e `neve` não aparecem mais como opção de nave
  aliada no Modo Esquadrão. Isso vale também no caso de reserva, quando todas
  as elegíveis já estão na ala: antes esse caso voltava a usar todas as
  naves.
- **Motivo:** pedido do usuário; as naves de Natal ainda não estão prontas.
- **Arquivos:** `Assets/AstroCrawler/Scripts/Game/RunController.cs` (novo
  `IsWingmanEligible`, usado em `PrepareWingmanOffers`).
- **Validação executada:**
  - compilação com o Roslyn do Unity, sem erros;
  - conferência em `ships.json`: restam 5 naves elegíveis, o suficiente para
    3 ofertas por escolha.

  **Não** foi testado em jogo (o sorteio é aleatório e a regra é um filtro
  direto).
- **Pendências:** quando as naves de Natal ficarem prontas, elas voltam ao
  sorteio ao sair da lista de `ShipUnlocks.IsAvailableForAcquisition`. Isso
  também as libera na loja.

### 26/11/2026 — Menu em retrato: chip "NÍVEL" sobre "MODO DE JOGO"

- **Problema:** em retrato, o chip "NÍVEL 0" da nave cobria a legenda "MODO DE
  JOGO" (print do usuário, com banner de anúncio). As peças da nave tinham
  posições fixas em volta do centro da área livre, e essa área encolhe em
  telas mais baixas e com o banner. Não tem relação com o Modo Esquadrão.
- **Alteração:** as peças da nave passaram para um grupo `HeroContent`, que o
  novo componente `HeroFit` reduz para caber na `HeroZone`.
- **Arquivos:** `Assets/AstroCrawler/Scripts/UI/GameUI.Menu.cs` (`BuildHero`)
  e `Assets/AstroCrawler/Scripts/UI/HeroFit.cs` (novo; o `.meta` foi gerado
  pelo Unity).
- **Validação executada:** compilação com o Roslyn e recompilação no editor,
  sem erros. Play Mode com a janela Game em 9:16 e banner de teste: o chip
  "NÍVEL 0" ficou acima de "MODO DE JOGO", sem sobrepor
  (`print_menu_retrato_nivel.jpg`). Com a janela Game pequena, o grupo
  encolheu como esperado.
- **Pendências:** conferir em aparelho real e em paisagem; pela conta, em
  paisagem a área livre sobra e a escala fica 1.

### 26/11/2026 — Esquadrão: 3 posições em triângulo

- **Alteração:** as aliadas passam de 4 cantos para **3 posições em
  triângulo** ao redor da nave principal (em cima, embaixo à esquerda,
  embaixo à direita).
  - A escolha de aliada acontece nos níveis 3, 6 e 9.
  - São até 4 habilidades no total (a principal mais 3).
  - A tela de escolha mostra as posições em triângulo.
  - Os textos dizem "posição" e "de 3".
- **Motivo:** pedido do usuário.
- **Arquivos:** `Combat/SquadronWing.cs` (`SlotCount` = 3, `SlotRadius`,
  `SlotOffset`), `UI/GameUI.Squadron.cs` (layout e textos),
  `UI/GameUI.Modes.cs` (descrição do modo) e `UI/ShipAbilityButton.cs`
  (comentário). O resto usa `SquadronWing.SlotCount` e se ajusta sozinho.
- **Validação executada:**
  - compilação com o Roslyn do Unity e recompilação no editor, sem erros;
  - Play Mode (Game view em paisagem), numa run do Esquadrão: no nível 3
    apareceu "Posições livres: 3 de 3";
  - a tela "ESCOLHA A POSIÇÃO" mostrou as 3 posições em triângulo ao redor da
    nave principal, sem encostar no "TROCAR NAVE";
  - a Orion, posta na posição de cima, apareceu acima da nave na arena, com o
    botão da habilidade dela ao lado do principal;
  - no nível 4 o sorteio trouxe "Tiro Explosivo", carta da Orion.

  Prints: `print_esquadrao_triangulo.jpg` e `print_esquadrao_triangulo_arena.jpg`.
- **Ajuste após o teste:** a descrição do cartão do modo ficou longa e cobria
  a linha "VANGUARDA-7 · RECORDE". Foi encurtada para "…troque a melhoria por
  uma nave aliada (até 3, em triângulo)." Esse ajuste só foi compilado, não
  visto no jogo.
- **Observação de layout:** a aliada da posição de cima fica no mesmo lugar
  em que aparecem os avisos "ONDA N" / "ONDA N LIMPA", que a cobrem por
  alguns segundos.
- **Observação:** antes desta alteração o disco ficou cheio. Foi liberado
  espaço apagando o ambiente Python temporário desta sessão (na pasta
  temporária, fora do projeto).

### 26/11/2026 — Espessura uniforme nos ícones das cartas

- **Alteração:** todos os 54 ícones de carta passam a ter traço de 4,7% da
  largura:
  - 20 artes de carta novas em `Art/CardIcons`;
  - os 5 ícones preenchidos viraram contorno;
  - os glifos de código foram unificados e ganharam antialiasing.
- **Motivo:** o usuário viu ícones com linhas muito grossas e outros com
  linhas finas.
- **Arquivos:**
  - novos: `Assets/AstroCrawler/Resources/AstroCrawler/Art/CardIcons/` (20
    PNGs e `.meta`, com as configurações de sprite copiadas de
    `ic_barragem.png.meta` e GUIDs novos) e `CardIcons.meta`;
  - alterados: `Scripts/Art/ArtLibrary.cs` (`CardArt`, `EffectIcon`) e
    `Scripts/UI/CardIconFactory.cs` (`Stroke`, rasterização 128 px com
    antialiasing, `Plus`, `OutArrow`, `Box` e tabela de offsets).
  - Os PNGs originais não foram alterados.
- **Validação executada:**
  - medição da espessura de cada arte, antes e depois (transformada de
    distância sobre o esqueleto);
  - folha de contato dos 54 ícones, com os glifos renderizados por um porte
    automático da função de desenho C#;
  - compilação com o Roslyn do Unity e importação no editor (20 texturas
    importadas, recompilação sem erros);
  - Play Mode: página Deck Base e escolha de melhoria inicial (Cadência
    Aumentada, Cura por Abate e Escudo Recarregável), com traços iguais e na
    cor da carta. Print em `print_icones_escolha.jpg`.
- **Pendências:**
  - o escudo vem de uma arte de 24 px, então o contorno tem leve ondulação;
    uma arte vetorial nova resolveria;
  - nem todos os 54 ícones foram vistos dentro do jogo, só na folha de
    contato.

### 26/11/2026 — Ícones das cartas na cor da carta

- **Alteração:** os ícones das cartas usam a cor exata da carta. A arte
  colorida (o escudo bege da Defesa) é convertida para branco em tempo de
  execução antes de ser tingida.
- **Motivo:** o usuário viu ícones com cor diferente da carta (ex.: escudo na
  carta de nível 1, que é branca).
- **Arquivos:** `Assets/AstroCrawler/Scripts/UI/CardIconFactory.cs`
  (`Monochrome`, `importedArt`, ajuste em `CenterArtwork`) e
  `Assets/AstroCrawler/Scripts/UI/CardWidget.cs` (cor do ícone). Nenhuma arte
  (PNG) foi alterada.
- **Validação executada:**
  - análise de cor de todas as 20 artes de ícone de carta (leitor PNG em
    Python), que identificou só `ShieldIcon.png` como colorida;
  - compilação com o Roslyn do Unity 6000.3.14f1, sem erros.

  **Não** foi vista em Play Mode: o Unity estava fechado.
- **Pendências:** conferir em jogo a carta Defesa nos níveis 1, 2 e 3 e o
  ícone dela no HUD.

### 26/11/2026 — Animação de "habilidade pronta"

- **Alteração:** os botões de habilidade (o principal e os das aliadas) ganham
  uma animação contínua enquanto podem ser tocados:
  - o botão "respira" (escala de até +6%, ciclo de 1,1 s);
  - um halo suave na cor da nave pulsa atrás dele;
  - um contorno hexagonal se expande e some a cada 1,4 s.

  A animação para assim que o botão deixa de estar disponível (em recarga,
  com a habilidade ativa, em pausa ou durante uma escolha de melhoria). O
  "punch" do ícone ao ficar pronto continua igual.
- **Motivo:** pedido do usuário, para indicar que a habilidade está
  disponível para uso.
- **Arquivos:** `Assets/AstroCrawler/Scripts/UI/ShipAbilityButton.cs`
  (`BuildReadyFx`, `TickReadyFx`, imagens `ReadyHalo` e `ReadyRing` criadas
  em tempo de execução). O prefab `ShipAbilityButton` não foi alterado.
- **Validação executada:** compilação com o Roslyn do Unity 6000.3.14f1, sem
  erros. **Não** foi vista em Play Mode: o Unity estava fechado.
- **Pendências:** conferir no Play Mode a intensidade e o ritmo da animação, e
  se o halo ou o contorno encostam nos botões vizinhos das aliadas.

### 26/11/2026 — Esquadrão: habilidades e cartas das aliadas

- **Alteração:**
  - ao posicionar uma aliada, a habilidade especial dela vira um botão ao lado
    do da nave principal (até 5 no total);
  - as cartas exclusivas dela entram no sorteio de melhorias;
  - a grade de cantos da tela de aliada foi redimensionada, porque em
    paisagem encostava no botão "TROCAR NAVE" e cobria a nave do centro.
- **Motivo:** pedido do usuário.
- **Arquivos:** `Combat/PlayerShip.Special.cs`, `UI/ShipAbilityButton.cs`,
  `UI/GameUI.Special.cs`, `UI/GameUI.cs`, `UI/GameUI.Squadron.cs`,
  `Cards/CardSystem.cs` e `Game/RunController.cs`, todos em
  `Assets/AstroCrawler/Scripts/`. Nenhum prefab, cena ou ScriptableObject foi
  alterado: os botões extras são instâncias do prefab existente.
- **Validação executada:**
  - compilação com o Roslyn do Unity e recompilação no editor, sem erros;
  - Play Mode no Unity (Game view em paisagem), numa run do Esquadrão até o
    nível 4: no nível 3 apareceu a escolha de aliada; a Aurora Médica foi posta
    no canto superior direito; o botão rosa da habilidade dela apareceu à
    esquerda do botão principal e, ao ser tocado, entrou em recarga; no nível 4
    o sorteio trouxe "Drones Atiradores", carta exclusiva da Aurora.
- **Pendências:**
  - não testado: o novo tamanho da grade de cantos (só compilado: o Play
    seguinte não pôde ser controlado), 3 a 5 botões ao mesmo tempo, layout em
    retrato, e duas habilidades iguais ativas juntas;
  - o painel de detalhes do HUD (ícone "?") mostra só a habilidade da nave
    principal.

### 26/11/2026 — Modo Esquadrão

- **Alteração:** novo modo de jogo em que se joga com a nave padrão e, a cada
  3 níveis, a melhoria é trocada por uma nave aliada em um dos 4 cantos ao
  redor da nave principal.
- **Motivo:** pedido do usuário.
- **Arquivos:**
  - novos: `Assets/AstroCrawler/Scripts/Combat/SquadronWing.cs`,
    `Assets/AstroCrawler/Scripts/UI/GameUI.Squadron.cs`, este documento;
  - alterados: `Data/AstroEnums.cs` (enum `RunMode`), `Game/RunController.cs`,
    `UI/GameUI.cs`, `UI/GameUI.Modes.cs`, `UI/GameUI.Hangar.cs`,
    `UI/GameUI.Menu.cs`, `UI/GameUI.Hud.cs`, `UI/GameUI.Pause.cs`,
    `UI/GameUI.Run.cs`, `UI/GameUI.Warp.cs` (todos em
    `Assets/AstroCrawler/Scripts/`).
  - Nenhuma cena, prefab, Prefab Variant ou ScriptableObject foi alterado.
- **Validação executada:** compilação do `Assembly-CSharp` com o Roslyn do
  Unity 6000.3.14f1, usando o `.rsp` gerado pelo editor mais os dois arquivos
  novos — sem erros. **Não** foi executado no editor/Play Mode nem em
  aparelho: layout das telas, balanceamento e sensação de jogo não foram
  verificados.
- **Pendências:**
  - testar no Play Mode (retrato e paisagem) a página de modos com três
    cartões e a tela de escolha de aliada;
  - os `.meta` dos dois scripts novos são gerados pelo Unity ao importar;
  - não há arte própria `ic_esquadrao` (usa o sprite da nave como alternativa);
  - balanceamento de `DamageShare`, `FireIntervalScale` e `CornerOffset`
    precisa de teste em jogo;
  - textos novos estão inline com `L(...)` (PT/EN/ES), como o resto da UI;
    nos outros idiomas o jogo usa `GameLocalization.Additional(pt, en)`, como
    nos demais textos.
