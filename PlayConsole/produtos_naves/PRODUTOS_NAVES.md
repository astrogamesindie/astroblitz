# Produtos das naves — Play Console e App Store Connect

Quatro produtos de compra única (não consumíveis): Bastião Aegis, Espectro Nulo, Aurora Médica e Orion.
Os mesmos IDs servem nas duas lojas. Depois de criado, o ID não pode ser mudado nem reutilizado.

Ícones nesta pasta (PNG 32 bits, 1:1, sem texto, sem marca):
- `ship_<id>_1024.png` — Play Console (aceita de 512 a 1080 px) e imagem promocional da App Store (1024 × 1024).
- `ship_<id>_512.png` — opção menor para a Play.

> O jogo vende essas naves de dois jeitos: por **1.000 créditos** (`ShipUnlocks.Price`) ou com dinheiro real (botão ao lado, `ShipPurchases`, Google Play Billing / StoreKit 2). "Restaurar compras" fica na janela Premium. Os IDs abaixo precisam ser idênticos aos do código (`ship_` + id da nave).

---

## Google Play Console — Produtos únicos

Categoria fiscal: **Vendas de apps digitais**. Restrição de local: nenhuma. Classificação indicativa: a mesma do app.

### 1. Bastião Aegis
- **ID do produto:** `ship_bastion`
- **Tags:** `ship`, `ship-unlock`, `bastion`
- **Nome (en-US):** Aegis Bastion Ship
- **Descrição (en-US):** Unlock the Aegis Bastion: the toughest hull in the fleet. Emergency Aegis makes it invulnerable for 4s, and its exclusive cards add repulsion, debris and armor. Yours forever.
- **Nome (pt-BR):** Nave Bastião Aegis
- **Descrição (pt-BR):** Libere a Bastião Aegis, o casco mais resistente da frota. A Égide de Emergência deixa a nave invulnerável por 4s e as cartas exclusivas dão repulsão, destroços e blindagem. Para sempre.
- **Ícone:** `ship_bastion_1024.png`

### 2. Espectro Nulo
- **ID do produto:** `ship_spectre`
- **Tags:** `ship`, `ship-unlock`, `spectre`
- **Nome (en-US):** Null Specter Ship
- **Descrição (en-US):** Unlock the Null Specter, an experimental prototype that bends space. Temporal Stasis freezes every enemy, bosses included, for 3s. Exclusive cards: crits, slow and paralysis. Yours forever.
- **Nome (pt-BR):** Nave Espectro Nulo
- **Descrição (pt-BR):** Libere a Espectro Nulo, um protótipo que dobra o espaço. A Estase Temporal congela todos os inimigos, até o chefe, por 3s. Cartas exclusivas de crítico, lentidão e paralisia. Para sempre.
- **Ícone:** `ship_spectre_1024.png`

### 3. Aurora Médica
- **ID do produto:** `ship_aurora`
- **Tags:** `ship`, `ship-unlock`, `aurora`
- **Nome (en-US):** Medical Aurora Ship
- **Descrição (en-US):** Unlock the Medical Aurora, a support ship that rebuilds its own hull. Restorative Repair heals 30% of max HP instantly. Exclusive cards: self-repair, healing missiles and drones. Yours forever.
- **Nome (pt-BR):** Nave Aurora Médica
- **Descrição (pt-BR):** Libere a Aurora Médica, uma nave de suporte que reconstrói o próprio casco. O Reparo Restaurador cura 30% do HP na hora. Cartas exclusivas de autorreparo, mísseis e drones. Para sempre.
- **Ícone:** `ship_aurora_1024.png`

### 4. Orion
- **ID do produto:** `ship_orion`
- **Tags:** `ship`, `ship-unlock`, `orion`
- **Nome (en-US):** Orion Ship
- **Descrição (en-US):** Unlock the Orion, a heavy-cannon artillery ship where every shot counts. Damage Amplification doubles cannon damage for 5s. Exclusive cards: chain lightning, siege shots, boss damage. Yours forever.
- **Nome (pt-BR):** Nave Orion
- **Descrição (pt-BR):** Libere a Orion, uma nave de artilharia com canhões pesados. A Amplificação de Dano dobra o dano do canhão por 5s. Cartas exclusivas de raios em cadeia, cerco e dano em chefes. Para sempre.
- **Ícone:** `ship_orion_1024.png`

Tamanhos conferidos: nomes de 10 a 19 caracteres (limite 55); descrições de 175 a 198 (mínimo 50, limite 200).

---

## App Store Connect — Compras dentro do app

Tipo: **Não consumível**. Nome de referência é interno (não aparece para o jogador).

| Nome de referência | ID do produto | Nome de exibição (en-US, ≤ 35) | Descrição (en-US, ≤ 55) |
|---|---|---|---|
| Ship - Aegis Bastion | `ship_bastion` | Aegis Bastion Ship | Toughest hull, 4s invulnerability special. |
| Ship - Null Specter | `ship_spectre` | Null Specter Ship | Freezes every enemy, bosses included, for 3s. |
| Ship - Medical Aurora | `ship_aurora` | Medical Aurora Ship | Support ship that heals 30% of its hull at once. |
| Ship - Orion | `ship_orion` | Orion Ship | Heavy cannons; doubles its damage for 5s. |

Localização pt-BR (opcional):

| ID | Nome de exibição | Descrição |
|---|---|---|
| `ship_bastion` | Nave Bastião Aegis | Casco mais resistente, fica invulnerável por 4s. |
| `ship_spectre` | Nave Espectro Nulo | Congela todos os inimigos, até o chefe, por 3s. |
| `ship_aurora` | Nave Aurora Médica | Nave de suporte que cura 30% do casco na hora. |
| `ship_orion` | Nave Orion | Canhões pesados; dobra o dano por 5s. |

- **Imagem promocional (opcional):** `ship_<id>_1024.png`.
- **Captura para revisão (obrigatória):** a Apple pede um print **real do app** mostrando a compra (por exemplo, o hangar com o botão de comprar a nave). Não foi gerada aqui: as artes de `PlayConsole/screenshots` são material de divulgação, tire o print do hangar com o botão de preço visível num aparelho com a conta Sandbox.
- **Observações para revisão (sugestão):** "Unlocks the ship permanently. In the app: Hangar > select the ship > Buy. Purchases can be restored in Settings > Restore purchases."

---

## Preço

A decisão é sua. Referência de mercado para naves/personagens permanentes em jogos idle: entre US$ 0,99 e US$ 2,99 cada (por exemplo, R$ 4,99 a R$ 14,99). Mantenha os quatro no mesmo preço se o jogo continuar tratando as naves como equivalentes (hoje todas custam 1.000 créditos).
