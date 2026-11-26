# Astro Survivor — Pilares de UI e Direção de Arte

23/11/2026 · Ely Honda · cópia em Markdown do documento [no Claude](https://claude.ai/artifact/8THu7DZBWS46fMFTdR2Ys6)

## 1. Para que serve

O jogo irmão herda do Astro Survivor uma UI **wireframe sci-fi sobre o vazio**: fundo quase preto, contornos finos em néon, cor que sempre significa alguma coisa e brilho só por componente, nunca pintado na imagem. Este guia reúne as regras que o Ely aprovou (ou recusou) entre ago e nov/2026, os números que deram certo e a lista de arquivos a copiar.

Como usar:

- **Seção 2 (pilares)** é o que não se negocia. Se uma tela nova contradiz um pilar, a tela muda.
- **Seções 3 a 7** são a especificação: cores em hex, tamanhos de texto, peças por função, movimento.
- **Seção 8** é o pacote de assets e código para levar ao projeto novo.
- **Seção 9** é o checklist de revisão de cada tela.

Fontes: `DOCUMENTACAO_TECNICA.md` (§1, §6.4–6.5, §7, §12.6, §12.53–12.56), a classe `Palette` em `Core.cs` e as decisões registradas do dono do jogo. O GDD §14 no Notion é a referência de design original; onde este guia diverge dele, vale o que está no jogo hoje (desvios aprovados depois do GDD).

## 2. Os pilares

São nove regras. As três primeiras vêm do GDD §14.1; as outras seis nasceram de correções feitas no jogo e já se provaram.

1. **Wireframe neon sobre o vazio.** O contorno define a forma; o miolo é quase o fundo (Vazio a 82%). Sem sombra, sem skeuomorfismo, sem textura de material. O brilho é a única profundidade. *Por quê:* herda o Asteroids de 1979, lê bem em tela pequena e é barato no celular.
2. **Cor é significado, nunca enfeite.** Seis cores normativas, cada uma com um papel fixo (seção 3). Antes de pintar algo, pergunte "o que essa cor diz?". *Por quê:* o jogador lê a tela inteira pela cor em fração de segundo, e uma cor fora do papel cria uma mentira.
3. **Zero palavras durante o jogo.** Na arena só ícone e número. Texto mora nos menus. *Por quê:* legibilidade de relance e tradução barata (9 idiomas com 250–400 strings).
4. **O polegar manda.** O topo (0–15%) informa, o meio (15–65%) é a arena e não recebe UI, a base (65–100%) é do polegar. Toque mínimo de 88 px de canvas. Um toque que nasce num botão nunca pilota a nave. *Por quê:* jogo de uma mão, no ônibus, na fila.
5. **Nada tapa a informação.** Feedback vai para a borda da tela, não para o centro. Efeito do jogador não cobre o corpo do chefe. Aviso de ataque cobre no máximo 40% da tela. O tiro inimigo fica por cima de tudo. *Por quê:* o perigo é o que o jogador precisa ver; o resto é decoração.
6. **Estado e evento não falam com a mesma voz.** Um estado que dura (vida crítica) e um evento que pisca (dano) precisam de cor ou ritmo diferentes. "Quando tudo treme, nada treme": tremor e vibração são só de chefe, miniboss e dano no jogador. *Por quê:* aviso que nunca para vira ruído e o jogador desliga.
7. **Brilho é componente, não pixel.** Arte de UI é vetorial e nítida; o glow vem só do componente `Brilho` (True Shadow), nunca pintado na imagem. *Por quê:* glow dentro da textura, esticado, vira borrão (o Ely viu isso nos planetas e mandou refazer).
8. **A recompensa se vê chegar.** Ganhos são revelados em sequência, contam para cima e voam até a carteira. Nunca escrever "você morreu": a tela convida a jogar de novo. *Por quê:* a sequência é a dopamina, e a derrota precisa terminar num convite.
9. **Denso e legível ao mesmo tempo.** O público primário é o Japão, onde UI e marketing carregados são o padrão: selos, preços, relógios e chamadas são bem-vindos. A densidade se paga com texto grande (nome 44, legenda 36). *Por quê:* cortar informação por conta própria foi corrigido pelo Ely em 02/11/2026.

Um critério atravessa os nove: **jogo leve é permanente.** Toda peça nova declara o peso em MB e o custo por quadro.

## 3. Linguagem visual

A paleta tem seis cores normativas e uma exceção. Elas moram na classe `Palette` (`Core.cs`) e o código usa apelidos por função (`Palette.Ore`, `Palette.UIAccent`), nunca o hex solto.

| Cor | Hex | Significado | Nunca usar para |
| --- | --- | --- | --- |
| Vazio | #05060A | Fundo; miolo de painéis a 82% | — |
| Fósforo | #E8F4FF | Traço primário, HUD, texto, carta comum | Destaque de ação |
| Ciano | #00E5FF | Jogador, XP, aliado, botões do menu, carta rara | Inimigo |
| Magenta | #FF2D95 | Dano, perigo, chefe, carta de evolução, badges | Efeito do jogador |
| Âmbar | #FFC24B | Minério, recompensa, o botão JOGAR, compra possível | Aviso de perda |
| Ácido | #7CFF4B | Cura, confirmação, vitória, oferta da loja | Inimigo |
| Crítico | #FF2D4B | **Só** a moldura de vida crítica | Qualquer outra coisa |

Regras da cor:

- **O jogo tem um vermelho só.** O #FF2D4B existe por contraste: o flash de dano já é magenta, então "estou quase morrendo" precisava de outra voz. Fica colado no Magenta na roda (mesma saturação e brilho) para não sair da família.
- **No menu, todo botão é ciano e o JOGAR é o único âmbar.** Exceções: o verde da oferta, os pontos magenta de novidade e o minério âmbar.
- **Âmbar é o que se ganha.** Texto sobre o que se perde ("Sair perde os minérios") vai em Fósforo a 70%, nunca em âmbar.
- **Raridade pela borda:** Fósforo comum, Ciano raro, Magenta evolução.
- **Cores de nave ocupam faixas próprias da roda:** Violeta #A96BFF, Rosa claro #FF8AD8, Índigo #7C84FF, além de ciano, azul-claro, laranja e verde. Toda cor nova de nave precisa ser conferida contra os inimigos: em 10/11/2026 cada cor de nave tinha uma gêmea inimiga e jogadores não distinguiam aliado de inimigo.
- **Efeitos de pacotes de fora são tingidos pela paleta.** Os fundos do Hovl vinham em laranja de fogo; viraram Ciano (escolha de upgrade, força 0,5), Âmbar (vitória) e Magenta (derrota).

Forma:

- **Segmentos retos.** Círculo vira polígono de 12 lados; ícones na grade 24×24 com traço de 2 px.
- **Perigo tem ângulo agudo, aliado tem polígono regular.** A forma diferencia mesmo sem cor (daltonismo, contraste mínimo 4,5:1).
- **A massa triangular é a nave do jogador.** O `ship.png` (borda 0,09, miolo 0,55) é só das naves desde 10/11; a identidade de cada nave é a tinta. Revetorizações foram recusadas: nariz fino, cabine escura e asas caídas viram "bicho".
- **Inimigo novo usa formas que o jogo já tem** (hex_disc, wire_ring, wire_hex, losango), não desenho inventado.
- **Tiro inimigo tem receita única (neon):** linha branca só na borda + brilho magenta 0,55, camadas acima de tudo. Contorno preto e ponto branco no meio foram recusados.
- **Julgar arte reduzida** a 100/52/32 px, nunca no zoom.

Divergem do GDD §14.2 e valem as do jogo: o vidro dos painéis tem degradê leve por dentro (kit Sci-Fi) e a fonte é a Rajdhani, não uma fonte de segmentos.

## 4. Tipografia e densidade

A fonte da interface é a **Rajdhani SemiBold** (latim e símbolos comuns), com fallback **Noto Sans CJK** em atlas estático só com os ~1.550 caracteres que o jogo escreve. O canvas de referência é **1080×1920** em retrato.

| Uso | Tamanho (px de canvas) |
| --- | --- |
| Título da tela de carregamento | 136 |
| Manchete do resultado | 62, encolhe até 40, sem quebra |
| Prêmio, rótulo de botão | 46 |
| Nome / título de linha | 44–46 |
| Linha de baixo (legenda) | ~36 |
| Contagem | 34 |
| Corpo de texto em caixa (notas) | 30, encolhe até 22 |
| Altura da linha de lista | 198, prêmio no canto de baixo |

Regras:

- **Caixa alta** em títulos de janela (`Box/Title`) e manchetes. Manchete, carimbo de recorde e aviso de chefe em **negrito inclinado** com degradê leve.
- **Todo texto tem autoajuste com teto e piso.** A frase mais longa dos 9 idiomas (geralmente italiano ou francês) não pode decidir o corpo das outras oito. Abaixo de 22 fica ilegível: aí é o texto que encurta.
- **Botão usa ícone desenhado, nunca caractere Unicode.** A fonte não tem os símbolos e eles viram quadrado vazio. Em botão só entram letras e números.
- **Algarismos sem pulo:** cronômetro MM:SS; milhar com separador (N0) em abates e pontos.
- **Nunca `Brilho` em texto que muda** (pontuação ao vivo, contadores): refaz a textura todo quadro.
- **Idioma:** padrão = o do sistema, fallback `en`, nunca perguntar no primeiro boot. Nome do idioma no próprio idioma (日本語) e sem bandeira. Coreano e japonês quebram por caractere.
- **Densidade:** não "limpar" telas por conta própria. Selos, preços, relógios e chamadas são desejáveis pensando no Japão. Só se remove o que o dono pedir.

Lacuna herdada: a cadeia de fontes não tem cirílico, grego nem árabe (aparece em nomes de conta no placar).

## 5. Componentes

A forma de cada peça segue a **função** dela. Cada papel tem três camadas: `linha` (traço, na cor de destaque), `vidro` (miolo escuro, vazado onde passa o traço) e `placa` (as duas juntas). O vidro ocupa o mesmo retângulo da moldura, sem recuo.

| Papel | Onde | Desenho | Sprites |
| --- | --- | --- | --- |
| botao | Ação com rótulo, abas | Canto em diagonal + marcas | `sf_botao_*` |
| icone | Botão de ícone, alternadores | Octógono | `sf_icone_*` |
| painel | Janela modal, ficha da nave | Degrau no topo | `sf_painel_*` |
| card | Ladrilho, carta, dia do calendário | Dois cantos cortados | `sf_card_*` |
| faixa | Linha de lista, campo de texto | Traço fino | `sf_faixa_*` |
| reta | Janela de vídeo da ficha | Retângulo simples | `sf_reta_*` |
| JOGAR e troféu | Par redondo do menu (220 e 150) | `ItemButton04White_2` | escolha do dono, não trocar |

Peças avulsas: setas `sf_seta_dir/esq` (todas as setas do jogo, nunca "<" em texto), trilho de pontas `sf_trilho` (só barra do chefe e do carregamento), fio `sf_fio` (XP), véu `sf_degrade` (topo da HUD), faixa de manchete `sf_aviso_*`, divisor `sf_divisor`, slots `sf_skill_*`, selos de maestria `sf_selo_*`.

**HUD (topo, dentro da caixa segura):**

- **Vida = silhuetas da nave selecionada** (3 por nave, regra do Ely). Escudo = silhueta com contorno duplo ciano, à direita. O último ícone pulsa em magenta.
- **Nível** num hexágono wireframe; **cronômetro** no topo central, magenta e pulsando a partir de 4:30; **minério** âmbar no canto direito.
- **XP minimalista:** fio de trilho + linha ciana de 6 px, 20 px de recuo de cada lado. O Ely recusou contorno de pontas na XP.
- **Pontuação ao vivo** colada acima da barra de XP, trocando a 10 Hz, sem brilho.
- **Slots de upgrade** (68 px) abaixo da XP: moldura + anel de recarga + véu preto 68%, sem número de segundos. Ativável ciano na frente, passivo branco no fim, evolução magenta.
- **Nada girando na HUD.** Tudo que anima por quadro refaz o canvas.

**Moldura da tela:** é a única superfície de feedback que não disputa espaço com o jogo. Funciona como **fila de prioridade, nunca soma**: cura (Ácido) > dano (Magenta) > vida crítica (#FF2D4B) > chefe chegando (Magenta) > frenesi do combo (Âmbar). Duas camadas: traço de 2 px e halo atrás com metade do alfa. Canto em superelipse (p=4) para acompanhar o vidro arredondado do celular. O flash de dano escala com a vida perdida: alfa 0,55 + 0,35 × (1 − vida)².

**Telas:**

- **Level up:** jogo visível atrás, escurecido; 3 cartas centralizadas (decisão do dono; o GDD pedia metade de baixo); tocar na carta já escolhe, sem confirmar. Relógio de 20 s escolhe sozinho.
- **Gasto permanente pede dois toques:** nó da Oficina abre painel na metade de baixo com "atual → depois" (valor novo em ciano), custo âmbar e CONFIRMAR largo. Sem saldo: botão apagado e custo pisca magenta uma vez, sem pop-up de erro.
- **Hangar:** grade de 2 colunas só com a nave na cor dela e o nome; estado sem texto (em uso = moldura ciano + visto; travada = cadeado, que acende âmbar quando o minério já paga). Tocar abre a ficha com vídeo de 5 s da nave em jogo.
- **Opções:** lista de linhas com ícone à esquerda e controle à direita; deslizador de 5 segmentos; abas GERAL / ACESSIBILIDADE.
- **Menu:** os cantos de cima são **avisos** (telas que abrem sozinhas; o botão só reabre), a linha de baixo são **destinos**. Abre sozinho no máximo uma coisa por volta ao menu.
- **Caixa segura:** `SafeAreaFitter` em toda tela; 16 px de margem própria, elementos de canto a 20 px, 44 px reservados no topo. Nada sangra até a borda. Tablet (mais largo que 9:16) casa o canvas pela altura (`EscalaPorAspecto`).

## 6. Movimento e feedback

Toda animação de UI passa por uma porta só (`Juice`, sobre LeanTween) e roda em **tempo não escalado**, porque level up e resultado acontecem com o jogo parado. Nada espera a animação para funcionar: tocar no JOGAR aos 0,1 s já entra na partida.

O vocabulário de movimento:

| Gesto | Quando | Regra |
| --- | --- | --- |
| Mola do botão | Todo toque em botão | Afunda e volta passando do ponto, com som de clique |
| Soco | Valor que muda (contador, nível, milhar) | Um soco novo cancela o anterior do mesmo alvo |
| Cascata | Entrada de lista e grade | Itens entram em sequência; filho de LayoutGroup só anima escala e alfa |
| Pop / salto | Modal, aviso, ícone novo | Véu funde, caixa salta; fechar leva 0,14 s com o véu ainda segurando o toque |
| Respirar | Algo raro ou novo esperando ação | Carta de evolução, setor NOVO, botão ×2 |
| Carimbo torto | Só a frase rara | "NOVO RECORDE!"; as manchetes comuns entram com pop reto |
| Voo de minério | Todo prêmio de minério | Diamantes explodem da origem e voam até o contador, que sobe a cada um |

**Tela de resultado — a sequência é a dopamina:**

1. Manchete: "SETOR CONCLUÍDO!" em Ácido ou "TENTE OUTRA VEZ!" em Ciano, sobre a faixa `sf_aviso_*`. Nunca "você morreu".
2. Tempo sobrevivido, grande, ao centro.
3. Destruições e pontos contando para cima, lado a lado (na derrota, a meio tom).
4. Subida no ranking global, da posição antiga até a nova.
5. Minério voando até o cartão.
6. Botão ×2 com ícone de vídeo, que chega elástico e respira.
7. Fileira casa / ranking / repetir e o botão largo do próximo passo ("PRÓXIMO: NÍVEL N").

Regras de feedback:

- **Fundo de momento fica até a tela sair.** Efeito com prazo fixo acaba no meio da leitura e parece bug; quem desliga é o `Hide` da tela.
- **Nenhuma partícula do tamanho da tela:** teto de 35% da largura visível.
- **Tremor e vibração são eventos raros:** chefe, miniboss e dano no jogador. Inimigo comum não treme a tela.
- **Curar só avisa quando curou de verdade;** regeneração passiva não pisca a moldura.
- **Contador de minério sempre por `VooDeMinerio.Exibir`,** senão o número pula para o fim e volta.
- **Transição é enfeite:** o salto de estrelas para o setor nunca pode ser o motivo de um botão não responder. Esperar duas vezes (efeito + carga) lê pior que esperar uma.
- **Reduzir flashes** corta os clarões brancos para ~40–50% e a moldura pela metade da amplitude.

## 7. Leitura em jogo

A UI do jogo continua dentro da arena: o que o jogador precisa ler no meio do caos segue as mesmas regras de clareza.

- **Sprite um pouco maior que a colisão (~5%).** Desenhar o inimigo menor que o corpo dele é injusto na direção errada: o jogador apanha de algo que não vê. Em 09/2026, 13 inimigos estavam assim, o pior um projétil 44% menor que o raio.
- **Chefe ocupa uma fatia legível da tela.** Em retrato, os chefes ocupam 41–58% da largura; o menor (O Farsante, 29%) lê como inimigo comum.
- **Corpo do chefe nunca é tapado.** Efeito de área que cairia em cima dele desliza ~0,75 do raio para fora da borda; o dano é o mesmo, porque o chefe é medido pela borda.
- **Todo ataque letal tem aviso desenhado:** corredor, círculo de 12 lados ou hexágono, cujo preenchimento é o relógio. Mínimo 0,8 s; teto de 40% da tela; contorno com `LineRenderer` em unidades de mundo, nunca sprite esticado.
- **O que machuca tem uma receita só** (tiro neon por cima de tudo); efeito do jogador é recessivo e não usa a faixa magenta/vermelha.
- **Acessibilidade:** opção "inimigos em cor única" (tudo que machuca vira Magenta), efeitos suaves, números de dano e vibração desligáveis.
- **Número de dano nasce no ponto de contato,** não no centro do alvo.

## 8. Kit de assets a copiar

O jogo só depende de `Assets/Art/ui` para o visual dos componentes (~409 KB). O pack de origem (928 MB, 700 de PSD) fica em `ForaDoBuild/` e só é necessário para regerar o kit. Copie sempre **com os arquivos .meta**: prefabs referenciam sprites por GUID.

**Arte e fontes**

| O quê | Caminho | Observação |
| --- | --- | --- |
| Kit Sci-Fi (peças por papel) | `Assets/Art/ui/sf_*.png` + `ItemButton04White_2.png` | 80 arquivos com .meta, ~409 KB |
| Ícones de ação e de upgrade | `Assets/Art/ic_*.png` | 192 px nítidos, sem glow; trazer só os genéricos (play, home, retry, gear, back, lock, trofeu, ore, pausa, menu, notas, suporte, globe) |
| Formas de arame da UI | `Assets/Art/ui_wire_*.png` | Cópias só da UI; o mundo usa `wire_*` |
| Formas base | `hex_disc`, `ring`, `diamond`, `poly_disc`, `wire_frame`, `dashed_ring`, `DashLine.mat` | Para inimigos e avisos no padrão |
| Pictogramas | `Assets/Art/PictoIcon_*.Png` | Presente, ímã, vida, cofrinho, anúncio, visto |
| Fonte latina | `Rajdhani-SemiBold.ttf` (no pack Shift) + `Resources/Fontes/Latin.asset` | |
| Fonte CJK | `Assets/Art/Fontes/NotoSansCJK-VF.ttc` (fora de Resources) + `Resources/Fontes/CJK.asset` | Atlas estático; regerar com os textos do jogo novo |

**Código de UI (namespace `AstroSurvivor`)**

| Script | O que faz |
| --- | --- |
| `Palette` (dentro de `Core.cs`) | As cores. No jogo novo, separar num arquivo próprio |
| `UI/Brilho.cs` | Única porta para o True Shadow (glow, pulso, piscar) |
| `UI/FormaVetorial.cs` | Polígono, arco, polilinha e pílula vetoriais, sem textura |
| `UI/Juice.cs` | Soco, cascata, pop, respirar e `MolaDoBotao` sobre LeanTween |
| `UI/VooDeMinerio.cs` | Moeda voando até o contador |
| `UI/SafeAreaFitter.cs`, `UI/EscalaPorAspecto.cs` | Caixa segura e escala para tablet |
| `UI/BarraDeXP.cs`, `UI/SlotDeUpgrade.cs`, `UI/GiroDeAnel.cs` | Barra com rastro e mola, slot com recarga, anel girando |
| `UI/UISounds.cs` | Clique e whoosh sintetizados (sem arquivo de áudio) |
| `Localization/FontRegistry.cs` | Liga a CJK como fallback da latina |
| `SpriteFactory.cs` | Gera formas e ícones procedurais (`Icone`, `WireHex`, `BorderGlow`) |
| `Editor/KitSciFi.cs` + `Editor/SetupUI.VisualSciFi.cs` | Passo 1 gera o kit; passo 2 veste prefabs. Idempotente |
| `Editor/ShiftUI.cs` | Construtor de `Botao`/`Placa` usado pelos enxertos |

**Pacotes de terceiros usados**

| Pacote | Uso | Peso no projeto |
| --- | --- | --- |
| True Shadow (Le Tai) | Todo glow de UI | 935 KB (sem a pasta Demo) |
| LeanTween | Animação de UI | 7,9 MB no projeto |
| Shift — Complete Sci-Fi UI | Fonte Rajdhani e base dos enxertos | 1,8 MB |
| SCI-FI UI Pack Pro | Fonte do kit `sf_*` (fora do build) | 928 MB |
| Hovl Studio — Background VFX | Salto para o setor e fundos de momento | 19 MB |

Os pacotes pagos da Asset Store precisam estar na conta de quem vai abrir o projeto novo; confira a licença de cada um antes de copiar a pasta.

**Como levar:** os pacotes já estão prontos na pasta `KitDeUI/` do projeto (veja o `LEIAME.md` dela). Importe no projeto novo antes de criar qualquer tela, nesta ordem: `AstroUI_1_Arte` (sprites, ícones, formas e este guia), `AstroUI_2_Fontes` (Rajdhani + Noto CJK já ligadas) e `AstroUI_3_Codigo` (os scripts que compilam sozinhos, o LeanTween e uma ponte de som `AudioFX` para ligar ao áudio do jogo novo). `VooDeMinerio`, `BarraDeXP`, `SlotDeUpgrade`, `SpriteFactory` e os editores do kit ficaram de fora porque dependem de sistemas do Astro Survivor; servem de referência, não de cópia. O `UIRoot.prefab` **não** deve ir inteiro: ele é o jogo, não o sistema. Use os prefabs de item (`ButtonIcon`, `UpgradeCard`, `ShipTile`, `WorkshopRow`) como referência de montagem.

## 9. Checklist de conformidade

Revise toda tela nova do jogo irmão com esta lista antes de mostrar ao dono.

**Cor e forma**

- [ ] Cada cor usada tem o papel da seção 3; nada de hex solto fora da `Palette`.
- [ ] Nenhum vermelho fora da vida crítica; perigo em Magenta.
- [ ] Um único botão âmbar por tela (a ação principal ou a compra).
- [ ] Peça escolhida pelo papel (botao, icone, painel, card, faixa), com vidro no mesmo retângulo da moldura.
- [ ] Nenhum glow pintado na imagem; brilho só por `Brilho`, e nunca em texto que muda.
- [ ] Cor nova de personagem conferida contra as cores dos inimigos.

**Texto e layout**

- [ ] Tamanhos do padrão (nome 44, legenda 36, prêmio 46) com autoajuste e teto.
- [ ] Testado na frase mais longa dos 9 idiomas e em japonês.
- [ ] Nenhum caractere Unicode em botão; setas são `sf_seta_*`.
- [ ] `SafeAreaFitter` na tela; nada sangra até a borda; conferido em 19,5:9 e em tablet 3:4.
- [ ] Toque mínimo de 88 px; nada interativo na faixa da arena.
- [ ] Densidade mantida: nada foi cortado sem pedido do dono.

**Movimento e feedback**

- [ ] Entrada animada via `Juice`, em tempo não escalado, sem bloquear o toque.
- [ ] Todo botão tem `MolaDoBotao` e som de clique.
- [ ] Ganho de moeda passa por `VooDeMinerio`.
- [ ] Nenhum aviso permanente na mesma cor ou ritmo de um evento.
- [ ] Nada gira ou anima por quadro na HUD.

**Peso**

- [ ] Sprite novo com Read/Write desligado; peso em KB anotado na proposta.

Pendências herdadas do Astro Survivor que o jogo novo pode resolver desde o início: Canvas único (separar HUD dos menus), ausência de Sprite Atlas e de compressão ASTC definida, cronômetro da HUD reescrito todo quadro, fonte sem cirílico/grego/árabe.

## Fontes

- [Astro Survivor — GDD v2](https://app.notion.com/p/3c2e5a37882d81a5aa3ce8adf31a4639), §1 (pilares de design), §7 (game feel) e §14 (interface), lido em 23/11/2026. Divergência conhecida: o §14.4.b diz "sem pausa", mas desde 10/11/2026 a HUD tem botão de pausa ao lado do ≡ (benefício do Premium ou por vídeo) e ainda não voltou ao GDD.
- `DOCUMENTACAO_TECNICA.md` do projeto: §1, §6.4–6.5, §7, §12.6, §12.53–12.56.
- Código: `Palette` em `Assets/Scripts/AstroSurvivor/Core.cs`, `Assets/Scripts/AstroSurvivor/UI/`, `Assets/Editor/KitSciFi.cs`.
