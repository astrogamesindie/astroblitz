# Starter Pack — Play Console e App Store Connect

Oferta de compra única: **1 nave aleatória** (Bastião Aegis, Espectro Nulo, Aurora Médica ou Orion, entre as que o jogador ainda não tem) **+ 3.000 minérios**. Se o jogador já tiver as quatro naves, recebe +4.000 minérios (o preço de uma nave) no lugar da nave.

No jogo, o botão especial aparece no menu principal (coluna da direita, abaixo do presente) com uma contagem regressiva de 24 h, que começa na primeira vez que o menu é aberto. Depois de comprado ou depois das 24 h, o botão some.

- **ID do produto (as duas lojas):** `starter_pack` (tem de ser idêntico; está em `StarterPack.ProductId`).
- **Ícone:** `starter_pack_1024.png` (Play aceita de 512 a 1080 px; imagem promocional da App Store 1024 × 1024) e `starter_pack_512.png`. PNG 32 bits, opaco, sem texto.

---

## Google Play Console — Produtos únicos

Categoria fiscal: **Vendas de apps digitais**. Sem restrição de local.

- **ID do produto:** `starter_pack`
- **Tags:** `starter-pack`, `bundle`, `ship`
- **Nome (en-US):** Starter Pack: Random Ship + 3,000 Ore
- **Descrição (en-US):** Start strong: get one random ship you don't own yet (Aegis Bastion, Null Specter, Medical Aurora or Orion) plus 3,000 ore for upgrades. One purchase per account.
- **Nome (pt-BR):** Starter Pack: Nave Aleatória + 3.000 Minérios
- **Descrição (pt-BR):** Comece forte: ganhe uma nave aleatória que você ainda não tem (Bastião Aegis, Espectro Nulo, Aurora Médica ou Orion) e 3.000 minérios para melhorias. Uma vez por conta.

---

## App Store Connect — Compras dentro do app

Tipo: **Não consumível** (compra única por conta, restaurável pelo botão "Restaurar compras" da janela Premium).

| Nome de referência | ID do produto | Nome de exibição (en-US, ≤ 35) | Descrição (en-US, ≤ 55) |
|---|---|---|---|
| Starter Pack | `starter_pack` | Starter Pack | A random ship plus 3,000 ore. One-time offer. |

Localização pt-BR:

| ID | Nome de exibição | Descrição |
|---|---|---|
| `starter_pack` | Starter Pack | Uma nave aleatória e 3.000 minérios. Oferta única. |

- **Imagem promocional (opcional):** `starter_pack_1024.png`.
- **Captura para revisão (obrigatória):** print real do app com a janela do Starter Pack aberta e o preço visível (num aparelho com conta Sandbox).
- **Observações para revisão (sugestão):** "Main menu > pink OFFER button (top right) > buy. Grants one random ship the player does not own and 3,000 in-game ore, once per account. Restore: main menu > crown (Premium) > Restore purchases. The offer button is shown for 24 hours after first launch."

> Atenção à revisão da Apple: a contagem de 24 h é real (o botão some depois), então a descrição não promete nada falso. A Apple pode pedir que o produto seja **consumível**, por conter moeda do jogo. O código trata o pacote como compra única: se for cadastrado como consumível, o "Restaurar" não o devolve, mas o pacote continua sendo entregue uma vez só.

---

## Preço

A decisão é sua. Referência: starter packs de jogos idle costumam custar entre US$ 0,99 e US$ 4,99. Aqui o pacote vale uma nave (vendida avulsa nas lojas) mais 3.000 minérios (quase uma nave comprada com minérios, que custa 4.000). Um preço acima do de uma nave avulsa (por exemplo, US$ 2,99 ou R$ 14,99) deixa a oferta atraente sem desvalorizar as naves.
