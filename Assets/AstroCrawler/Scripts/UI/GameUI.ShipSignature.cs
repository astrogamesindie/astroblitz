// AstroCrawler - hangar: each ship card names its signature passive
// (PlayerShip.Signature) in a box beside the deck button.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private static bool ShipPassiveText(string shipId, out string name, out string description)
        {
            switch (shipId)
            {
                case "vanguard":
                    name = L("CALOR DE FOCO", "FOCUS HEAT", "CALOR DE ENFOQUE");
                    description = L("Tiros seguidos no mesmo alvo esquentam: até +40% de dano.",
                        "Hits on the same target heat up: up to +40% damage.",
                        "Disparos seguidos al mismo objetivo se calientan: hasta +40% de daño.");
                    return true;
                case "bastion":
                    name = L("CANHÃO DE IMPACTO", "IMPACT CANNON", "CAÑÓN DE IMPACTO");
                    description = L("Tiros empurram. 3 placas orbitais atiram em quem chegar perto.",
                        "Shots knock back. 3 orbiting plates fire at anything close.",
                        "Los disparos empujan. 3 placas orbitales disparan a lo que se acerque.");
                    return true;
                case "spectre":
                    name = L("TIRO DE FASE", "PHASE SHOT", "DISPARO DE FASE");
                    description = L("Atravessa, deixa lento e se repete em eco. Alvo lento leva crítico.",
                        "Pierces, slows and echoes. Slowed targets take crits.",
                        "Atraviesa, ralentiza y se repite en eco. Objetivo lento recibe crítico.");
                    return true;
                case "aurora":
                    name = L("SUTURA", "SUTURE", "SUTURA");
                    description = L("Mira o mais fraco. Abates soltam orbes de cura; o excesso vira escudo.",
                        "Aims at the weakest. Kills drop healing orbs; overheal becomes a shield.",
                        "Apunta al más débil. Las bajas sueltan orbes de curación; el exceso es escudo.");
                    return true;
                case "orion":
                    name = L("ARTILHARIA DE CERCO", "SIEGE ARTILLERY", "ARTILLERÍA DE ASEDIO");
                    description = L("Mira o mais forte. A cada 5 tiros, um disparo carregado explode.",
                        "Aims at the strongest. Every 5th shot is a charged blast.",
                        "Apunta al más fuerte. Cada 5 disparos, uno cargado explota.");
                    return true;
                default:
                    name = description = "";
                    return false;
            }
        }

        /// <summary>
        /// Builds (once, at runtime: the cards are baked) the passive box to the
        /// left of the deck button and fills it. Ships without a passive keep the
        /// deck button centred.
        /// </summary>
        private void RefreshShipPassive(RectTransform panel, ShipDefinition ship)
        {
            string name, description;
            var has = ShipPassiveText(ship.id, out name, out description);
            var deck = panel.Find("ViewDeck") as RectTransform;
            var box = panel.Find("Passive") as RectTransform;
            if (deck != null)
                UIFactory.Place(deck, new Vector2(0.5f, 1f), new Vector2(has ? 186f : 0f, -656f), new Vector2(140f, 84f));
            if (!has)
            {
                if (box != null) box.gameObject.SetActive(false);
                return;
            }

            var accent = SpriteFactory.ParseColor(ship.tint);
            if (box == null)
            {
                var frame = UIFactory.Panel9(panel, "Passive", new Color(0.035f, 0.045f, 0.085f, 0.95f));
                frame.raycastTarget = false;
                box = frame.rectTransform;
                UIFactory.Place(box, new Vector2(0.5f, 1f), new Vector2(-76f, -656f), new Vector2(360f, 84f));
                var rail = UIFactory.Rect(box, "Rail", accent, ArtLibrary.Pixel);
                UIFactory.Place(rail.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(5f, 72f));
                rail.raycastTarget = false;

                var title = UIFactory.Label(box, "Name", "", 26, accent, TextAlignmentOptions.TopLeft, true);
                UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -6f), new Vector2(334f, 30f));
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.enableAutoSizing = true;
                title.fontSizeMin = 20f;
                title.fontSizeMax = 22f;
                title.characterSpacing = 2f;
                UIFactory.AddUnderlay(title);

                var body = UIFactory.Label(box, "Description", "", 22, UIFactory.Ink, TextAlignmentOptions.TopLeft);
                UIFactory.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -36f), new Vector2(334f, 44f));
                body.enableAutoSizing = true;
                body.fontSizeMin = 18f;
                body.fontSizeMax = 18f;
                body.lineSpacing = -6f;
            }
            box.gameObject.SetActive(true);
            SetLabel(box, "Name", L("PASSIVA · ", "PASSIVE · ", "PASIVA · ") + name);
            SetLabel(box, "Description", description);
        }
    }
}
