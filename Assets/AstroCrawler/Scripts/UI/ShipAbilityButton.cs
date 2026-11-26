using LeTai.TrueShadow;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    // Reusable prefab host; children are built once using the project's UI theme.
    //
    // The recharge is the button itself filling up: a radial sweep, clockwise from the top,
    // over a dark face. When it closes the circle the ability is ready and the
    // button lights up.
    public sealed class ShipAbilityButton : MonoBehaviour
    {
        /// <summary>Used only when there is no ship to take a colour from.</summary>
        public static readonly Color AbilityColor = new Color(1f, 0.8f, 0.15f);

        /// <summary>
        /// The ability's colour is the ship's own, a little softer so it does
        /// not shout over the arena: 10% less saturation and 8% less brightness.
        /// </summary>
        public static Color ColorFor(ShipDefinition ship)
        {
            if (ship == null) return AbilityColor;
            float hue, saturation, value;
            Color.RGBToHSV(SpriteFactory.ParseColor(ship.tint), out hue, out saturation, out value);
            return Color.HSVToRGB(hue, saturation * 0.9f, value * 0.92f);
        }

        /// <summary>
        /// The symbol of each ship's power, shared by this button, the details
        /// card and the hangar: the engine for Overdrive, the shield for Aegis,
        /// frozen time for Stasis, a heart for Repair, the blaster for Amplify.
        /// </summary>
        public static Sprite IconFor(ShipSpecialKind kind)
        {
            switch (kind)
            {
                case ShipSpecialKind.Repair: return ArtLibrary.IconLife;
                case ShipSpecialKind.ChristmasStar: return SpriteFactory.ChristmasStar();
                case ShipSpecialKind.Aegis: return ArtLibrary.IconShield;
                case ShipSpecialKind.Stasis: return CardIconFactory.Get(CardEffectId.TemporalParalysis);
                case ShipSpecialKind.Overdrive: return CardIconFactory.Get(CardEffectId.EngineOverload);
                case ShipSpecialKind.Amplify: return ArtLibrary.PassiveIcon(PassiveUpgrade.Damage);
                default: return ArtLibrary.IconEnergy;
            }
        }

        /// <summary>Card artwork has uneven margins; nudges it like the cards do.</summary>
        public static void CenterIcon(Image image, ShipSpecialKind kind)
        {
            if (image == null) return;
            if (kind == ShipSpecialKind.Overdrive)
                CardIconFactory.CenterArtwork(image, CardEffectId.EngineOverload, Vector2.zero);
            else if (kind == ShipSpecialKind.Stasis)
                CardIconFactory.CenterArtwork(image, CardEffectId.TemporalParalysis, Vector2.zero);
            else
                image.rectTransform.anchoredPosition = Vector2.zero;
        }

        private const float ButtonSize = 156f;
        // Height / width of the "Icon Button 04" hexagon artwork (225 x 203).
        private const float HexAspect = 225f / 203f;
        private static readonly Color EmptyFace = new Color(0.05f, 0.07f, 0.12f, 0.92f);

        private static Color WithAlpha(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

        private RunController run;
        private Button button;
        private CanvasGroup buttonGroup;
        private Image icon;
        private Image face;
        private Image frame;
        private Image charge;
        private TrueShadow glow;
        private Color color = AbilityColor;
        private ShipSpecialDefinition shownSpecial;
        private bool shownReady;

        public void Bind(RunController controller)
        {
            run = controller;
            if (button == null)
            {
                var root = (RectTransform)transform;
                UIFactory.Place(root, new Vector2(1f, 0f), new Vector2(-32f, 32f), new Vector2(260f, 226f));

                // Gameplay: mantém a moldura original (o kit Sci-Fi veste só os menus).
                using (UIFactory.GameplayFrames())
                {
                    button = UIFactory.RoundButton(root, "Activate", ArtLibrary.IconEnergy,
                        AbilityColor, ButtonSize, Sound.Tap);
                }
                // A hexagon ("Icon Button 04"), taller than wide like the artwork.
                UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 0f),
                    Vector2.zero, new Vector2(ButtonSize, ButtonSize * HexAspect));
                // The colours are driven here (dark face, gold fill); the default
                // tint on a disabled button would wash the face out.
                button.transition = Selectable.Transition.None;
                buttonGroup = UIFactory.Group(button);
                icon = button.transform.Find("Icon").GetComponent<Image>();
                frame = button.transform.Find("Ring").GetComponent<Image>();
                frame.sprite = ArtLibrary.IconHexFrame;
                frame.color = WithAlpha(AbilityColor, 0.95f);

                face = button.GetComponent<Image>();
                face.sprite = ArtLibrary.IconHexFill;
                face.color = EmptyFace;
                glow = UIFactory.GlowShadow(face, new Color(color.r, color.g, color.b, 0f), 26f, 0.06f);

                // Under the frame and the icon, over the face.
                charge = UIFactory.Rect(button.transform, "Charge", AbilityColor, ArtLibrary.IconHexFill);
                charge.raycastTarget = false;
                charge.type = Image.Type.Filled;
                charge.fillMethod = Image.FillMethod.Radial360;
                charge.fillOrigin = (int)Image.Origin360.Top;
                charge.fillClockwise = true;
                charge.fillAmount = 0f;
                UIFactory.Stretch(charge.rectTransform);
                charge.transform.SetAsFirstSibling();

                button.onClick.AddListener(() => { if (run != null) run.ActivateShipSpecial(); Refresh(); });
            }
            shownSpecial = null;
            shownReady = false;
            Refresh();
        }

        private void ApplyColor(Color next)
        {
            color = next;
            charge.color = next;
            if (frame != null) frame.color = WithAlpha(next, 0.95f);
            // Keeps the glow's current alpha, so it does not flash on a ship change.
            glow.Color = new Color(next.r, next.g, next.b, glow.Color.a);
        }

        public void Refresh()
        {
            if (button == null || run == null) return;
            var ship = run.ship;
            var special = ship != null ? ship.Special : null;
            var exists = special != null && special.kind != ShipSpecialKind.None;
            if (!exists)
            {
                button.interactable = false;
                buttonGroup.alpha = 0.35f;
                charge.fillAmount = 0f;
                shownReady = false;
                return;
            }
            buttonGroup.alpha = 1f;

            if (shownSpecial != special)
            {
                shownSpecial = special;
                ApplyColor(ColorFor(ship.definition));
                icon.sprite = IconFor(special.kind);
            }

            // Filled by the time that has passed since the ability was used,
            // the effect's own duration included (the recharge counts it).
            var total = ship.SpecialCooldown;
            var amount = total <= 0f ? 1f : Mathf.Clamp01(1f - ship.SpecialCooldownLeft / total);
            // Only touch the graphic when it moves enough to be seen.
            if (Mathf.Abs(charge.fillAmount - amount) > 0.004f || (amount >= 1f && charge.fillAmount < 1f))
            {
                charge.fillAmount = amount;
            }

            var ready = run.CanActivateSpecial;
            if (button.interactable != ready) button.interactable = ready;
            if (shownReady != ready)
            {
                shownReady = ready;
                UIFactory.FadeGlow(glow, new Color(color.r, color.g, color.b, ready ? 0.7f : 0f), 0.25f);
                if (ready) Juice.Punch(icon.transform, 0.25f, 0.3f);
            }
        }
    }
}
