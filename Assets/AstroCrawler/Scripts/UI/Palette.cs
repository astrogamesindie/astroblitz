// AstroCrawler - paleta normativa herdada do Astro Survivor (PILARES_DE_UI.md, seção 3).
//
// Seis cores com papel fixo e uma exceção. O código da interface usa os apelidos
// de UIFactory (Accent, Danger, Good...), que apontam para cá; cor nova de UI
// entra aqui com um papel, nunca como hex solto numa tela.

using UnityEngine;

namespace AstroCrawler
{
    public static class Palette
    {
        /// <summary>#05060A — fundo; miolo de painéis.</summary>
        public static readonly Color Vazio = Hex(0x05, 0x06, 0x0A);

        /// <summary>#E8F4FF — traço primário, HUD, texto, carta comum.</summary>
        public static readonly Color Fosforo = Hex(0xE8, 0xF4, 0xFF);

        /// <summary>#00E5FF — jogador, XP, aliado, botões do menu, carta rara.</summary>
        public static readonly Color Ciano = Hex(0x00, 0xE5, 0xFF);

        /// <summary>#FF2D95 — dano, perigo, chefe, carta de evolução, badges.</summary>
        public static readonly Color Magenta = Hex(0xFF, 0x2D, 0x95);

        /// <summary>#FFC24B — minério/créditos, recompensa, o botão JOGAR, compra possível.</summary>
        public static readonly Color Ambar = Hex(0xFF, 0xC2, 0x4B);

        /// <summary>#7CFF4B — cura, confirmação, vitória, oferta da loja.</summary>
        public static readonly Color Acido = Hex(0x7C, 0xFF, 0x4B);

        /// <summary>#FF2D4B — só a vida crítica. O jogo tem um vermelho só.</summary>
        public static readonly Color Critico = Hex(0xFF, 0x2D, 0x4B);

        /// <summary>Texto secundário: Fósforo apagado sobre o Vazio (opaco, para não somar alfa). 0,8 desde a revisão de legibilidade (contraste ≈ 11:1).</summary>
        public static readonly Color FosforoDim = Color.Lerp(Vazio, Fosforo, 0.8f);

        /// <summary>
        /// Miolo de vidro tingido por um destaque: quase o Vazio, com uma lasca
        /// da cor da moldura. É o "fill" dos botões e folhas recortadas.
        /// </summary>
        public static Color Vidro(Color destaque, float forca, float alfa = 1f)
        {
            var c = Color.Lerp(Vazio, destaque, forca);
            c.a = alfa;
            return c;
        }

        public static Color Alfa(Color c, float alfa)
        {
            c.a = alfa;
            return c;
        }

        private static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
