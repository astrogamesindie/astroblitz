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

        // "Ready" animation: while the ability can be used the button breathes,
        // a soft halo pulses behind it and a hexagon ring keeps rippling out.
        // Unscaled time, so a hit stop does not freeze it.
        private const float BreathePeriod = 1.1f;
        private const float BreatheScale = 0.06f;
        private const float RipplePeriod = 1.4f;
        private const float RippleGrowth = 0.5f;
        private Image readyHalo;
        private Image readyRing;
        private float readySince;

        /// <summary>-1: the ship's own special. 0..2: a Squadron wingman's (PlayerShip.GetWingSpecial).</summary>
        private int wingIndex = -1;

        /// <summary>Distance between two ability buttons in the row (the wingmen's go left of the main one).</summary>
        public const float RowStep = 172f;

        public void Bind(RunController controller)
        {
            Bind(controller, -1);
        }

        public void Bind(RunController controller, int wingIndex)
        {
            run = controller;
            this.wingIndex = wingIndex;
            var root = (RectTransform)transform;
            // Main button in the bottom-right corner; each wingman's one step further left.
            UIFactory.Place(root, new Vector2(1f, 0f), new Vector2(-32f - (wingIndex + 1) * RowStep, 32f),
                new Vector2(260f, 226f));
            root.localScale = Vector3.one * (wingIndex < 0 ? 1f : 0.8f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(-32f - 130f - (wingIndex + 1) * RowStep, 32f);
            if (button == null)
            {

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

                BuildReadyFx(root);

                button.onClick.AddListener(() =>
                {
                    if (run != null)
                    {
                        if (this.wingIndex < 0) run.ActivateShipSpecial();
                        else run.ActivateWingSpecial(this.wingIndex);
                    }
                    Refresh();
                });
            }
            shownSpecial = null;
            shownReady = false;
            Refresh();
        }

        private void BuildReadyFx(RectTransform root)
        {
            var center = new Vector2(0f, ButtonSize * HexAspect * 0.5f);
            readyHalo = UIFactory.Rect(root, "ReadyHalo", WithAlpha(color, 0f), ArtLibrary.Glow);
            readyHalo.raycastTarget = false;
            UIFactory.Place(readyHalo.rectTransform, new Vector2(0.5f, 0f), center, Vector2.one * ButtonSize * 1.9f);
            readyHalo.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            readyHalo.rectTransform.anchoredPosition = center;
            readyHalo.transform.SetAsFirstSibling();

            readyRing = UIFactory.Rect(root, "ReadyRing", WithAlpha(color, 0f), ArtLibrary.IconHexFrame);
            readyRing.raycastTarget = false;
            UIFactory.Place(readyRing.rectTransform, new Vector2(0.5f, 0f), center,
                new Vector2(ButtonSize, ButtonSize * HexAspect));
            readyRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            readyRing.rectTransform.anchoredPosition = center;
            // Over the halo, under the button.
            readyRing.transform.SetSiblingIndex(1);
        }

        private void TickReadyFx()
        {
            if (button == null) return;
            if (!shownReady)
            {
                if (button.transform.localScale != Vector3.one) button.transform.localScale = Vector3.one;
                if (readyHalo != null && readyHalo.color.a > 0f) readyHalo.color = WithAlpha(color, 0f);
                if (readyRing != null && readyRing.color.a > 0f) readyRing.color = WithAlpha(color, 0f);
                return;
            }

            var t = Time.unscaledTime - readySince;
            // 0..1..0, starting at rest so the punch on becoming ready reads first.
            var breathe = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / BreathePeriod);
            button.transform.localScale = Vector3.one * (1f + BreatheScale * breathe);
            if (readyHalo != null)
            {
                readyHalo.color = WithAlpha(color, 0.18f + 0.32f * breathe);
                readyHalo.transform.localScale = Vector3.one * (0.92f + 0.1f * breathe);
            }
            if (readyRing != null)
            {
                var ripple = Mathf.Repeat(t, RipplePeriod) / RipplePeriod;
                var eased = 1f - (1f - ripple) * (1f - ripple);
                readyRing.transform.localScale = Vector3.one * (1f + RippleGrowth * eased);
                readyRing.color = WithAlpha(color, 0.85f * (1f - ripple));
            }
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
            if (wingIndex >= 0)
            {
                RefreshWing();
                return;
            }
            var ship = run.ship;
            var special = ship != null ? ship.Special : null;
            var exists = special != null && special.kind != ShipSpecialKind.None;
            if (!exists)
            {
                button.interactable = false;
                buttonGroup.alpha = 0.35f;
                charge.fillAmount = 0f;
                shownReady = false;
                TickReadyFx();
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

            ShowReady(run.CanActivateSpecial);
        }

        /// <summary>A wingman's button: hidden until that wingman (with a special) has joined.</summary>
        private void RefreshWing()
        {
            var ship = run.ship;
            var slot = ship != null ? ship.GetWingSpecial(wingIndex) : null;
            var visible = slot != null;
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
                if (visible) Juice.PopIn(transform, 0.8f, 0.35f, 0f, true);
            }
            if (!visible)
            {
                shownSpecial = null;
                shownReady = false;
                TickReadyFx();
                return;
            }
            buttonGroup.alpha = 1f;
            if (shownSpecial != slot.special)
            {
                shownSpecial = slot.special;
                ApplyColor(ColorFor(slot.owner));
                icon.sprite = IconFor(slot.special.kind);
            }
            var total = ship.WingSpecialCooldown(slot);
            var amount = total <= 0f ? 1f : Mathf.Clamp01(1f - slot.cooldownLeft / total);
            if (Mathf.Abs(charge.fillAmount - amount) > 0.004f || (amount >= 1f && charge.fillAmount < 1f))
                charge.fillAmount = amount;
            ShowReady(run.CanActivateWingSpecial(wingIndex));
        }

        private void ShowReady(bool ready)
        {
            if (button.interactable != ready) button.interactable = ready;
            if (shownReady != ready)
            {
                shownReady = ready;
                readySince = Time.unscaledTime;
                UIFactory.FadeGlow(glow, new Color(color.r, color.g, color.b, ready ? 0.7f : 0f), 0.25f);
                if (ready) Juice.Punch(icon.transform, 0.25f, 0.3f);
            }
            TickReadyFx();
        }
    }
}
