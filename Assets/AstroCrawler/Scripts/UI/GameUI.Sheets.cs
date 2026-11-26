// AstroCrawler - the cut-corner sheet shared by the modal screens.
//
// The login prompt set the look the other screens follow (settings, rewards,
// store, hangar actions): a cyan cut-corner border around dark teal glass, a
// soft gradient and top light, a short rule at the top, and wide actions in the
// cut frame with an icon, a lit bar and an arrow. The pause menu and the daily
// login are built from these pieces so they cannot drift from it.

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private static readonly Color SheetGlass = Palette.Vidro(Palette.Ciano, 0.07f);

        private static Sprite CutFrameSprite { get { return ArtLibrary.Ui("Cut Frame Filled Big (300ppu)"); } }

        // ------------------------------------------------------------------
        // Molduras do kit Sci-Fi do Astro Survivor (PILARES_DE_UI §5)
        //
        // Cada papel tem a "linha" (traço, na cor de destaque) e o "vidro" (miolo
        // escuro, vazado onde passa o traço), no mesmo retângulo, sem recuo.
        // Só as telas de menu/meta usam o kit: a gameplay (HUD, escolha de
        // melhoria, cartas em jogo, botão da habilidade e pausa) mantém a
        // moldura recortada original, por decisão do dono (23/11/2026).
        // ------------------------------------------------------------------

        internal enum KitRole { Botao, Icone, Card, Painel, Faixa }

        /// <summary>Escala do 9-slice das peças do kit (100 ppu): 1 = traço fino, como no Survivor.</summary>
        private const float KitPixelsPerUnit = 1f;

        private static string KitName(KitRole role)
        {
            switch (role)
            {
                case KitRole.Icone: return "icone";
                case KitRole.Card: return "card";
                case KitRole.Painel: return "painel";
                case KitRole.Faixa: return "faixa";
                default: return "botao";
            }
        }

        /// <summary>
        /// Veste um par borda + vidro com a peça do kit. A borda vira a "linha";
        /// o vidro (filho) passa a ocupar o mesmo retângulo. Cores e hierarquia
        /// não mudam. Devolve false (e não mexe em nada) na gameplay ou se o
        /// sprite faltar, para o chamador manter o ajuste da moldura antiga.
        /// </summary>
        internal static bool ApplyKitFrame(Image line, Image glass, KitRole role)
        {
            if (UIFactory.KeepGameplayFrames || line == null) return false;
            var name = KitName(role);
            var lineSprite = ArtLibrary.Ui("Kit/sf_" + name + "_linha");
            var glassSprite = ArtLibrary.Ui("Kit/sf_" + name + "_vidro");
            if (lineSprite == null || glassSprite == null) return false;

            line.sprite = lineSprite;
            line.type = Image.Type.Sliced;
            line.pixelsPerUnitMultiplier = KitPixelsPerUnit;
            if (glass != null)
            {
                glass.sprite = glassSprite;
                glass.type = Image.Type.Sliced;
                glass.pixelsPerUnitMultiplier = KitPixelsPerUnit;
                UIFactory.Stretch(glass.rectTransform, 0f);
            }
            return true;
        }

        /// <summary>Atalho para os pares criados com Panel9: procura o vidro pelo nome do filho.</summary>
        internal static bool ApplyKitFrame(Image line, string glassChild, KitRole role)
        {
            if (line == null) return false;
            var child = line.transform.Find(glassChild);
            return ApplyKitFrame(line, child != null ? child.GetComponent<Image>() : null, role);
        }

        /// <summary>
        /// A centred cut-corner sheet. Returns the border image; its "Surface"
        /// child is the glass (masked, so the light cannot square the corners).
        /// </summary>
        private static Image BuildCutSheet(Transform parent, string name, Vector2 size, Color accent)
        {
            var frame = CutFrameSprite;
            var panel = UIFactory.Panel9(parent, name, accent, frame);
            panel.pixelsPerUnitMultiplier = 0.2f;
            panel.raycastTarget = true;
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            UIFactory.DropShadow(panel, 34f, 12f, 0.6f);
            UIFactory.GlowShadow(panel, WithAlpha(accent, 0.22f), 34f, 0.06f);

            var surface = UIFactory.Panel9(panel.transform, "Surface", SheetGlass, frame);
            UIFactory.Stretch(surface.rectTransform, 8f);
            surface.pixelsPerUnitMultiplier = 0.2f;
            surface.raycastTarget = false;
            surface.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            ApplyKitFrame(panel, surface, KitRole.Painel);

            var gradient = UIFactory.Rect(surface.transform, "Gradient", new Color(0.06f, 0.15f, 0.18f, 0.55f),
                ArtLibrary.GradientPanel);
            UIFactory.Stretch(gradient.rectTransform, 6f);
            gradient.raycastTarget = false;
            SweepGlow(surface.rectTransform, "TopLight", true, 160f, WithAlpha(accent, 0.06f));

            var topRule = UIFactory.Rect(panel.transform, "TopRule", WithAlpha(accent, 0.6f), ArtLibrary.Pixel);
            UIFactory.Place(topRule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(260f, 5f));
            topRule.raycastTarget = false;
            return panel;
        }

        /// <summary>Recolours a sheet's border, its top rule and glow (e.g. red while asking to confirm).</summary>
        private static void TintSheet(Image sheet, Color accent)
        {
            if (sheet == null) return;
            sheet.color = accent;
            var rule = sheet.transform.Find("TopRule");
            if (rule != null) rule.GetComponent<Image>().color = WithAlpha(accent, 0.6f);
            var shadows = sheet.GetComponents<LeTai.TrueShadow.TrueShadow>();
            for (var i = 0; i < shadows.Length; i++)
            {
                // The drop shadow stays black; only the coloured glow follows the accent.
                if (shadows[i].Color.r + shadows[i].Color.g + shadows[i].Color.b > 0.05f)
                    shadows[i].Color = WithAlpha(accent, 0.22f);
            }
        }

        /// <summary>
        /// A wide sheet action, as on the login prompt: cut frame in the border
        /// colour over a filled inset, the icon on the left after a lit bar, the
        /// caption, and an arrow on the right when it leads somewhere.
        /// </summary>
        private static Button CutSheetAction(Transform parent, string name, Sprite icon, Color border, Color fill,
            Vector2 size, Sound sound, bool arrow = false, int fontSize = UIFactory.TextBody)
        {
            var button = UIFactory.Button(parent, name, "", fill, UIFactory.Ink, fontSize, sound);
            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            StyleCutAction(button, CutFrameSprite, border, fill);

            var bar = UIFactory.Rect(rect, "Accent", border, ArtLibrary.Pixel);
            UIFactory.Place(bar.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(4f, size.y * 0.68f));
            bar.raycastTarget = false;

            var left = 40f;
            if (icon != null)
            {
                var glyph = UIFactory.Rect(rect, "Glyph", UIFactory.Ink, icon);
                // Ícone maior (44 → 56): a ação se lê pelo ícone antes do texto.
                UIFactory.Place(glyph.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(56f, 56f));
                glyph.preserveAspect = true;
                glyph.raycastTarget = false;
                left = 100f;
            }
            var right = 40f;
            if (arrow)
            {
                var next = UIFactory.Rect(rect, "Arrow", UIFactory.Ink, ArtLibrary.IconNext);
                UIFactory.Place(next.rectTransform, new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(36f, 36f));
                next.preserveAspect = true;
                next.raycastTarget = false;
                right = 76f;
            }
            var caption = rect.Find("Caption") as RectTransform;
            if (caption != null)
            {
                caption.offsetMin = new Vector2(left, 10f);
                caption.offsetMax = new Vector2(-right, -10f);
                var label = caption.GetComponent<TMP_Text>();
                label.characterSpacing = 4f;
                label.enableAutoSizing = true;
                label.fontSizeMin = UIFactory.TextMin;
                label.fontSizeMax = fontSize;
            }
            return button;
        }

        /// <summary>
        /// Contagem regressiva com ícone de relógio no lugar da frase ("Disponível
        /// em", "Nova recompensa em"): o relógio fica colado à esquerda do texto
        /// centralizado. Texto vazio esconde o relógio; texto comum (sem tempo)
        /// passa por <see cref="HideTimerClock"/>.
        /// </summary>
        private static void SetTimerText(TMP_Text label, string time)
        {
            if (label == null) return;
            label.text = time;
            var clock = label.transform.Find("Clock") as RectTransform;
            if (clock == null) clock = BuildClockGlyph(label.rectTransform);
            var show = !string.IsNullOrEmpty(time);
            clock.gameObject.SetActive(show);
            if (!show) return;
            var size = Mathf.Max(32f, label.fontSize * 1.05f);
            clock.sizeDelta = new Vector2(size, size);
            var width = Mathf.Min(label.GetPreferredValues(time).x, label.rectTransform.rect.width);
            clock.anchoredPosition = new Vector2(-width * 0.5f - size * 0.5f - 10f, 0f);
            foreach (var image in clock.GetComponentsInChildren<Image>(true)) image.color = label.color;
        }

        private static void HideTimerClock(TMP_Text label)
        {
            var clock = label != null ? label.transform.Find("Clock") : null;
            if (clock != null) clock.gameObject.SetActive(false);
        }

        /// <summary>Relógio desenhado com o anel redondo e dois ponteiros (não há ícone de relógio no projeto).</summary>
        private static RectTransform BuildClockGlyph(RectTransform parent)
        {
            var clock = UIFactory.NewRect(parent, "Clock");
            clock.anchorMin = clock.anchorMax = new Vector2(0.5f, 0.5f);
            clock.pivot = new Vector2(0.5f, 0.5f);
            var ring = UIFactory.Rect(clock, "Ring", UIFactory.Ink, ArtLibrary.RoundRing);
            UIFactory.Stretch(ring.rectTransform);
            ring.raycastTarget = false;
            var hour = UIFactory.Rect(clock, "Hour", UIFactory.Ink, ArtLibrary.Pixel);
            hour.rectTransform.anchorMin = hour.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            hour.rectTransform.pivot = new Vector2(0.5f, 0f);
            hour.rectTransform.anchoredPosition = Vector2.zero;
            hour.rectTransform.sizeDelta = new Vector2(4f, 12f);
            hour.raycastTarget = false;
            var minute = UIFactory.Rect(clock, "Minute", UIFactory.Ink, ArtLibrary.Pixel);
            minute.rectTransform.anchorMin = minute.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            minute.rectTransform.pivot = new Vector2(0f, 0.5f);
            minute.rectTransform.anchoredPosition = Vector2.zero;
            minute.rectTransform.sizeDelta = new Vector2(10f, 4f);
            minute.raycastTarget = false;
            return clock;
        }

        /// <summary>A small cut-corner tag (dark glass, lit text) that sits on a sheet's top edge.</summary>
        private static Image CutTag(Transform parent, string name, Sprite icon, Color accent, Vector2 size)
        {
            var tag = UIFactory.Pill(parent, name, "", new Color(0.04f, 0.1f, 0.16f, 1f), accent, size.y, 22, icon);
            tag.sprite = CutFrameSprite;
            tag.type = Image.Type.Sliced;
            tag.pixelsPerUnitMultiplier = 0.45f;
            var border = UIFactory.Panel9(tag.transform, "Border", WithAlpha(accent, 0.75f), ArtLibrary.FrameOutline);
            UIFactory.Stretch(border.rectTransform);
            border.raycastTarget = false;
            border.pixelsPerUnitMultiplier = 0.5f;
            UIFactory.GlowShadow(tag, WithAlpha(accent, 0.45f), 18f, 0.06f);
            return tag;
        }
    }
}
