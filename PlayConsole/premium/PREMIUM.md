# Premium — Play Console e App Store Connect

Compra única, permanente e **não consumível**. É o produto principal do jogo; as naves (`ship_*`) e o Starter Pack continuam à venda.

O que o Premium entrega (conferido no código em 12/11/2026):
- **Sem anúncios:** banner, intersticial, app open e rewarded deixam de aparecer (`AdsPolicy.CanShowAds`).
- **Recompensas sem vídeo:** os três botões que antes pediam um vídeo passam a entregar direto (`GameUI.PremiumRewards`):
  - **Reviver** na derrota: continua **uma vez por partida** (não vale no modo infinito).
  - **+100 minérios** na aba Recompensas: continua com espera de **5 minutos** entre usos.
  - **Recompensa diária x2**: dobra sem vídeo, uma vez por dia.

> Não prometa "recompensas ilimitadas" nem "renascer sempre": os limites acima continuam valendo.

- **ID do produto:** Google Play `remove_ads` (campo `googlePlayProductId`), App Store `premium` (campo `removeAdsProductId`), ambos em `Database/AndroidStore.asset`; cada um tem de ser **idêntico** ao cadastrado na sua loja.
- **Ícone:** `premium_1024.png` (Play aceita de 512 a 1080 px; imagem promocional da App Store 1024 × 1024) e `premium_512.png`. PNG 32 bits, opaco, 1:1, sem texto. Mesma coroa do botão Premium do jogo. Gerado por `Tools/play_store/build_premium_icon.py`.

---

## Google Play Console — Produtos únicos

Categoria fiscal: **Vendas de apps digitais**. Sem restrição de local. Opção de compra: **permanente** (não usar aluguel).

- **ID do produto:** `remove_ads`
- **Tags:** `premium`, `no-ads`
- **Nome (en-US):** Premium: No Ads + Free Rewards
- **Descrição (en-US):** No more ads: no banners, no interstitials, no videos. Revive once per run, claim +100 ore and double your daily reward without watching anything. One purchase, yours forever.
- **Nome (pt-BR):** Premium: Sem Anúncios + Recompensas
- **Descrição (pt-BR):** Chega de anúncios: sem banner, sem tela cheia, sem vídeo. Reviva uma vez por partida, pegue +100 minérios e dobre a recompensa diária sem assistir nada. Compra única, para sempre.

Tamanhos conferidos: nomes com 30 e 35 caracteres (limite 55); descrições com 174 e 179 (mínimo 50, limite 200). App Store: 30/46 (en) e 21/51 (pt).

---

## App Store Connect — Compras dentro do app

Tipo: **Não consumível**. Nome de referência é interno.

| Nome de referência | ID do produto | Nome de exibição (en-US, ≤ 35) | Descrição (en-US, ≤ 55) |
|---|---|---|---|
| Premium | `premium` | Premium: No Ads + Free Rewards | No ads. Revive and get rewards without videos. |

Localização pt-BR:

| ID | Nome de exibição (≤ 35) | Descrição (≤ 55) |
|---|---|---|
| `premium` | Premium: Sem Anúncios | Sem anúncios. Reviva e ganhe recompensas sem vídeo. |

- **Imagem promocional (opcional):** `premium_1024.png`.
- **Captura para revisão (obrigatória):** print real do app com a janela PREMIUM aberta e o preço no botão (aparelho com conta Sandbox). Não gerada aqui.
- **Observações para revisão (sugestão):** "Main menu > crown button (Premium) > buy. Removes all ads and grants the rewarded-video rewards without a video (revive once per run, +100 ore every 5 minutes, daily reward x2). Restore: same window > Restore purchases."

---

## No jogo

Janela PREMIUM (`UI/GameUI.Store.cs`), traduzida nos 10 idiomas:
- pt: "Compra única e permanente. Sem anúncios e recompensas sem vídeo."
- en: "One permanent purchase. No ads, and rewards without videos."
- es: "Compra única y permanente. Sin anuncios y recompensas sin videos."

---

## Preço

A decisão é sua. Referência: "remover anúncios" em jogos casuais costuma ficar entre US$ 2,99 e US$ 4,99 (R$ 9,99 a R$ 24,99). Como agora inclui as recompensas sem vídeo, faz sentido ficar acima de uma nave avulsa.
