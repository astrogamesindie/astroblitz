// AstroCrawler - the on-screen representation of an upgrade.
//
// Plays the role CCG Kit's CardView plays - read a card definition, paint a
// frame - but the card it paints has no mana cost, no attack/defence stats and
// no drag-to-board interaction: it is a permanent modifier the ship keeps for
// the rest of the run.
//
// This is a MonoBehaviour rather than a plain class so its parts can be
// serialized into the GameUI prefab. Construct() is what builds those parts, and
// it no-ops when they are already present - which is exactly the case for an
// instance that came out of the prefab.

using System;

using LeTai.TrueShadow;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstroCrawler
{
    public class CardWidget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image frame;
        [SerializeField] private Image glowBorder;
        [SerializeField] private Image gradient;
        [SerializeField] private Image bodyPanel;
        [SerializeField] private Image accentBar;
        [SerializeField] private Image iconGlow;
        [SerializeField] private Image iconBack;
        [SerializeField] private Image iconRing;
        [SerializeField] private Image headerRule;
        [SerializeField] private Image[] levelPips;
        [SerializeField] private RectTransform brackets;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image footerShade;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text footerText;
        [SerializeField] private Button button;

        [Tooltip("Resting scale. The card is laid out at 340x320 and scaled up where it has " +
                 "to be read on a phone; hover and press animate around this, not around 1.")]
        [SerializeField] private float restScale = 1f;

        private Color accent = Color.white;
        private bool interactive;
        private bool shineAdded;
        [SerializeField] private bool compactChoice;
        [SerializeField] private Button infoButton;
        private int shownUpgradeLevel = 1;

        // Where the card rests. Its entrance animates scale only, so the card
        // stays in the position assigned by the choice screen's layout.
        private Vector2 home;
        private bool hasHome;
        private bool dealing;
        private bool pointerInside;

        // Added at runtime: True Shadow components carry no references worth
        // serializing, and cards authored before it existed need them too.
        private TrueShadow frameGlow;
        private RectTransform unlockEmblem;
        private GameObject[] unlockHidden;

        public RuntimeCard card { get; private set; }

        /// <summary>The widget's own RectTransform, under its authored name.</summary>
        public RectTransform root
        {
            get { return (RectTransform)transform; }
        }

        public event Action<CardWidget> onClicked;
        public event Action<CardWidget> onInfoRequested;

        public float RestScale
        {
            get { return restScale; }
        }

        /// <summary>Scales the whole card; text is SDF, so it stays sharp.</summary>
        public void SetRestScale(float scale)
        {
            restScale = Mathf.Max(0.1f, scale);
            root.localScale = Vector3.one * restScale;
        }

        public void SetHomePosition(Vector2 position)
        {
            home = position;
            hasHome = true;
            root.anchoredPosition = position;
        }

        public static CardWidget Create(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var widget = go.AddComponent<CardWidget>();
            widget.Construct(size);
            return widget;
        }

        /// <summary>
        /// Builds the card's parts. Returns immediately when they already exist,
        /// which is how an instance from the prefab skips all of this.
        /// </summary>
        public void Construct(Vector2 size)
        {
            if (frame != null)
            {
                return;
            }

            root.sizeDelta = size;

            glowBorder = UIFactory.Rect(root, "Glow", new Color(1f, 1f, 1f, 0f), ArtLibrary.FrameGlow);
            if (glowBorder.sprite != null)
            {
                glowBorder.type = Image.Type.Sliced;
                glowBorder.pixelsPerUnitMultiplier = 2f;
            }
            UIFactory.Stretch(glowBorder.rectTransform, -18f);

            frame = UIFactory.Panel9(root, "Frame", UIFactory.PanelSoft);
            UIFactory.Stretch(frame.rectTransform);
            frame.raycastTarget = true;

            // A vertical gradient keeps the flat panel from reading as a plain
            // rectangle, and darkens the bottom where the footer sits.
            gradient = UIFactory.Rect(root, "Gradient", new Color(0.02f, 0.03f, 0.07f, 0.55f),
                ArtLibrary.GradientPanel);
            UIFactory.Stretch(gradient.rectTransform, 6f);

            accentBar = UIFactory.Rect(root, "LevelAccent", Color.white, ArtLibrary.Pixel);
            UIFactory.Place(accentBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -7f),
                new Vector2(size.x - 34f, 4f));

            footerShade = UIFactory.Rect(root, "FooterShade", new Color(0.015f, 0.02f, 0.04f, 0.72f),
                ArtLibrary.Pixel);
            UIFactory.Place(footerShade.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 10f),
                new Vector2(size.x - 34f, 44f));

            bodyPanel = UIFactory.Rect(root, "BodyPanel", new Color(0.015f, 0.02f, 0.04f, 0.32f),
                ArtLibrary.Pixel);
            UIFactory.Place(bodyPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -86f),
                new Vector2(size.x - 34f, size.y - 148f));

            iconGlow = UIFactory.Rect(root, "IconGlow", new Color(1f, 1f, 1f, 0.3f),
                ArtLibrary.CircleGlow);
            UIFactory.Place(iconGlow.rectTransform, new Vector2(0f, 1f), new Vector2(11f, -11f),
                new Vector2(62f, 62f));

            // A hexagonal plate with a lit outline, not a round chip.
            iconBack = UIFactory.Rect(root, "IconBack", new Color(0f, 0f, 0f, 0.55f),
                ArtLibrary.ArtOr("hex_disc", ArtLibrary.CircleFilled));
            UIFactory.Place(iconBack.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -16f),
                new Vector2(52f, 52f));
            iconRing = UIFactory.Rect(root, "IconRing", Color.white,
                ArtLibrary.ArtOr("wire_hex", ArtLibrary.CircleOutline));
            UIFactory.Place(iconRing.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -16f),
                new Vector2(52f, 52f));
            iconBack.raycastTarget = false;
            iconRing.raycastTarget = false;

            // A thin rule under the header, and three pips for the card's level.
            headerRule = UIFactory.Rect(root, "HeaderRule", new Color(1f, 1f, 1f, 0.4f), ArtLibrary.Pixel);
            UIFactory.Place(headerRule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78f),
                new Vector2(size.x - 34f, 2f));
            headerRule.raycastTarget = false;

            levelPips = new Image[3];
            for (var i = 0; i < levelPips.Length; i++)
            {
                levelPips[i] = UIFactory.Rect(root, "Pip" + i, Color.white,
                    ArtLibrary.ArtOr("diamond", ArtLibrary.CircleFilled));
                UIFactory.Place(levelPips[i].rectTransform, new Vector2(0f, 0f),
                    new Vector2(28f + i * 22f, 25f), new Vector2(14f, 14f));
                levelPips[i].raycastTarget = false;
            }

            // Corner brackets, dim at rest and lit while the pointer is on the card.
            brackets = UIFactory.Brackets(root, new Color(1f, 1f, 1f, 0.5f), 26f, -6f);
            var corners = brackets.GetComponentsInChildren<Image>();
            for (var i = 0; i < corners.Length; i++) corners[i].raycastTarget = false;

            iconImage = UIFactory.Rect(root, "Icon", Color.white);
            iconImage.preserveAspect = true;
            UIFactory.Place(iconImage.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -26f),
                new Vector2(32f, 32f));
            iconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            iconImage.rectTransform.anchoredPosition = new Vector2(42f, -42f);

            titleText = UIFactory.Label(root, "Title", "", 28, Color.white,
                TextAlignmentOptions.Left, true);
            UIFactory.Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(74f, -12f),
                new Vector2(size.x - 92f, 58f));
            titleText.overflowMode = TextOverflowModes.Truncate;
            titleText.characterSpacing = 2f;

            bodyText = UIFactory.Label(root, "Body", "", 22, new Color(1f, 1f, 1f, 0.92f),
                TextAlignmentOptions.TopLeft);
            UIFactory.Place(bodyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -96f),
                new Vector2(size.x - 56f, size.y - 166f));
            bodyText.overflowMode = TextOverflowModes.Truncate;
            bodyText.lineSpacing = 6f;

            footerText = UIFactory.Label(root, "Footer", "", 22, new Color(1f, 1f, 1f, 0.88f),
                TextAlignmentOptions.Center, true);
            UIFactory.Place(footerText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 11f),
                new Vector2(size.x - 40f, 42f));
            footerText.characterSpacing = 5f;

            UIFactory.AddUnderlay(titleText);
            UIFactory.AddUnderlay(bodyText, 0.15f, 0.05f);
            UIFactory.AddUnderlay(footerText);

            button = gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(HandleClick);
        }

        /// <summary>
        /// Re-attaches the click handler on an instance that came from the
        /// prefab, where the listener was not serialized.
        /// </summary>
        public void RebindEvents()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
            if (infoButton != null)
            {
                infoButton.onClick.RemoveListener(HandleInfoClick);
                infoButton.onClick.AddListener(HandleInfoClick);
            }
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
                button.onClick.AddListener(HandleClick);
            }
        }

        private void HandleClick()
        {
            // A tap on a card still flying in is almost always a tap meant for
            // the screen before; it should not pick an upgrade.
            if (!interactive || dealing)
            {
                return;
            }

            // The confirmation reads before the screen tears down.
            Juice.Punch(root, 0.12f, 0.24f);
            UIFactory.FlashGlow(frameGlow, new Color(accent.r, accent.g, accent.b, 1f),
                new Color(accent.r, accent.g, accent.b, 0.6f), 0.35f);
            Juice.ScreenFlash(new Color(accent.r, accent.g, accent.b, 0.22f), 0.35f);

            if (onClicked != null)
            {
                onClicked(this);
            }
        }

        public bool interactable
        {
            get { return button != null && button.interactable; }
            set
            {
                if (button != null)
                {
                    button.interactable = value;
                }
                interactive = value;
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------------
        // Pointer feel
        // ------------------------------------------------------------------

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            SetHovered(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            SetHovered(false);
        }

        private void OnDisable()
        {
            // Snapping back rather than tweening: a tween started here would run
            // against a disabled object and leave it mid-animation.
            ResetVisualState();
        }

        internal void SetHovered(bool hovered)
        {
            // While dealing, a resting mouse pointer the card slides under
            // would fire this. FinishDeal applies the hover once it lands.
            if (!interactive || dealing)
            {
                return;
            }

            // Only the scale tween is affected; colour and glow have their own targets.
            LeanTween.cancel(gameObject, false, TweenAction.SCALE);
            LeanTween.scale(root, Vector3.one * (restScale * (hovered ? 1.05f : 1f)), 0.16f)
                .setEase(LeanTweenType.easeOutCubic)
                .setIgnoreTimeScale(true);

            Juice.TintGraphic(glowBorder,
                new Color(accent.r, accent.g, accent.b, hovered ? 0.5f : 0f), 0.18f);
            TintBrackets(new Color(accent.r, accent.g, accent.b, hovered ? 1f : 0.55f), 0.18f);
            UIFactory.FadeGlow(frameGlow, new Color(accent.r, accent.g, accent.b, hovered ? 0.9f : 0f), 0.18f);

            if (hovered && iconImage != null)
            {
                Juice.Punch(iconImage.rectTransform, 0.22f, 0.3f);
            }
        }

        private void TintBrackets(Color color, float duration)
        {
            if (brackets == null) return;
            var corners = brackets.GetComponentsInChildren<Image>();
            for (var i = 0; i < corners.Length; i++)
            {
                if (duration <= 0f) corners[i].color = color;
                else Juice.TintGraphic(corners[i], color, duration);
            }
        }

        /// <summary>The parts that only a painted card has: outline, rule, level pips, brackets.</summary>
        private void PaintExtras(Color tint, int level)
        {
            if (iconRing != null) { iconRing.enabled = true; iconRing.color = new Color(tint.r, tint.g, tint.b, 0.95f); }
            if (headerRule != null) { headerRule.enabled = true; headerRule.color = new Color(tint.r, tint.g, tint.b, 0.45f); }
            if (levelPips != null)
            {
                for (var i = 0; i < levelPips.Length; i++)
                {
                    if (levelPips[i] == null) continue;
                    levelPips[i].gameObject.SetActive(level > 0);
                    levelPips[i].color = i < level ? tint : new Color(tint.r, tint.g, tint.b, 0.22f);
                }
            }
            if (brackets != null)
            {
                brackets.gameObject.SetActive(true);
                TintBrackets(new Color(tint.r, tint.g, tint.b, 0.55f), 0f);
            }

            // A slow sweep of light, added once and only while playing.
            if (!compactChoice && !shineAdded && Application.isPlaying)
            {
                shineAdded = true;
                UIFactory.AddShine(root, 4.8f, 0.1f);
            }
        }

        /// <summary>Reveals the card in place with a stagger.</summary>
        public void PlayDeal(int index)
        {
            SetVisible(true);
            if (!hasHome)
            {
                home = root.anchoredPosition;
                hasHome = true;
            }

            LeanTween.cancel(gameObject);
            root.anchoredPosition = home;
            root.localScale = Vector3.one * (restScale * 0.82f);
            if (glowBorder != null)
            {
                glowBorder.color = new Color(accent.r, accent.g, accent.b, 0f);
            }
            UIFactory.FadeGlow(frameGlow, new Color(accent.r, accent.g, accent.b, 0f), 0f);

            dealing = true;
            LeanTween.scale(gameObject, Vector3.one * restScale, 0.4f)
                .setDelay(0.06f + index * 0.09f)
                .setEase(LeanTweenType.easeOutBack)
                .setIgnoreTimeScale(true)
                .setOnComplete(FinishDeal);
        }

        private void FinishDeal()
        {
            dealing = false;
            if (hasHome) root.anchoredPosition = home;
            // The pointer was already resting where the card landed.
            if (pointerInside) SetHovered(true);
        }

        /// <summary>
        /// Drops any in-flight animation and returns to the resting look. Called
        /// when the widget is hidden, so it never comes back mid-tween.
        /// </summary>
        internal void ResetVisualState()
        {
            LeanTween.cancel(gameObject);
            root.localScale = Vector3.one * restScale;
            if (hasHome) root.anchoredPosition = home;
            dealing = false;
            pointerInside = false;
            UIFactory.FadeGlow(frameGlow, new Color(accent.r, accent.g, accent.b, 0f), 0f);
            if (glowBorder != null)
            {
                LeanTween.cancel(glowBorder.gameObject);
                glowBorder.color = new Color(accent.r, accent.g, accent.b, 0f);
            }
        }

        // ------------------------------------------------------------------
        // Painting
        // ------------------------------------------------------------------

        /// <summary>
        /// Paints an empty slot - the hand is momentarily short because the whole
        /// deck is in play.
        /// </summary>
        public void BindEmpty()
        {
            card = null;
            accent = UIFactory.InkDim;
            titleText.text = "";
            bodyText.text = "<i>" + GameLocalization.Choose("slot vazio", "empty slot", "espacio vacío") + "</i>";
            footerText.text = "";
            frame.color = new Color(0.12f, 0.14f, 0.2f, 0.6f);
            glowBorder.color = new Color(0f, 0f, 0f, 0f);
            accentBar.enabled = false;
            iconGlow.enabled = false;
            iconBack.enabled = false;
            if (iconRing != null) iconRing.enabled = false;
            if (headerRule != null) headerRule.enabled = false;
            if (brackets != null) brackets.gameObject.SetActive(false);
            if (levelPips != null)
            {
                for (var i = 0; i < levelPips.Length; i++)
                {
                    if (levelPips[i] != null) levelPips[i].gameObject.SetActive(false);
                }
            }
            iconImage.enabled = false;
            bodyPanel.color = new Color(0.015f, 0.02f, 0.04f, 0.22f);
            interactable = false;
        }

        /// <summary>
        /// Paints a card. <paramref name="hotkey"/> is 0 when the widget is not a
        /// hand slot (the upgrade screen, the ship preview).
        /// </summary>
        public void Bind(RuntimeCard card, int hotkey, string footerOverride = null, int upgradeLevel = 0,
            Color? previewTint = null)
        {
            RestoreUnlockFace();
            this.card = card;
            EnsureShadows();

            var level = upgradeLevel > 0 ? upgradeLevel : card.level;
            shownUpgradeLevel = level;
            var evolved = level > 1;
            var tint = upgradeLevel > 0
                ? UpgradeColor(upgradeLevel)
                : previewTint ?? SpriteFactory.ParseColor(card.definition.tint);
            accent = tint;

            titleText.text = GameLocalization.Content(upgradeLevel > 0
                ? card.definition.NameFor(level)
                : card.displayName);
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 22f;
            titleText.fontSizeMax = 25f;
            titleText.color = Color.white;

            bodyText.text = GameLocalization.Content(upgradeLevel > 0
                ? card.definition.DescriptionFor(level)
                : card.displayDescription);
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 20f;
            bodyText.fontSizeMax = 21f;

            if (!string.IsNullOrEmpty(footerOverride))
            {
                footerText.text = footerOverride;
            }
            else
            {
                var tag = CategoryLabel(card.definition.category);
                footerText.text = hotkey > 0 ? tag + "   ·   [" + hotkey + "]" : tag;
            }

            frame.color = new Color(tint.r * 0.20f + 0.05f, tint.g * 0.20f + 0.06f,
                tint.b * 0.20f + 0.085f, 0.99f);
            bodyText.color = new Color(1f, 1f, 1f, 0.92f);
            footerText.color = upgradeLevel > 0 ? tint : new Color(1f, 1f, 1f, 0.88f);

            accentBar.enabled = true;
            accentBar.color = new Color(tint.r, tint.g, tint.b, 0.95f);

            iconGlow.enabled = true;
            iconGlow.color = new Color(tint.r, tint.g, tint.b, 0.34f);
            iconBack.enabled = true;
            iconBack.color = new Color(tint.r * 0.16f, tint.g * 0.16f, tint.b * 0.16f, 0.95f);
            PaintExtras(tint, level);
            iconImage.enabled = true;
            iconImage.sprite = CardIconFactory.Get(card.definition.effect);
            // A central pivot also centers non-square sprites under preserveAspect.
            iconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            CardIconFactory.CenterArtwork(iconImage, card.definition.effect, new Vector2(42f, -42f));
            // Also aligns widgets already serialized in the scene/prefab.
            iconGlow.rectTransform.anchoredPosition = new Vector2(11f, -11f);
            // The card's own colour, like its ring, rule and pips (the icon
            // artwork is white, so the tint is the colour that shows).
            iconImage.color = new Color(tint.r, tint.g, tint.b, 1f);

            bodyPanel.color = new Color(0.015f, 0.02f, 0.04f, 0.32f);
            glowBorder.color = new Color(tint.r, tint.g, tint.b, level > 2 ? 0.55f : evolved ? 0.35f : 0f);

            if (compactChoice)
            {
                ApplyCompactChoice();
                return;
            }

            // An already-upgraded pick advertises itself with a slow pulse;
            // the maximum form pulses brighter and faster.
            if (level > 2)
            {
                Juice.BreatheUI(glowBorder, 0.35f, 0.85f, 1f);
            }
            else if (evolved)
            {
                Juice.BreatheUI(glowBorder, 0.2f, 0.6f, 1.3f);
            }
        }

        /// <summary>
        /// A card the player has not unlocked yet: black silhouette for the
        /// icon and "???" for the name and text, so it stays a surprise.
        /// </summary>
        public void ShowAsMystery()
        {
            titleText.text = "???";
            bodyText.text = "???";
            bodyText.color = new Color(1f, 1f, 1f, 0.55f);
            iconImage.color = Color.black;
            iconGlow.enabled = false;
            glowBorder.color = Color.clear;
            LeanTween.cancel(glowBorder.gameObject);
        }

        /// <summary>Uses the real card frame and corners for the golden unlock charge.</summary>
        public void ShowUnlockMystery()
        {
            unlockHidden = new[] { titleText.gameObject, bodyText.gameObject, footerText.gameObject,
                iconImage.gameObject, iconGlow.gameObject, iconBack.gameObject, iconRing.gameObject,
                headerRule.gameObject, bodyPanel.gameObject, footerShade.gameObject };
            foreach (var part in unlockHidden) part.SetActive(false);
            if (levelPips != null)
                foreach (var pip in levelPips) if (pip != null) pip.gameObject.SetActive(false);

            // Keep the card's gradient, corner brackets, accent rail and shine visible.
            var gold = UIFactory.Evolved;
            glowBorder.color = new Color(gold.r, gold.g, gold.b, 0.22f);
            Juice.BreatheUI(glowBorder, 0.12f, 0.32f, 1.4f);
            unlockEmblem = UIFactory.NewRect(root, "UnlockEmblem");
            UIFactory.Place(unlockEmblem, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 200f);

            var halo = UIFactory.Rect(unlockEmblem, "Halo", new Color(gold.r, gold.g, gold.b, 0.18f), ArtLibrary.Glow);
            UIFactory.Place(halo.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 250f);
            var plate = UIFactory.Rect(unlockEmblem, "HexPlate", new Color(0.12f, 0.085f, 0.025f, 0.9f),
                ArtLibrary.Art("hex_disc"));
            UIFactory.Place(plate.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 176f);
            var ring = UIFactory.Rect(unlockEmblem, "HexRing", gold, ArtLibrary.Art("wire_hex"));
            UIFactory.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 176f);
            foreach (var image in new[] { halo, plate, ring })
            {
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            var question = UIFactory.Label(unlockEmblem, "Question", "?", 120, gold,
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(question.rectTransform, 12f);
            question.raycastTarget = false;
            UIFactory.AddUnderlay(question);
            UIFactory.GlowShadow(question, new Color(gold.r, gold.g, gold.b, 0.35f), 14f, 0.04f);
        }

        private void RestoreUnlockFace()
        {
            if (unlockHidden == null) return;
            foreach (var part in unlockHidden) if (part != null) part.SetActive(true);
            unlockHidden = null;
            if (unlockEmblem != null) unlockEmblem.gameObject.SetActive(false);
            LeanTween.cancel(glowBorder.gameObject);
        }

        /// <summary>Compact choice using the full card's frame, emblem and level accents.</summary>
        public void SetCompactChoice(Vector2 size)
        {
            compactChoice = true;
            root.sizeDelta = size;
            ApplyCompactChoice();
        }

        private void EnsureInfoButton()
        {
            // Revisão de legibilidade: o "i" deixou de ser uma aba da largura da
            // carta com 80 de altura e virou um botão redondo no canto superior
            // direito da própria carta (112 visível, 144 de área de toque), com o
            // anel original da gameplay (Icon Button 07), não a peça do kit.
            if (infoButton == null)
            {
                infoButton = UIFactory.Button(root, "Info", "i", accent, UIFactory.Ink, 52, Sound.None);
                var surface = UIFactory.Panel9(infoButton.transform, "Inset", Color.white);
                surface.raycastTarget = false;
                surface.transform.SetAsFirstSibling();
                infoButton.onClick.AddListener(HandleInfoClick);
            }
            var rect = infoButton.GetComponent<RectTransform>();
            const float size = 112f;
            UIFactory.Place(rect, new Vector2(1f, 1f), new Vector2(18f, 18f), new Vector2(size, size));
            rect.pivot = new Vector2(1f, 1f);
            rect.SetAsLastSibling();
            var image = infoButton.GetComponent<Image>();
            image.sprite = ArtLibrary.IconRingFill;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = new Color(0.025f, 0.055f, 0.075f);
            var label = infoButton.GetComponentInChildren<TMP_Text>();
            label.color = UIFactory.Ink;
            UIFactory.UseBold(label);
            var inset = rect.Find("Inset").GetComponent<Image>();
            // "Inset" guarda o anel na cor do nível da carta, por cima do miolo e sob o "i".
            inset.sprite = ArtLibrary.IconRingFrame;
            inset.type = Image.Type.Simple;
            inset.preserveAspect = true;
            inset.color = accent;
            UIFactory.Stretch(inset.rectTransform, 0f);
            UIFactory.ExpandHitArea(rect, UIFactory.Touch, UIFactory.Touch);
        }

        private void HandleInfoClick()
        {
            if (interactive && !dealing) onInfoRequested?.Invoke(this);
        }

        private void ApplyCompactChoice()
        {
            EnsureInfoButton();
            var size = root.sizeDelta;
            frame.pixelsPerUnitMultiplier = 2.4f;
            frame.color = new Color(accent.r * 0.20f + 0.05f, accent.g * 0.20f + 0.06f,
                accent.b * 0.20f + 0.085f, 0.99f);
            gradient.sprite = ArtLibrary.GradientPanel;
            gradient.color = new Color(0.02f, 0.03f, 0.07f, 0.55f);
            UIFactory.Stretch(gradient.rectTransform, 6f);
            gradient.raycastTarget = false;
            foreach (var image in new[] { glowBorder, bodyPanel, accentBar, iconGlow, iconBack, iconRing, headerRule })
            {
                if (image == null) continue;
                image.gameObject.SetActive(true);
                image.enabled = true;
                image.raycastTarget = false;
            }
            glowBorder.color = new Color(accent.r, accent.g, accent.b, 0.18f);
            accentBar.color = new Color(accent.r, accent.g, accent.b, 0.95f);
            UIFactory.Place(accentBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -7f),
                new Vector2(size.x - 34f, 4f));
            iconGlow.color = new Color(accent.r, accent.g, accent.b, 0.34f);
            iconBack.color = new Color(accent.r * 0.16f, accent.g * 0.16f, accent.b * 0.16f, 0.95f);
            iconRing.color = new Color(accent.r, accent.g, accent.b, 0.95f);
            // The same hexagonal emblem as a full card, enlarged for the compact face.
            foreach (var image in new[] { iconGlow, iconBack, iconRing })
            {
                var diameter = image == iconGlow ? 190f : 156f;
                UIFactory.Place(image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -96f),
                    new Vector2(diameter, diameter));
                image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            headerRule.color = new Color(accent.r, accent.g, accent.b, 0.45f);
            UIFactory.Place(headerRule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f),
                new Vector2(size.x - 34f, 2f));
            bodyPanel.color = new Color(0.015f, 0.02f, 0.04f, 0.32f);
            UIFactory.Place(bodyPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -198f),
                new Vector2(size.x - 34f, size.y - 218f));
            if (brackets != null)
            {
                brackets.gameObject.SetActive(true);
                TintBrackets(new Color(accent.r, accent.g, accent.b, 0.55f), 0f);
            }
            if (footerShade != null)
            {
                footerShade.gameObject.SetActive(true);
                UIFactory.Place(footerShade.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f),
                    new Vector2(size.x - 34f, 36f));
            }
            if (levelPips != null) foreach (var pip in levelPips) if (pip != null) pip.gameObject.SetActive(false);
            bodyText.gameObject.SetActive(false);
            footerText.gameObject.SetActive(true);
            footerText.text = GameLocalization.Choose("NÍVEL ", "LEVEL ", "NIVEL ") + shownUpgradeLevel;
            footerText.color = accent;
            footerText.fontSize = 26f;
            footerText.characterSpacing = 2f;
            footerText.alignment = TextAlignmentOptions.Center;
            UIFactory.Place(footerText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f),
                new Vector2(size.x - 44f, 32f));
            iconImage.raycastTarget = false;
            UIFactory.Place(iconImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(96f, 96f));
            iconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            if (card != null) CardIconFactory.CenterArtwork(iconImage, card.definition.effect, new Vector2(0f, -96f));
            UIFactory.Place(titleText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(size.x - 44f, size.y - 252f));
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.characterSpacing = 0f;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 28f;
            titleText.fontSizeMax = 42f;
            titleText.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>Roomier full card used by the shared details sheet.</summary>
        public void ResizeDetails(Vector2 size)
        {
            root.sizeDelta = size;
            accentBar.rectTransform.sizeDelta = new Vector2(size.x - 34f, 4f);
            footerShade.rectTransform.sizeDelta = new Vector2(size.x - 34f, 44f);
            bodyPanel.rectTransform.sizeDelta = new Vector2(size.x - 34f, size.y - 148f);
            headerRule.rectTransform.sizeDelta = new Vector2(size.x - 34f, 2f);
            titleText.rectTransform.sizeDelta = new Vector2(size.x - 92f, 58f);
            bodyText.rectTransform.sizeDelta = new Vector2(size.x - 56f, size.y - 166f);
            footerText.rectTransform.sizeDelta = new Vector2(size.x - 40f, 42f);
            bodyText.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>Uses the card presentation for information outside the upgrade deck.</summary>
        public void BindInformation(string title, string description, string footer, Color tint, Sprite icon)
        {
            ResetVisualState();
            var definition = new CardDefinition
            {
                name = title,
                description = description,
                tint = ColorUtility.ToHtmlStringRGB(tint)
            };
            Bind(new RuntimeCard(-1, definition, null), 0, footer);
            iconImage.sprite = icon;
            iconImage.rectTransform.anchoredPosition = new Vector2(42f, -42f);
            PaintExtras(tint, 0);
            interactable = false;
        }

        private void EnsureShadows()
        {
            if (frameGlow != null || frame == null)
            {
                return;
            }
            UIFactory.EnsureDropShadow(frame, 22f, 8f, 0.55f);
            frameGlow = UIFactory.EnsureGlow(frame, new Color(1f, 1f, 1f, 0f), 34f, 0.12f);
        }

        public static Color UpgradeColor(int level)
        {
            switch (level)
            {
                // Raridade pela borda (PILARES_DE_UI §3): Fósforo comum, Ciano raro, Magenta evolução.
                case 1: return Palette.Fosforo;
                case 2: return Palette.Ciano;
                default: return Palette.Magenta;
            }
        }

        public static string CategoryLabel(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Vanguard: return GameLocalization.Choose("ASSALTO", "ASSAULT", "ASALTO");
                case CardCategory.Bastion: return GameLocalization.Choose("FORTIFICACAO", "FORTIFICATION", "FORTIFICACIÓN");
                case CardCategory.Spectre: return GameLocalization.Choose("GRAVITACIONAL", "GRAVITATIONAL", "GRAVITACIONAL");
                case CardCategory.Aurora: return GameLocalization.Choose("SUPORTE", "SUPPORT", "APOYO");
                case CardCategory.Orion: return GameLocalization.Choose("ARTILHARIA", "ARTILLERY", "ARTILLERÍA");
                case CardCategory.Noel: return GameLocalization.Choose("NATAL", "CHRISTMAS", "NAVIDAD");
                default: return GameLocalization.Choose("COMUM", "COMMON", "COMÚN");
            }
        }
    }
}
