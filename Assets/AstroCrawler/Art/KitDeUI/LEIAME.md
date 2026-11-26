# Kit de UI do Astro Survivor

Pacotes para levar a direção de arte e a interface do Astro Survivor a um jogo irmão. As regras estão em `PILARES_DE_UI.md` (na raiz do projeto e também dentro do pacote de arte, em `Assets/AstroUIKit/`).

Gerados em 23/11/2026 a partir do projeto na Unity 6000.0.58f2. Todo arquivo vai com o `.meta` original, então os GUIDs são os mesmos do Astro Survivor: prefabs e materiais copiados depois continuam achando os sprites.

## Ordem de importação

No projeto novo: **Assets › Import Package › Custom Package…**, um de cada vez.

| Pacote | Tamanho | O que traz |
| --- | --- | --- |
| `AstroUI_1_Arte.unitypackage` | 0,55 MB | `Assets/Art/ui` (37 peças `sf_*` do kit Sci-Fi, `ItemButton04White_2`, `ic_evoluir`, `ic_loja`), 53 ícones `ic_*` de `Assets/Art`, `ui_wire_*`, `PictoIcon_*`, formas base (`hex_disc`, `ring`, `diamond`, `poly_disc`, `wire_frame`, `dashed_ring`, `dash_line` + `DashLine.mat`) e o guia |
| `AstroUI_2_Fontes.unitypackage` | 24 MB | Rajdhani SemiBold + `Resources/Fontes/Latin.asset`, com a `CJK.asset` (atlas estático) já como fallback, e a `NotoSansCJK-VF.ttc` para regerar o atlas |
| `AstroUI_3_Codigo.unitypackage` | 0,07 MB | `Brilho`, `FormaVetorial`, `Juice` (+ `MolaDoBotao`), `SafeAreaFitter`, `EscalaPorAspecto`, `GiroDeAnel`, `UISounds`, o LeanTween (`Assets/LeanTween/Framework`), `Kit/Palette.cs` e `Kit/AudioFX.cs` |

## Antes de importar o código

- **TextMeshPro** (vem com o pacote uGUI da Unity 6) e **True Shadow** (Le Tai, Asset Store, pago) precisam estar no projeto. `Brilho` e `FormaVetorial` usam o True Shadow.
- O código foi compilado isolado contra UnityEngine, TMP, True Shadow e LeanTween, sem nenhum outro script do jogo: compila limpo.
- O namespace continua `AstroSurvivor`, para que código copiado depois do jogo antigo encaixe sem troca.
- `Kit/AudioFX.cs` é uma **ponte**: no Astro Survivor o `AudioFX` é o sintetizador do jogo. Aqui ele só expõe quatro ganchos. Ligue-os ao áudio do jogo novo, por exemplo `AudioFX.Clique = () => MeuAudio.Tocar("click");`. Sem isso a UI funciona muda.
- `Kit/Palette.cs` é a cópia da `Palette` do `Core.cs`. Se um dia o `Core.cs` inteiro for copiado, apague este arquivo.

## O que ficou de fora e por quê

- `VooDeMinerio`, `BarraDeXP`, `SlotDeUpgrade`, `SpriteFactory`, `FontRegistry`: dependem de `UIManager`, `MetaProgress`, `Haptics`, `GameSettings`, `WeaponSystem`, `Loc` e das naves. Servem de referência para reescrever no jogo novo.
- `Editor/KitSciFi.cs` e `Editor/SetupUI.*`: geram o kit a partir do SCI-FI UI Pack Pro (928 MB, fora do build) e vestem os prefabs do Astro Survivor. Os sprites já gerados estão no pacote de arte.
- `UIRoot.prefab`: é o jogo, não o sistema.
- Shift UI e Hovl: pacotes pagos da Asset Store. A Rajdhani (licença OFL) foi a única coisa tirada do Shift.

## Licenças

Rajdhani e Noto Sans CJK são OFL. LeanTween é MIT. As peças `sf_*` e `ItemButton04White_2` derivam do SCI-FI UI Pack Pro, e os `PictoIcon_*` de um pack de ícones da Asset Store: confira se a licença cobre o jogo novo (a licença padrão da Asset Store é por usuário e vale para vários projetos dele).
