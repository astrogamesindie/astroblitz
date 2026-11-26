# AstroCrawler

Roguelike sci-fi de arena com melhorias permanentes, implementado a partir do
[GDD no Notion](https://app.notion.com/p/3d1e5a37882d81d4abb5ed2194443625).

A nave fica parada, mira e atira sozinha no inimigo mais próximo. Antes de cada
onda, o jogador escolhe exatamente uma entre três melhorias; ela permanece na
build pelo resto da run. Durante o combate não há cartas nem ações manuais. Uma
run = 10 ondas, com o Núcleo Aberrante na onda final. As fases são Nível 1,
Nível 2 e Nível 3. Consulte `../../DOCUMENTACAO_TECNICA.md` para o estado técnico atual.

---

## Como rodar

1. Abra o projeto no Unity **6000.0.58f2**.
2. `Tools ▸ AstroCrawler ▸ Abrir cena do jogo` (a cena é criada automaticamente
   na primeira importação, em `Assets/AstroCrawler/Scenes/AstroCrawler.unity`).
3. Play.

A cena tem só dois objetos: uma câmera e um GameObject com `GameManager`. Todo o
resto — arena, nave, inimigos, HUD, telas, efeitos — é construído em runtime. Não
há prefabs nem referências por GUID que possam quebrar.

### Controles

| Ação | Entrada |
|---|---|
| Escolher melhoria permanente | clique em uma das três opções antes da onda |
| Ver detalhe de uma melhoria adquirida | clique no ícone dela no HUD |
| Pausar / continuar | botão redondo no canto superior direito, **Esc**, voltar do Android ou Start do gamepad |
| Acelerar o jogo (1x → 2x → 3x → 1x) | botão redondo logo abaixo da pausa |
| Sair da run | pausa → *Reiniciar* ou *Menu* (os dois pedem confirmação) |
| Voltar uma tela nos menus | botão redondo no canto superior esquerdo, **Esc** / voltar do Android |

O jogo usa o **Input System novo** (o projeto está com `activeInputHandler: 1`,
ou seja, só o pacote novo). `UnityEngine.Input` legado não funciona aqui.

---

## Mapa da arquitetura

```
Scripts/
  Art/        ArtLibrary                              ← carrega a arte do Resources
  Audio/      SoundDirector (+ enum Sound, Sfx.Play)    ← sons e música
  Data/       AstroEnums, Definitions, GameConfig,
              CardAsset, ShipAsset, EnemyAsset, WaveAsset,
              GameDatabase, PrefabLibrary, SoundLibrary ← ScriptableObjects
  Cards/      RuntimeCard, CardSystem                 ← seleção de melhorias
  Combat/     Arena, SpriteFactory, CombatWorld, Enemy,
              PlayerShip, Projectile, Missile,
              BlackHole, Shockwave, DebrisWall, Pulse,
              AbilitySpawner, EffectRunner            ← simulação em tempo real
  Juice/      Juice, JuiceDirector, Impact, DamageNumber
  Game/       GameManager, RunController, WaveController,
              GameSpeed, StarfieldLayer, StaticState
  UI/         UIFactory, CardWidget, CardIconFactory,
              ButtonFeedback, TextUnderlay, SafeArea, ScreenFit,
              GameUI (+ .Hud .Menu .Hangar .Run .Upgrades .Pause)
  Utils/      ListShuffle
Editor/       AstroCrawlerEditor, AstroCrawlerSceneBuilder,
              AstroCrawlerAssetBuilder, AstroCrawlerPrefabBuilder,
              AstroCrawlerSoundBuilder, AstroCrawlerBalanceBot
Database/     48 ScriptableObjects (17 cartas, 3 naves, 7 inimigos, 20 ondas)
              + PrefabLibrary + SoundLibrary
Audio/        UI/, Partida/, Musica/ (usados) e Reservado/
Art/Reservado/  sprites guardados para recursos planejados (fora do build)
Prefabs/      11 prefabs de runtime
Scenes/       AstroCrawler.unity (sistemas, starfield e UI assados)
Resources/AstroCrawler/  GameDatabase.asset
                         Art/  (naves, asteroides, ícones, fontes,
                                AstroCrawler SDF e AstroCrawler Bold SDF)
                         UI/   (frames, ícones e bordas sci-fi, 9-slice)
                         *.json (fonte de importação; o jogo lê o banco)
LeanTween/Framework/     (só o framework; exemplos e docs foram arquivados)
ThirdParty/FullSerializer/
```

Cada classe de MonoBehaviour e de ScriptableObject vive num arquivo com o nome
dela. Isso não é estilo: a Unity só cria o `MonoScript` — e portanto só consegue
serializar o componente num prefab ou na cena — quando o nome do arquivo bate.
Foi por isso que `Missile`, `Shockwave`, `Pulse`, `BlackHole` e `DebrisWall`
saíram de `AbilityObjects.cs`.

Responsabilidades, em uma linha cada:

- **`GameConfig`** carrega as quatro tabelas JSON e valida a integridade delas.
- **`CardSystem`** é lógica pura (sem MonoBehaviour, sem Unity): monta três
  opções distintas e registra as melhorias permanentes adquiridas na run.
- **`CombatWorld`** é o registro único de inimigos vivos — alvo mais próximo,
  inimigos num raio, contagem da onda, evento de abate.
- **`EffectRunner`** transforma uma melhoria escolhida em estado de jogo.
- **`PlayerShip`** não lê input nenhum: ela só combina o statline da nave com os
  modificadores ativos.
- **`RunController`** é a máquina de estados da run.
- **`ArtLibrary`** é o único ponto que conhece nomes de arquivo de arte.
- **`Juice` / `JuiceDirector`** são o vocabulário de animação (ver abaixo).
- **`SoundDirector`** toca os sons por `Sfx.Play(Sound.X)`; o mix fica no
  `SoundLibrary` (ver "Áudio").

---

## Arte

Toda a arte vem da base do **Astro Survivors** e do kit **Shift – Complete
Sci-Fi UI**, em duas pastas dentro de `Resources`:

| Pasta | Conteúdo |
|---|---|
| `Resources/AstroCrawler/Art/` | `ship`, `asteroid_0..4`, `bullet`, `missile`, `glow_circle`, `ring`, `dashed_ring`, os `wire_*`, os 27 `ic_*` de melhoria, os `PictoIcon_*` de HUD, `Logo` e as fontes |
| `Resources/AstroCrawler/UI/` | frames `Cut Frame *` (9-slice), `Gradient Background*`, `Circle *` e os ícones de interface |

Duas regras que governam esse acoplamento:

**1. Nada é obrigatório.** Todo acessor de `ArtLibrary` cai de volta na forma
procedural que o `SpriteFactory` já gerava. Renomear ou remover um PNG degrada o
visual; não quebra o jogo. Foi por isso que o `SpriteFactory` continuou no
projeto em vez de ser deletado.

**2. `enemies.json` continua mandando no tamanho.** As formas procedurais foram
desenhadas com exatamente 1 unidade de mundo; os PNGs importados têm cada um seu
tamanho em pixels e seu *pixels-per-unit*. `ArtLibrary.ScaleToDiameter` converte
qualquer sprite para o diâmetro pedido, então o campo `radius` do JSON continua
sendo a fonte de verdade — trocar o sprite de um inimigo não muda a hitbox dele.

O mapeamento melhoria → ícone está em `ArtLibrary.EffectIcon`. Os 17 efeitos do
GDD têm ícone próprio; `CardIconFactory` só desenha o glifo vetorial quando esse
switch devolve `null`.

---

## Texto

Toda a interface é **TextMeshPro**. A fonte é a **Rajdhani SemiBold** (do kit
Shift), e o `TMP_FontAsset` é **criado em runtime** por
`UIFactory.TmpFont`, com atlas dinâmico:

```csharp
TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA,
                              1024, 1024, AtlasPopulationMode.Dynamic, true);
```

Isso mantém a regra de "nenhuma referência serializada" — não há `.asset` de
fonte comitado — e o atlas dinâmico rasteriza os acentos do português sob
demanda, em vez de exigir que eles sejam assados antes.

> **Atenção.** O `TMP Settings.asset` deste projeto aponta para dois font assets
> por GUID (`38675f7d…` e `ae1bb8f7…`) que **não existem mais**: os essentials do
> TMP foram movidos para `TextMesh Pro/Fonts & Materials (fora do build)` e
> reimportados com outros GUIDs. Ou seja, `TMP_Settings.defaultFontAsset` é
> `null`. Componentes TMP que dependam do default não renderizariam nada — todos
> os do AstroCrawler recebem a fonte explicitamente, e `UIFactory.TmpFont` tem
> como último recurso construir um asset a partir de uma fonte do sistema. Se
> você adicionar TMP fora do `UIFactory`, atribua a fonte na mão.

Outra armadilha, já resolvida: pedir esse asset **cedo demais** devolve um
`TMP_FontAsset` não-nulo mas **sem material e sem atlas**, e aí qualquer leitura
de `fontMaterial` lança `ArgumentNullException`. Era o que acontecia quando a UI
era construída no `Awake` do `GameManager`. Por isso `GameUI.Build` agora roda no
`Start`, `BuildFontAsset` confere material e atlas antes de aceitar o resultado, e
`AddUnderlay` desiste em silêncio se não houver material para clonar.

Sombra de texto usa o *underlay* do material SDF (`UIFactory.AddUnderlay`), não o
componente `Shadow` do uGUI — que custaria um segundo draw call e não se
comporta bem com TMP.

---

## Game feel

Nenhum script chama `LeanTween` diretamente; tudo passa por `Juice`,
`JuiceDirector` ou `Impact`. Isso existe por três motivos concretos:

1. **Os tempos ficam consistentes** entre telas (`Juice.Quick/Normal/Slow`).
2. **Toda animação de UI ignora `Time.timeScale`.** A tela de melhoria pausa o
   jogo (`timeScale = 0`); um tween em tempo escalado congelaria o próprio menu
   que está pedindo o clique.
3. **Todo callback verifica `null`.** Um tween sobrevive ao inimigo ou ao widget
   que o iniciou.

Cuidado não óbvio: `LeanTween.cancel(gameObject)` cancela **todos** os tweens do
objeto. Por isso existe `Juice.DealIn` (mover + escalar numa chamada só) em vez
de compor `SlideIn` com `PopIn` — as duas se cancelariam. Onde é preciso
preservar um tween em curso, o cancelamento é tipado
(`LeanTween.cancel(go, false, TweenAction.SCALE)`).

O que está animado:

| Onde | O quê |
|---|---|
| Combate | recuo e clarão de tiro, squash do inimigo ao levar dano, flash branco, explosão + destroços na morte, pop de spawn, trilha nos projéteis e mísseis, anel do escudo pulsando, aura da nave respirando |
| Câmera | shake com amplitude por evento (teto de 0.55 e ruído Perlin, em tempo real) e *hit stop* em impactos pesados |
| Tela | flash colorido por dano, cura, chefe e fim de run; banner de início e de onda limpa |
| Números | dano e cura flutuando em `TextMeshPro` de mundo, com pool e dispersão horizontal |
| HUD | barra de casco com camada "fantasma" atrasada, contadores com punch, ícones de melhoria entrando escalonados, barra do chefe interpolada |
| Cartas | entrada escalonada tipo baralho, hover com escala + brilho, punch e flash ao confirmar |
| Botões | hover e press por escala (`ButtonFeedback`) |

`JuiceDirector` centraliza shake, hit stop, flash e números porque eles precisam
**compor**: duas mortes no mesmo frame não podem virar um terremoto de dois
segundos, e o hit stop nunca pode brigar com a pausa da tela de melhoria (ele é
ignorado se `Time.timeScale` já estiver baixo, e só restaura o valor se ninguém
mais mexeu nele enquanto isso).

---

## Interface e navegação (mobile)

A interface é desenhada para celular em paisagem. Num aparelho de 6,5" a altura
de referência (1080 unidades) tem ~68 mm, então **1 dp ≈ 2,5 unidades**. Daí
vêm as constantes de `UIFactory`:

| Constante | Valor | Equivale a |
|---|---|---|
| `TextCaption` | 26 | ~10,5 sp, só para rótulos curtos e legendas |
| `TextBody` | 30 | ~12 sp, o mínimo para frases |
| `TextButton` | 36 | ~14 sp |
| `Touch` | 120 | 48 dp, o alvo de toque mínimo |
| `TouchLarge` | 148 | ação principal (JOGAR, CONTINUAR) |

Regras que o layout segue:

- **Ícone primeiro, palavra depois.** Voltar, pausa, ajustes e fechar são botões
  redondos só com ícone; as seções do menu são *tiles* com ícone grande e uma
  palavra; JOGAR, CONTINUAR, DE NOVO e MENU têm ícone + palavra.
- **Texto enxuto.** O parágrafo de identidade da nave virou uma linha de papel
  ("OFENSIVA PURA"); a dificuldade da fase virou estrelas; a explicação da árvore
  é uma linha que também serve de linha de mensagem.
- **Voltar sempre no canto superior esquerdo, pausa no superior direito**, como
  em quase todo jogo mobile.
- **`SafeArea`** encaixa tudo que se lê ou toca dentro de `Screen.safeArea`
  (notch, câmera, cantos arredondados). Os fundos escuros das telas vazam além
  dela (`UIFactory.Page`), para não sobrar uma faixa sem cobertura.
- **`ScreenFit`** ajusta o `CanvasScaler`: em celular mais largo que 16:9 casa
  pela altura (o layout fica do tamanho certo e a largura extra vai para as
  laterais); em tablet 4:3/16:10 casa pela largura (nada é cortado).
- **Fonte Bold de verdade** (`AstroCrawler Bold SDF`, Rajdhani Bold) em títulos
  e botões, em vez do negrito sintetizado do TMP sobre a SemiBold.

```
Menu ─┬─ JOGAR ────────────────► run (escolha de melhoria ⇄ onda)
      ├─ nave em destaque / NAVES ► Escolha sua nave (+ DECK)
      ├─ FASES ─────────────────► Fases
      ├─ créditos / UPGRADES ───► Árvore de upgrades
      └─ engrenagem ────────────► Ajustes (música, efeitos, volume, idioma)
run ── pausa ──┬─ Continuar
               ├─ Reiniciar (confirma) ─► onda 1, mesma nave e fase
               └─ Menu      (confirma) ─► menu
fim ───────────┬─ De novo ─► mesma nave e fase
               └─ Menu
```

- **O HUD só existe durante a run.**
- **Seleção tem quatro sinais**: brilho (True Shadow), colchetes nos cantos
  (Shift), selo *SELECIONADA* e escala 103%; os não escolhidos recuam a 72% de
  opacidade. O card inteiro é tocável.
- **Naves se comparam por barras** normalizadas pela melhor nave em cada stat.
- **A pausa guarda e devolve o `timeScale`**; reiniciar/sair não devolvem, porque
  o `RunController` define o tempo sozinho. No aparelho, perder o foco no meio
  de uma onda pausa sozinho (desligado no editor).
- **Velocidade 1x/2x/3x.** O botão abaixo da pausa alterna a velocidade
  (`GameSpeed`); o `RunController` aplica esse valor quando a onda começa (antes
  era sempre 1). Trocar no meio da onda vale na hora; trocar com o jogo parado
  só vale quando ele volta a correr. Pausa, detalhe de melhoria e hit stop guardam
  e devolvem o valor de antes, então carregam a velocidade sem saber dela. A
  escolha fica salva (`AstroCrawler.GameSpeed` no PlayerPrefs). A 2x o botão fica
  azul e a 3x dourado, para um jogo acelerado nunca parecer o normal. Menus, fim
  de run e animações de UI continuam em tempo real. O bot de balanceamento
  roda sempre a 1x (`GameSpeed.forceNormal`), sem mexer na escolha salva.

O código está em `GameUI.cs` (ciclo de vida e roteamento), `GameUI.Hud.cs`,
`GameUI.Menu.cs`, `GameUI.Hangar.cs`, `GameUI.Run.cs`, `GameUI.Upgrades.cs` e
`GameUI.Pause.cs`. HUD, naves, escolha de melhoria e fim de run são **assados na
cena** (`Construct`); menu, fases, ajustes, árvore, deck e pausa são montados em
runtime (`Bind`).

**`GameUI.LayoutVersion`**: sempre que o que `Construct` monta mudar, aumente o
número e rode **Tools > AstroCrawler > Reassar interface** (reconstrói só a UI,
sem mexer no resto da cena) e salve. Se esquecer, o jogo detecta a versão antiga
e reconstrói em runtime — funciona, só boota mais devagar.

### True Shadow

Usado só em uGUI (é um `IMeshModifier` de `Graphic`; sprites do combate não
passam por ele — lá o brilho continua vindo das texturas de glow). Dois helpers
em `UIFactory`: `DropShadow` (sombra projetada que segue o recorte do frame) e
`GlowShadow` (halo aditivo para seleção, hover e ênfase; `FadeGlow`/`FlashGlow`
animam **só a cor**).

1. **`IgnoreCasterColor = true` sempre.** O cache do blur usa a cor do elemento
   no hash; sem isso, cada tint de botão refaria o blur.
2. **Animar a cor da sombra é barato** (vai para os vértices); tamanho e spread
   não são animados.
3. **Nada de True Shadow em TMP que muda ou escala** (em texto ele rastreia
   `lossyScale`). Só títulos fixos têm glow.

---

## Áudio

`SoundDirector` toca tudo com 12 vozes reaproveitadas em rodízio (sem alocar
por som) e a música de menu, que abaixa durante a run. Áudio ignora
`Time.timeScale`, então a pausa e a tela de melhoria continuam com som.
Chamada: `Sfx.Play(Sound.X)`. Música, efeitos e volume ficam salvos em
`PlayerPrefs` e aparecem em Ajustes.

**A cena não tinha `AudioListener`** — por isso o jogo era mudo. Sem listener,
todo `AudioSource` dá `isPlaying = true` e nada sai no alto-falante, então a
verificação anterior (que olhava qual clipe cada fonte estava tocando) passou
sem pegar o problema. `GameManager.BuildCamera` agora garante um listener na
câmera principal quando a cena não tem nenhum.

O mix está em `Editor/AstroCrawlerSoundBuilder.cs` (**Tools > AstroCrawler >
Gerar SoundLibrary**), que também aplica a importação mobile: efeitos em mono +
ADPCM, música em streaming Vorbis. Linguagem do mix: cliques são toques;
intervalos subindo são avanço/recompensa, descendo são voltar/perda; *rings* são
créditos e escudo; *glitches* são dano e erro; *impacts* são os grandes momentos.

| Som | Clipe | Onde |
|---|---|---|
| Tap / Back / Hover | `Click_Mid-High`, `Click_Pitched_Down`, `High_Click_1` | todo botão (`ButtonFeedback.clickSound`); hover só com mouse |
| Select / Navigate / Toggle | `Click_Scoop_Up`, `Click_Combo_3_High`, `Click_Combo_2_High` | escolher nave/fase, abrir seção, ajustes |
| Play | `Start.ogg` (Shift) | JOGAR, DE NOVO |
| CardsDealt / UpgradeChosen | `Click_Combo_4_High`, `Tone1B_MajorTriadUp` | escolha de melhoria |
| Purchase / Denied | `Ring_Pitched_Up` + `Tone1B_OctaveUp`, `Glitch_16` | árvore de upgrades |
| PauseOpen / PauseClose / ConfirmPrompt | `Tone1C_FifthDown`, `Tone1C_FifthUp`, `Tone1C_TritoneDown` | pausa |
| WaveStart / WaveCleared / BossIncoming | `Reverse_Ring_2`, `Tone1A_MajorTriadUp`, `Impact_2_Top_End` | banners de onda |
| Victory / Defeat | `Tone1A_OctaveUp` + `Impact_2_Reso`, `Tone1C_MinorTriadDown` + `Glitch_14` | fim de run |
| ShipHit / ShieldBlock / Heal | `Glitch_5`, `Ring_Pitched_Down`, `Tone1A_FifthUp` | nave |
| EnemyDown / BossDown | `Glitch_13` (variação de pitch, cooldown 60 ms), `Impact_2_Mid` | `CombatWorld.KillEnemy` |
| MissileLaunch / AreaAbility | `Click_Combo_5`, `Reverse_Ring_2_Low` | `Missile.Spawn`, `AbilitySpawner` |
| Música | `Background.ogg` (Shift) | menus; abaixada na run |

A escolha foi feita medindo os clipes (duração útil, pico, ataque), não só pelo
nome: todos os arquivos do pacote têm 0,7–1,4 s, mas só 0,07–0,3 s de som. Os
*Tone1* têm pico de −13 dB e os *Tone3* de −20 dB, por isso o mix usa *Tone1*.
**Ninguém ouviu o resultado ainda** — os volumes são um ponto de partida.

---

## O que veio dos pacotes Shift e SCI-FI UI SFX

Dos dois pacotes ficou só o que é usado, copiado para as pastas do jogo com
`AssetDatabase.CopyAsset` (mantém o 9-slice e as configurações de importação,
com GUIDs novos — reimportar um pacote no futuro não mexe nesses arquivos):

- **Áudio** (`Audio/UI`, `Audio/Partida`, `Audio/Musica`): os 30 clipes da tabela acima.
- **Sprites** (`Resources/AstroCrawler/UI`): ícones *Arrow Right, Audio,
  Campaign, Star, Star Filled, Swap, Tutorial, Armor Glow*; *Rounded Circle* e
  *Rounded Circle Outline* (botões redondos), *Rounded Filled* (pílulas, slider),
  *Cut Frame - 3px*, *Cut Frame Corner Glow* (colchetes de seleção), *Shiny Move*
  (o brilho que percorre o JOGAR), *Gradient Top/Bottom* (fades de borda),
  *Background Glow* (atrás da nave do menu) e *Lens Flare* (sob o título; limitado
  a 512 px — o original tem 1800×1000).
- **Fonte**: `Rajdhani-Bold.ttf`, de onde sai a `AstroCrawler Bold SDF`. As
  fontes SDF do Shift **não** foram usadas: são atlas estáticos com 98 glifos
  ASCII, sem "ç", "ã" ou "é".

**Reservado para uso futuro** (fora de `Resources`, então fora do build até ser usado):

| Arquivo | Para |
|---|---|
| `Audio/Reservado/Tone1A_Double.wav` | som de desbloqueio, quando houver naves/fases bloqueadas (o ícone `Lock` já está no set de UI) |
| `Art/Reservado/Visuals.png`, `Controls.png` | abas futuras de Ajustes: gráficos (qualidade para celular fraco) e controles/vibração |
| `Art/Reservado/DonutGlow.png` | indicador de carregamento (anúncios, salvamento na nuvem) |

O que **não** foi aproveitado do Shift, e por quê: os scripts (vários usam o
`Input` legado sem proteção, que lança exceção neste projeto), os prefabs e cenas
de demo (dependem desses scripts e do `UIManager` deles), os shaders de blur
(usam GrabPass, que o URP não suporta), o vídeo de fundo e os placeholders.

---

## Dados, prefabs e cena — e o contrato de regeração

O jogo não constrói mais nada que possa ser autorado antes. São três camadas:

**1. Dados em ScriptableObject.** `Resources/AstroCrawler/GameDatabase.asset` é a
única busca em `Resources` do boot; ele referencia os 48 assets em `Database/`.
Os JSON continuam no projeto, mas agora são **formato de importação**, não o que
o jogo lê — antes eles passavam pela reflexão do FullSerializer a cada boot.

**2. Prefabs.** `Database/PrefabLibrary.asset` aponta para os 11 prefabs. O maior
ganho está no inimigo: era o único spawn frequente que ainda era destruído e
recriado, e agora é `Instantiate` de um prefab **com pool** (`CombatWorld.Acquire`
/ `Recycle`). Projétil e efeitos de impacto já eram pooled, então para eles o
prefab só economiza o aquecimento do pool.

Pooling trouxe uma consequência sutil: um inimigo reciclado mantém o mesmo
`GetInstanceID()`, então um tiro perfurante que já tinha acertado aquele objeto
recusaria o novo ocupante. Por isso `Enemy.spawnId` é único por *spawn*, e é ele
que o `Projectile` rastreia.

**3. Cena.** `AstroCrawler.unity` carrega os sistemas (CombatWorld, EffectRunner,
WaveController, RunController, JuiceDirector, EventSystem), as 213 estrelas e os
140 objetos da interface — tudo ligado nos campos serializados do `GameManager`.

Tudo isso é **gerado a partir do código**, que continua sendo a fonte de verdade.
Os três geradores:

| Menu | Quando rodar |
|---|---|
| `Tools ▸ AstroCrawler ▸ Gerar ScriptableObjects` | depois de editar os JSON |
| `Tools ▸ AstroCrawler ▸ Gerar Prefabs` | depois de mexer em sprite, sorting order ou trilha |
| `Tools ▸ AstroCrawler ▸ Reconstruir hierarquia da cena` | depois de mexer no layout da UI **ou nos dados de uma nave** |

> A última linha é o preço de ter assado a UI inteira: os três cards da tela de
> naves são gerados a partir do banco, então mudar stats, deck, cor ou tagline de
> uma nave exige reconstruir a cena, senão a tela mostra dados velhos. O resto da
> interface não depende dos dados.

O caminho procedural não foi removido: todo prefab e todo campo de cena é
opcional, e um `GameManager` solto num GameObject vazio ainda constrói tudo em
runtime. É o que mantém o projeto funcionando enquanto os geradores não rodaram.

### Três coisas que não sobrevivem à serialização

Valem ser lembradas porque as três deram bug:

- **Material criado em runtime.** `new Material(shader)` com `HideAndDontSave`
  não pode ser salvo dentro de um prefab; a referência volta nula e a Unity
  desenha em rosa. Foi o que aconteceu com a trilha dos tiros. A correção foi
  usar `TrailSprite.mat`, que é um asset de verdade do set de arte.
- **Font asset criado em runtime.** Mesma história: todo rótulo assado voltava
  sem fonte. Agora `AstroCrawler SDF.asset` é gerado no editor
  (`Tools ▸ AstroCrawler ▸ Gerar fonte TMP`), com atlas e material como
  sub-assets. De quebra, o SDF não precisa mais ser rasterizado no boot.
- **Sprite criado em runtime.** As barras (vida, chefe, stats das naves) usavam
  um sprite branco gerado por código; assado na cena, ele volta nulo, e um
  `Image` do tipo *Filled* sem sprite ignora o `fillAmount` e desenha sempre
  cheio. As barras de stats pareciam iguais para as três naves. Agora usam
  `pixel.png`.

Ainda assim, `GameUI.Bind` reatribui fonte e sombra e refaz todos os listeners:
`UnityEvent.AddListener` feito em código e evento C# **não** são serializados, e
sem isso todo botão da cena nasceria morto. É o que `TextUnderlay` e
`CardWidget.RebindEvents` existem para resolver.

---

## Domain Reload desligado — o que isso obriga

`ProjectSettings/EditorSettings.asset` tem `m_EnterPlayModeOptions: 3`, ou seja
**Domain Reload e Scene Reload desativados**. Entrar em Play fica instantâneo, ao
preço de os `static` de C# sobreviverem de uma sessão para a outra enquanto todo
`UnityEngine.Object` que eles apontam já foi destruído.

Isso derrubou a interface inteira. O `LeanTween.init()` protege a inicialização
com `if (tweens == null)`; na segunda sessão de Play esse static ainda estava
preenchido, então ele **não recriava o objeto `~LeanTween`** — que tinha sido
destruído ao sair do Play anterior. Os tweens continuavam sendo registrados
(`tweensRunning` subia) e nunca avançavam: cada tela ficava congelada no alpha em
que o fade começou, sem nada no console.

`Scripts/Game/StaticState.cs` resolve com o gancho documentado para esse caso —
`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`,
que roda no início de toda sessão de Play, com ou sem domain reload. Ele zera
`LeanTween`, os caches de `SpriteFactory` e `ArtLibrary`, o font asset do TMP e
os pools de `Impact`, `Projectile` e `BlackHole`.

**Se você adicionar qualquer `static` que guarde um objeto de Unity, registre-o
lá.** Caso contrário ele volta na próxima sessão apontando para lixo.

---

## Balanceamento

Nada de valor de gameplay está hard-coded. Use:

**`Tools ▸ AstroCrawler ▸ Editor de Conteúdo`** — janela com abas de Cartas,
Naves, Inimigos, Ondas e Validação.

Ou edite `Assets/AstroCrawler/Resources/AstroCrawler/*.json` direto e rode
`Tools ▸ AstroCrawler ▸ Gerar ScriptableObjects` (o jogo lê os assets, não o JSON).

Cada carta tem `baseParams` e `evolvedParams` — um saco de pares chave/valor.
Parâmetro que a versão evoluída não sobrescreve herda o valor base. As chaves que
cada efeito lê estão no `switch` de `EffectRunner.Resolve`. Cada carta tem **dois
níveis** (base e evoluída): havia um terceiro, mas nenhum efeito lia nada além de
base/evoluída, então era uma escolha que não fazia nada. Quando todas as cartas
do deck estão evoluídas, a tela de escolha é pulada e a próxima onda começa.

Os raios do GDD (150, 220, 120…) são "pixels de design"; `Arena.PixelsPerUnit`
(hoje 60) é o único botão que converte todos eles para unidades de mundo.

### Alcance, mira e tiros

- **`Arena.DetectionRadius` = 6.** O canhão, os mísseis (inclusive o
  redirecionamento) e as melhorias automáticas só agem sobre inimigos cuja borda
  entrou nesse raio. A câmera mostra 7 de meia altura, então nada morre antes de
  aparecer — antes o alvo era "qualquer coisa na câmera", com os cantos sob o
  HUD, e as balas voavam 40 unidades. A nave desenha esse raio como um anel
  discreto que acende quando há alvo.
- **Balas** duram o alcance + 1,5 e testam colisão por varredura (segmento, não
  ponto): a 30 fps uma bala anda mais por quadro do que um meteoro pequeno mede.
- **Ataque Duplo/Triplo** dispara em pistas **paralelas** (0,2 de distância),
  não em leque: o leque de 7° abria 0,7 unidade a 6 de distância e só a bala do
  meio acertava um meteoro pequeno.
- **Mira sem desperdício:** cada inimigo soma o dano das balas já disparadas
  contra ele (`Enemy.incomingDamage`) e o canhão pula quem já vai morrer. Com
  inimigos de 2–3 tiros, mirar de novo no mesmo alvo desperdiçava ~1/3 dos tiros.
- **Investida de Impacto** deixou de ser imunidade total: o choque fere o
  inimigo e só não fere a nave se ele morrer.
- **Meteoros à deriva** oscilam em torno da linha até a nave em vez de acumular
  desvio; um deles chegava a passar longe do alcance e travar a onda por 40 s.
- **Pulso de interface sem acúmulo.** `Juice.Punch` guardava a escala *atual*
  como repouso; um pulso começado no meio de outro partia da escala aumentada.
  A Fúria Implacável cura a cada abate e fazia o painel de vida crescer até
  cobrir meia tela. Agora o repouso é lembrado enquanto o pulso dura, a cura
  só dá retorno (som e pulso) a cada 0,45 s, e cura com a vida cheia não mostra
  nada.
- **Paralisia Temporal** só congela inimigos dentro do alcance. Um inimigo
  ferido por um poço e congelado lá fora nunca entrava no alcance, e a onda
  ficava presa para sempre.

### Números

| Nave | Vida | Intervalo de tiro | Dano | DPS base |
|---|---|---|---|---|
| Vanguarda-7 | 100 | 0,30 s (era 0,15) | 8 | 27 |
| Bastião Aegis | 160 | 0,42 s (era 0,25) | 12 | 29 |
| Espectro Nulo | 80 | 0,34 s (era 0,20) | 9 | 26 |

| Inimigo | Vida (era) |
|---|---|
| Meteoro pequeno / médio / grande | 8 / 12 / 14 (1 / 2 / 3) — cada médio vira 2 pequenos, cada grande 2 médios |
| Batedor / Interceptador / Fragata | 12 / 26 / 90 (2 / 4 / 10) |
| Núcleo Aberrante | 1800 (80) |

Sobre a tabela vale **`StageCatalog.HealthScale`**: ×0,85 na onda 1, +12% por
onda até a 13, +6% por onda depois (×2,7 na onda 20); Setor Beta multiplica tudo
por 1,3 e Ômega por 1,6. Os fragmentos morrem com um tiro no começo, quando a
nave ainda não tem melhorias, e passam a pedir vários conforme a run avança.

Cartas: Sobrecarga de Motores +30% / +60% de cadência (era +50/+90); Rajada
Selvagem 12% / 25% de crítico x3 (era 15% x3 / 30% x4). O dano das habilidades
subiu junto com a vida (mísseis 25/34, buraco negro 18/24 por segundo, poços
16/s, drones de apoio 5/7 de dano por tiro, etc. — está tudo no `cards.json`). As ondas 2–5
ficaram mais espaçadas (0,9 / 1,1 / 1,2 / 0,85 s entre spawns): um meteoro médio
são 3 alvos e um grande são 7, e as ondas 3 e 4 chegavam como ~30 alvos em 10
segundos — mais do que a cadência base nova consegue atirar.

### Como foi medido

**`Tools ▸ AstroCrawler ▸ Balanceamento ▸ Simular Setor Alfa`** (ou os três
setores) entra em Play e joga runs inteiras sozinho com cada nave: escolhe a
melhoria com uma prioridade fixa (dano primeiro, depois habilidades, depois
sustentação), roda o relógio em passo fixo de 1/30 s o mais rápido que o editor
consegue (~4× o tempo real) e grava em `Logs/AstroCrawlerBalance.txt`, por
onda: duração, dano recebido, vida, tempo ocioso (inimigos vivos mas nenhum no
alcance) e a menor distância a que um inimigo chegou. O save é restaurado ao
final (créditos e setor selecionado). Os números acima saíram de cinco rodadas
desse bot, intercaladas com um simulador em Python da mesma mecânica (só tiros)
para varrer combinações rápido.

Resultado da última rodada (bot, sem upgrades passivos da árvore):

| Setor | Vanguarda | Bastião | Espectro |
|---|---|---|---|
| Alfa (normal) | vence · 35 de dano · chefe 28 s | vence · 15 · chefe 69 s | vence · 40 · chefe 77 s |
| Beta (difícil) | vence · 148 · chefe 38 s | morre na onda 11 | vence com 2 de vida · chefe 130 s |
| Ômega (extremo) | morre na onda 11 | morre na onda 11 | morre na onda 3 |

Leitura: o Alfa é vencível pelas três naves sem a árvore de upgrades; Beta já
derruba uma; Ômega pede a árvore. A onda 11 (10 meteoros grandes) é o muro dos
setores difíceis. O bot escolhe sempre "dano primeiro" e tem sorte nas ofertas
iniciais com frequência — um jogador que não pega Ataque Duplo cedo sente bem
mais as ondas 3–5. Ainda assim, na metade da run uma Vanguarda montada mata
meteoros com uma rajada; o que pede vários segundos são fragatas e o chefe.

---

## Mudanças feitas fora de `Assets/AstroCrawler/`

### Pacotes (era isto que travava o console)

O projeto estava marcado como **6000.6.0f1** no `ProjectVersion.txt`, mas essa
versão não está instalada — ele vinha sendo aberto com a **6000.0.58f2**. O
resultado eram duas famílias de erro que **não vinham do código do jogo**:

- 5 módulos que não existem na 6.0 (`adaptiveperformance`, `physicscore2d`,
  `tetgen`, `timelinefoundation`, `vectorgraphics`) e `com.unity.timeline 6.6.0`
  → *"Project has invalid dependencies"*.
- `com.unity.feature.2d 2.0.2` arrastava `2d.common 15`, `2d.animation 16`,
  `2d.aseprite 6`, `2d.tilemap.extras 9` — todos da linha 6.6, todos falhando com
  `EntityId`/`SpriteFitInfo`/`IsGPUSkinningEnabled` não encontrados.

Essa segunda família é que impedia o jogo de rodar: com os pacotes 2D quebrados,
o Unity nunca chegava a compilar o `Assembly-CSharp`, e o `.dll` em
`Library/ScriptAssemblies` estava parado em 11/set.

Correções aplicadas em `Packages/manifest.json`:

- removidos os 5 módulos inexistentes e `com.unity.timeline`;
- removido `com.unity.feature.2d` inteiro — o jogo não usa tilemap, PSD,
  Aseprite, SpriteShape, 2D Animation nem Pixel Perfect. `com.unity.2d.sprite` e
  `com.unity.2d.tilemap` são módulos embutidos do editor e continuam lá;
- removido `com.unity.pipeline` (experimental, exclusivo da 6.6);
- `packages-lock.json` podado dos 15 pacotes correspondentes;
- `ProjectSettings/ProjectVersion.txt` fixado em 6000.0.58f2.

Backups em `scratchpad/manifest.json.bak2`, `packages-lock.json.bak2` e
`ProjectVersion.txt.bak`. Se você quiser voltar para a 6.6, instale a
6000.6.0f1 e restaure os três.

### Um erro que era meu

`Projectile.cs` usava `EntityId` / `GetEntityId()` — API da 6.6 — para marcar
inimigos já atingidos por tiros perfurantes. Trocado por `int` /
`GetInstanceID()`.

### Render pipeline e build settings

- **`GraphicsSettings.asset` / `QualitySettings.asset`**: apontavam para
  `Assets/CCGKit/URP/*`. Repontados para `Assets/Settings/UniversalRP.asset` e
  `Assets/UniversalRenderPipelineGlobalSettings.asset`.
- **`EditorBuildSettings.asset`**: registra só a cena do AstroCrawler.

### `_Archive/` (fora de `Assets/`, o Unity ignora)

- `LeanTweenDemo/` — `Examples`, `Testing`, `Documentation` e o `Editor` do
  LeanTween. O script de editor só serve para abrir a pasta `Documentation`, e
  lança exceção sem ela; os exemplos trazem cenas, modelos e um script que usa o
  `Input` legado, que não funciona neste projeto.
- `UnityJunk/` — `New Scene.unity` e o `_Recovery/0.unity` de crash.
- `Pacotes/SCI-FI_UI_SFX_PACK/` (116 MB) e `Pacotes/Shift - Complete Sci-Fi UI/`
  (60 MB) — os pacotes completos, depois de copiado para o jogo só o que é usado
  (ver "O que veio dos pacotes"). Podem ser apagados quando quiser recuperar o
  espaço; os dois também podem ser reimportados da Asset Store.
- `Codigo/` — `AudioManager.cs`, `SoundFX.cs` e `ObjectPool.cs`, sobras do CCG
  Kit sem nenhuma chamada no projeto, substituídas pelo `SoundDirector`.

Para restaurar qualquer coisa, mova de volta para `Assets/`.

O CCG Kit e os defaults do template Unity, que em sessões anteriores estavam
aqui, já não existem mais no disco — `Assets/` hoje tem só `AstroCrawler/`,
`Settings/` e os dois assets de URP.

---

## O que veio do CCG Kit

O CCG Kit é um framework de card game **por turnos e em rede (Mirror)**. A
sobreposição real com o AstroCrawler é a camada de dados de cartas — não o motor
de jogo. Além disso, o kit **não compilava neste projeto**: depende do Mirror,
que nunca esteve no `manifest.json`.

| Do kit | Onde está agora |
|---|---|
| `ObjectPool`, `ListShuffle`, `AudioManager`, `SoundFX` | `Scripts/Utils/` |
| `FullSerializer` | `ThirdParty/FullSerializer/` |
| Padrão `GameConfiguration` (JSON + Resources + editor) | `Data/GameConfig.cs` |
| Padrão definição × instância (`Card`/`RuntimeCard`) | `Data/Definitions.cs` + `Cards/CardZone.cs` |
| `RuntimeZone` (pilha com eventos) | `Cards/CardZone.cs` |
| Singleton `GameManager` dono da config | `Game/GameManager.cs` |
| `CardView` (pintar carta a partir da definição) | `UI/CardWidget.cs` |
| Janela de editor com abas (`CCGKitEditor`) | `Editor/AstroCrawlerEditor.cs` |

Descartado: toda a camada de rede, o motor de efeitos por turno (`EffectSolver`,
`Effect`, `GameAction`, `Trigger`, `Ability`, custo de mana), as cenas de demo e
os assets URP do kit. A arte de carta do kit foi substituída pela do Shift.

---

## Pontos do GDD que precisam de decisão

Implementados como está no documento, mas vale registrar:

1. **Chefe com 80 de HP (seção 5.3).** Era morto em uma rajada. Agora tem 1800
   (×2,7 na onda 20 ≈ 4900); ver "Balanceamento".
2. **Nave 100% fixa (seção 9, item 1).** Implementada como fixa.
3. **Colisão destrói o inimigo.** O GDD não diz o que acontece com ele. Aqui é
   consumido no impacto e **não fragmenta** — senão ficaria sobreposto à nave
   causando dano repetido.
4. **Ondas "mistas" (5 e 16).** O GDD dá só o total. Distribuídas como 4/4/2
   (onda 5) e 6/5/5 (onda 16) entre pequeno/médio/grande.
5. **Detonação em Cadeia evoluída** lida como profundidade máxima 2.

---

## O que foi verificado, e o que não foi

**Verificado.** Os 95 scripts de runtime e os 3 de editor compilam com o Roslyn
da própria 6000.0.58f2, reaproveitando os response files que o Unity gerou (mesmo
conjunto de defines e referências que o editor usa): **0 erros, 0 warnings**. As
58 referências a `Resources` no código foram conferidas contra o disco — todas
resolvem, com duas exceções tratadas em código (`Logo.png` é multi-sprite e
precisa de `LoadAll`; o font asset do TMP não existe e tem fallback).

As regras da seção 4 do GDD já haviam sido exercitadas fora do Unity contra a
implementação real de `CardSystem`, e os quatro JSONs passaram pela validação do
`GameConfig` real.

**Rodado no editor.** O jogo foi executado em Play mode: tela de naves, escolha
de melhoria, início de run e combate com inimigos em arena, com console limpo (0
erros, 0 warnings). Entrar e sair de Play duas vezes seguidas — o caso que
quebrava — foi verificado explicitamente.

Depois da conversão para assets, o teste foi refeito **recarregando a cena do
disco** (`OpenScene`, não o Play mode, que com Scene Reload desligado usa a cena
em memória e esconderia exatamente este tipo de falha): 50 rótulos, 0 sem fonte;
as três camadas de estrela com seus 28/65/120 renderers e alphas intactos; os 3
botões de nave e os 3 cards de melhoria respondendo. O pool de inimigos foi
exercitado com 3 ciclos de spawn-e-matar em massa, e os reciclados voltam com HP,
escala e `spawnId` novos.

**Medido.** `GameUI.Build` custava 31 ms e 164 transforms — foi o que motivou
assar a interface. Não medi ganho de frame em combate denso; o argumento ali é
estrutural (menos alocação e menos `AddComponent` por spawn), não uma medição.

**Ainda não medido.** Balanceamento, e a intensidade do shake, do hit stop e do
fade em uso prolongado. Os tempos de animação foram escolhidos por julgamento,
não por teste — `Juice.Quick/Normal/Slow` e as amplitudes em `JuiceDirector` são
os botões para ajustar. O texto da lista de melhorias na tela de naves cai para
~11pt com nove itens (auto-size), o que é legível mas apertado; se quiser maior,
o caminho é aumentar o painel, não a fonte.

**Interface e pausa (Unity 6000.3.14f1).** 113 scripts de runtime e 5 de editor
compilam com 0 erros e 0 warnings. Em Play mode, cada tela foi capturada e
conferida, e os fluxos foram exercitados de ponta a ponta: pausar e continuar
numa onda (`timeScale` 1 → 0 → 1); pausar e continuar na escolha de melhoria
(fica em 0); cancelar a confirmação; *Reiniciar run* (volta à onda 1 com 0
melhorias); *Voltar ao menu* (HUD some, nave destruída, `timeScale` 1); derrota
→ *Menu principal*. Uma onda inteira rodou com os números de dano todos com fonte
e nenhum warning — antes eram dezenas por onda.

Não exercitado: uma **compra** na árvore de upgrades (o save do editor tinha 0
créditos e não foi alterado para o teste — só o caminho "sem saldo"); Esc e o
voltar do Android foram testados pela lógica que eles chamam, não por tecla real;
e a pausa automática ao perder o foco só existe fora do editor.

**Mobile, áudio e pacotes (Unity 6000.3.14f1).** 118 scripts de runtime e 6 de
editor compilam com 0 erros e 0 warnings, e o console fica limpo ao entrar em
Play (fora o erro de pacotes que já existia, ver abaixo). Em Play numa janela
2:1 (proporção de celular), todas as telas foram capturadas: menu, naves, deck,
fases, ajustes, árvore, escolha de melhoria, HUD, pausa, confirmação e fim de
run. Os toques foram simulados como cliques reais do `EventSystem` (passando
pelo `ButtonFeedback`), e o áudio foi conferido pelo clipe que cada `AudioSource`
estava tocando após cada ação — seleção, voltar, toggle, JOGAR, cartas, escolha,
início de onda, dano, pausa, retomar, alerta de confirmação e derrota tocaram o
clipe esperado; a música de menu entra em loop. Ligar e desligar Efeitos
atualiza o estado e o rótulo.

Não verificado: **como os sons soam** (escolhidos por medição, não por audição —
ajuste os volumes no `AstroCrawlerSoundBuilder`); `SafeArea` num aparelho com
notch (no editor a área segura é a tela toda); e, de novo, uma compra na árvore
(o save de teste tem 0 créditos).

**Rebalanceamento, som e cartas (Unity 6000.3.14f1).** 118 scripts de runtime
e 7 de editor compilam com 0 erros e 0 warnings. Em Play: a cena passa a ter 1
`AudioListener`; as três cartas de melhoria terminam a animação na mesma altura
com um "ponteiro parado" simulado sobre a do meio durante a entrada, e o destaque
só é aplicado depois que ela pousa (antes o hover cancelava o tween de posição e
a carta ficava 70 px abaixo). O alcance e os tiros paralelos foram conferidos
numa captura da câmera do jogo. O pulso do painel de vida foi disparado em 90
quadros seguidos, junto com curas, e o painel voltou à escala 1. O balanceamento foi medido com o bot (ver
"Balanceamento"); o save do editor foi restaurado depois de cada rodada.

Não verificado: o som **audível** (o listener existe e as fontes tocam, mas
ninguém ouviu); o hover com um mouse real; e o balanceamento com escolhas de um
jogador humano — o bot sempre pega dano primeiro, e as árvores de upgrade
estavam zeradas (sem bônus passivos).

**Velocidade 2x/3x (Unity 6000.3.14f1).** 119 scripts de runtime e 7 de editor
compilam com 0 erros e 0 warnings; a interface foi reassada (layout 5) e a cena
salva. Em Play, com cliques no botão: a onda começa em 1x; na escolha de melhoria
o rótulo troca e o tempo fica em 0; a onda seguinte começa na velocidade
escolhida (3); hit stop a 3x vai a 0,05 e volta a 3; trocar a velocidade no meio
de um hit stop não deixa o tempo preso em 0,05; pausar e continuar a 2x volta a 2.
Uma onda inteira rodou a 3x sem erros no console. A preferência de teste foi
apagada no fim.

**Erro de pacotes que já existia.** O `Packages/manifest.json` lista
`com.unity.modules.physicscore2d`, `tetgen` e `timelinefoundation`, que o Unity
6000.3 não tem; isso gera um erro de resolução a cada abertura do projeto. Não é
destas mudanças e não foi mexido.
