// AstroCrawler - feedback on the ship upgrade screen.
//
// Buying levels has to feel like progress toward something. Three pieces:
// - a "next exclusive card" strip above the buy buttons: the card as a black
//   silhouette, how many levels are left and a bar from the last unlock to
//   the next one (it pulses when one level is left);
// - milestones on the total levels bar, one per exclusive card (gold once
//   open, the next one breathing);
// - on purchase, the bars fill up instead of jumping and a "+N" floats off
//   the level counter; crossing an unlock level opens a full-screen reveal:
//   the card charges up as a silhouette, then flips to its real face with a
//   flash, light rays and the unlock sound. A tap skips the charge. The base
//   deck uses the same reveal when a card is bought.

using System;
using System.Collections.Generic;

using LeTai.TrueShadow;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private const float NextCardStripHeight = 76f;

        private RectTransform nextCardStrip;
        private Image nextCardIcon;
        private Image nextCardFill;
        private TMP_Text nextCardTitle;
        private TMP_Text nextCardRemaining;
        private TrueShadow nextCardGlow;
        private RectTransform levelMilestones;

        private RectTransform cardReveal;
        private CardWidget revealWidget;
        private CardDefinition revealCard;
        private Action revealClosed;
        private bool revealShown;
        private bool revealReady;

        private bool CardRevealOpen => cardReveal != null && cardReveal.gameObject.activeSelf;

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        /// <summary>The strip just above the buy buttons: what the next levels unlock.</summary>
        private void BuildNextCardStrip()
        {
            var strip = UIFactory.Panel9(passiveScreen, "NextCard", UIFactory.PanelDeep);
            nextCardStrip = strip.rectTransform;
            if (UIFactory.Portrait)
            {
                // Right above the buy buttons, where the decision is made.
                nextCardStrip.anchorMin = new Vector2(0f, 0f);
                nextCardStrip.anchorMax = new Vector2(1f, 0f);
                nextCardStrip.pivot = new Vector2(0.5f, 0f);
                nextCardStrip.offsetMin = new Vector2(26f, 150f);
                nextCardStrip.offsetMax = new Vector2(-26f, 150f + NextCardStripHeight);
            }
            else
            {
                // Landscape: the header row right of the levels bar, short of the credits chip.
                nextCardStrip.anchorMin = new Vector2(0f, 1f);
                nextCardStrip.anchorMax = new Vector2(1f, 1f);
                nextCardStrip.pivot = new Vector2(0.5f, 1f);
                nextCardStrip.offsetMin = new Vector2(620f, -PassiveHeaderHeight + 6f);
                nextCardStrip.offsetMax = new Vector2(-350f, -PassiveHeaderHeight + 6f + NextCardStripHeight);
            }
            nextCardGlow = UIFactory.GlowShadow(strip, WithAlpha(UIFactory.Evolved, 0f), 24f, 0.05f);

            var iconBack = UIFactory.Rect(nextCardStrip, "IconBack", new Color(0.02f, 0.03f, 0.06f, 0.95f),
                ArtLibrary.CircleFilled);
            UIFactory.Place(iconBack.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(60f, 60f));
            nextCardIcon = UIFactory.Rect(nextCardStrip, "Icon", Color.black, ArtLibrary.CircleFilled);
            nextCardIcon.preserveAspect = true;
            UIFactory.Place(nextCardIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(44f, 44f));
            var mark = UIFactory.Label(iconBack.rectTransform, "Mark", "?", 30, WithAlpha(UIFactory.Evolved, 0.9f),
                TextAlignmentOptions.Center, true);
            UIFactory.Stretch(mark.rectTransform);

            // The retention hook of the screen: bright ink and a readable size
            // (it was 20-22 px in the secondary colour).
            nextCardTitle = UIFactory.Label(nextCardStrip, "Title", "", 28, UIFactory.Ink, TextAlignmentOptions.TopLeft, true);
            Band(nextCardTitle.rectTransform, 90f, 300f, -6f, 34f);
            nextCardTitle.characterSpacing = 3f;
            nextCardTitle.textWrappingMode = TextWrappingModes.NoWrap;
            nextCardTitle.enableAutoSizing = true;
            nextCardTitle.fontSizeMin = 22f;
            nextCardTitle.fontSizeMax = 28f;
            UIFactory.Brackets(nextCardStrip, WithAlpha(UIFactory.Evolved, 0.7f), 16f, -2f);

            Image back;
            nextCardFill = UIFactory.Bar(nextCardStrip, "Bar", UIFactory.Evolved,
                new Color(0.02f, 0.03f, 0.06f, 0.95f), out back);
            Band((RectTransform)nextCardFill.rectTransform.parent, 90f, 300f, -46f, 14f);

            nextCardRemaining = UIFactory.Label(nextCardStrip, "Remaining", "", 32, UIFactory.Evolved,
                TextAlignmentOptions.Right, true);
            UIFactory.Place(nextCardRemaining.rectTransform, new Vector2(1f, 0.5f), new Vector2(-18f, 0f),
                new Vector2(270f, 56f));
            nextCardRemaining.textWrappingMode = TextWrappingModes.NoWrap;
            nextCardRemaining.enableAutoSizing = true;
            nextCardRemaining.fontSizeMin = 22f;
            nextCardRemaining.fontSizeMax = 32f;
        }

        // ------------------------------------------------------------------
        // Refresh
        // ------------------------------------------------------------------

        /// <summary>The exclusive cards that open with ship levels, in level order.</summary>
        private List<CardDefinition> LevelCards(string shipId)
        {
            var result = new List<CardDefinition>();
            var ship = config.GetShip(shipId);
            if (ship == null) return result;
            for (var i = 0; i < ship.deck.Count; i++)
            {
                var card = config.GetCard(ship.deck[i].cardId);
                if (card != null && card.unlockLevel > 0) result.Add(card);
            }
            result.Sort((a, b) => a.unlockLevel.CompareTo(b.unlockLevel));
            return result;
        }

        /// <summary>How far the bar of the next card is at a given ship level (1 when nothing is left).</summary>
        private float NextCardProgress(string shipId, int level)
        {
            var previous = 0;
            CardDefinition next = null;
            foreach (var card in LevelCards(shipId))
            {
                if (card.unlockLevel <= level) previous = card.unlockLevel;
                else if (next == null) next = card;
            }
            if (next == null) return 1f;
            return Mathf.Clamp01((float)(level - previous) / Mathf.Max(1, next.unlockLevel - previous));
        }

        private void RefreshNextCardStrip()
        {
            if (nextCardStrip == null || string.IsNullOrEmpty(passiveShipId)) return;
            var level = TotalLevels(passiveShipId);
            var next = NextExclusiveUnlock(passiveShipId);

            LeanTween.cancel(nextCardRemaining.gameObject);
            nextCardRemaining.transform.localScale = Vector3.one;
            nextCardRemaining.alpha = 1f;
            nextCardFill.fillAmount = NextCardProgress(passiveShipId, level);

            if (next == null)
            {
                nextCardTitle.text = L("TODAS AS CARTAS DA NAVE LIBERADAS", "ALL SHIP CARDS UNLOCKED",
                    "TODAS LAS CARTAS DE LA NAVE DESBLOQUEADAS");
                nextCardRemaining.text = "";
                nextCardIcon.transform.parent.gameObject.SetActive(true);
                nextCardIcon.sprite = ArtLibrary.ArtOr("ic_trofeu", ArtLibrary.CircleFilled);
                nextCardIcon.color = UIFactory.Evolved;
                SetLabel(nextCardStrip, "IconBack/Mark", "");
                UIFactory.FadeGlow(nextCardGlow, WithAlpha(UIFactory.Evolved, 0.25f), 0f);
                return;
            }

            var left = next.unlockLevel - level;
            nextCardTitle.text = L("PRÓXIMA CARTA EXCLUSIVA", "NEXT EXCLUSIVE CARD", "PRÓXIMA CARTA EXCLUSIVA");
            nextCardRemaining.text = left == 1
                ? L("FALTA 1 NÍVEL!", "1 LEVEL TO GO!", "¡FALTA 1 NIVEL!")
                : string.Format(L("FALTAM {0} NÍVEIS", "{0} LEVELS TO GO", "FALTAN {0} NIVELES"), left);
            // A silhouette: the card's own emblem, painted black, so it is a tease and not a spoiler.
            nextCardIcon.sprite = CardIconFactory.Get(next.effect);
            nextCardIcon.color = Color.black;
            SetLabel(nextCardStrip, "IconBack/Mark", "?");

            // Close to the unlock: the strip glows and the count breathes.
            var close = left <= 2;
            UIFactory.FadeGlow(nextCardGlow, WithAlpha(UIFactory.Evolved, close ? 0.45f : 0.15f), 0.2f);
            if (close) Juice.BreatheUI(nextCardRemaining, 0.55f, 1f, 0.6f);
        }

        /// <summary>One marker per exclusive card along the total levels bar.</summary>
        private void RefreshLevelMilestones()
        {
            if (passiveLevelsFill == null || string.IsNullOrEmpty(passiveShipId)) return;
            var bar = (RectTransform)passiveLevelsFill.rectTransform.parent;
            if (levelMilestones == null)
            {
                levelMilestones = UIFactory.NewRect(bar, "Milestones");
                UIFactory.Stretch(levelMilestones);
            }
            for (var i = levelMilestones.childCount - 1; i >= 0; i--)
                Destroy(levelMilestones.GetChild(i).gameObject);

            var cap = PassiveUpgrades.MaxLevel * PassiveCount;
            var level = TotalLevels(passiveShipId);
            var next = NextExclusiveUnlock(passiveShipId);
            foreach (var card in LevelCards(passiveShipId))
            {
                var open = card.unlockLevel <= level;
                // Amber like the "next exclusive card" strip they stand for (the
                // grey rings did not say what they were); dimmer while locked.
                var marker = UIFactory.Rect(levelMilestones, "Card_" + card.id,
                    open ? UIFactory.Evolved : WithAlpha(UIFactory.Evolved, 0.55f), ArtLibrary.CircleFilled);
                marker.raycastTarget = false;
                marker.rectTransform.anchorMin = marker.rectTransform.anchorMax =
                    new Vector2(Mathf.Clamp01((float)card.unlockLevel / cap), 0.5f);
                marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                marker.rectTransform.sizeDelta = new Vector2(24f, 24f);
                marker.rectTransform.anchoredPosition = Vector2.zero;
                var core = UIFactory.Rect(marker.rectTransform, "Core",
                    open ? new Color(1f, 0.95f, 0.75f, 1f) : new Color(0.03f, 0.04f, 0.08f, 1f), ArtLibrary.CircleFilled);
                UIFactory.Stretch(core.rectTransform, 6f);
                core.raycastTarget = false;
                if (next != null && card == next) Juice.BreatheUI(marker, 0.45f, 1f, 0.8f);
            }
        }

        // ------------------------------------------------------------------
        // Purchase feedback
        // ------------------------------------------------------------------

        /// <summary>Called after a purchase was applied and the screen refreshed.</summary>
        private void AnimatePassivePurchase(int levelBefore, int levelAfter, int[] counts, CardDefinition unlocked)
        {
            var cap = PassiveUpgrades.MaxLevel * PassiveCount;
            passiveLevelsFill.fillAmount = (float)levelBefore / cap;
            Juice.FillTo(passiveLevelsFill, (float)levelAfter / cap, 0.55f);
            Juice.Punch(passiveLevels.transform, 0.18f, 0.35f);
            FloatText(passiveLevels.rectTransform, "+" + (levelAfter - levelBefore), UIFactory.Good);

            for (var i = 0; i < counts.Length && i < passiveNodes.Count; i++)
            {
                if (counts[i] == 0) continue;
                var node = passiveNodes[i];
                var now = run.passiveUpgrades.Level(passiveShipId, node.upgrade);
                node.fill.fillAmount = (float)(now - counts[i]) / PassiveUpgrades.MaxLevel;
                Juice.FillTo(node.fill, (float)now / PassiveUpgrades.MaxLevel, 0.45f);
            }

            if (nextCardStrip == null) return;
            var from = NextCardProgress(passiveShipId, levelBefore);
            nextCardFill.fillAmount = from;
            Juice.Punch(nextCardStrip, 0.05f, 0.3f);
            if (unlocked == null)
            {
                Juice.FillTo(nextCardFill, NextCardProgress(passiveShipId, levelAfter), 0.5f);
                return;
            }

            // The bar runs to the end, the strip lights up, then the reveal takes over.
            Juice.FillTo(nextCardFill, 1f, 0.4f);
            LeanTween.cancel(nextCardRemaining.gameObject);
            nextCardRemaining.alpha = 1f;
            nextCardRemaining.text = "";
            nextCardTitle.text = L("NOVA CARTA EXCLUSIVA!", "NEW EXCLUSIVE CARD!", "¡NUEVA CARTA EXCLUSIVA!");
            UIFactory.FlashGlow(nextCardGlow, WithAlpha(UIFactory.Evolved, 1f), WithAlpha(UIFactory.Evolved, 0.45f), 0.6f);
            var card = unlocked;
            LeanTween.delayedCall(nextCardStrip.gameObject, 0.45f, () => ShowShipCardReveal(card))
                .setIgnoreTimeScale(true);
        }

        /// <summary>A short label that rises and fades from a spot on the screen.</summary>
        private void FloatText(RectTransform from, string text, Color color)
        {
            if (from == null) return;
            var label = UIFactory.Label(from.parent, "Float", text, 44, color, TextAlignmentOptions.Left, true);
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = from.anchorMin;
            rect.anchorMax = from.anchorMax;
            rect.pivot = from.pivot;
            rect.sizeDelta = new Vector2(200f, 56f);
            var start = from.anchoredPosition + new Vector2(from.rect.width * 0.55f, 0f);
            rect.anchoredPosition = start;
            UIFactory.GlowShadow(label, WithAlpha(color, 0.6f), 16f, 0.05f);
            Juice.PopIn(rect, 1f, 0.25f, 0f, true);
            LeanTween.value(label.gameObject, t =>
            {
                if (label == null) return;
                rect.anchoredPosition = start + new Vector2(0f, 70f * t);
                label.alpha = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
            }, 0f, 1f, 1.1f).setEase(LeanTweenType.easeOutCubic).setIgnoreTimeScale(true)
                .setOnComplete(() => { if (label != null) Destroy(label.gameObject); });
        }

        // ------------------------------------------------------------------
        // Card reveal (ship exclusive cards and base deck purchases)
        // ------------------------------------------------------------------

        /// <summary>A ship level crossed an exclusive card's unlock level.</summary>
        private void ShowShipCardReveal(CardDefinition card)
        {
            var ship = config.GetShip(passiveShipId);
            ShowCardReveal(card,
                L("NOVA CARTA EXCLUSIVA!", "NEW EXCLUSIVE CARD!", "¡NUEVA CARTA EXCLUSIVA!"),
                ship != null ? GameLocalization.Content(ship.name).ToUpperInvariant() : "",
                ship != null ? SpriteFactory.ParseColor(ship.tint) : UIFactory.Accent,
                L("Agora ela pode aparecer nas escolhas desta nave.", "It can now show up in this ship's picks.",
                    "Ahora puede aparecer en las elecciones de esta nave."),
                AfterShipCardReveal);
        }

        /// <summary>Back on the upgrade screen: the strip points at the following card and fills again.</summary>
        private void AfterShipCardReveal()
        {
            RefreshNextCardStrip();
            RefreshLevelMilestones();
            if (nextCardStrip == null) return;
            var target = nextCardFill.fillAmount;
            nextCardFill.fillAmount = 0f;
            Juice.FillTo(nextCardFill, target, 0.5f);
            Juice.Punch(nextCardStrip, 0.06f, 0.3f);
        }

        /// <summary>
        /// Full-screen card reveal on the safe area root, so the veil and the
        /// light cover the whole screen (not just the page that opened it).
        /// </summary>
        private void ShowCardReveal(CardDefinition card, string headingText, string subtitleText, Color subtitleColor,
            string bodyText, Action onClosed)
        {
            if (card == null || safeRoot == null) return;
            if (cardReveal != null) Destroy(cardReveal.gameObject);

            Image veil;
            cardReveal = UIFactory.Page(safeRoot, "CardReveal", new Color(0.005f, 0.01f, 0.025f, 0.94f), out veil);
            UIFactory.Group(cardReveal).ignoreParentGroups = true;
            var tap = veil.gameObject.AddComponent<Button>();
            tap.targetGraphic = veil;
            tap.transition = Selectable.Transition.None;
            Rebind(tap, CloseCardReveal);
            revealCard = card;
            revealClosed = onClosed;
            revealShown = false;
            revealReady = false;

            var cardY = UIFactory.Pick(10f, 30f);

            // Light behind the card: a radial halo (soft all the way to its edge)
            // and two thin crossed flares that turn slowly.
            var halo = UIFactory.Rect(cardReveal, "Halo", WithAlpha(UIFactory.Evolved, 0f), ArtLibrary.Glow);
            halo.raycastTarget = false;
            halo.preserveAspect = true;
            UIFactory.Place(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, cardY),
                Vector2.one * UIFactory.Pick(880f, 980f));
            var rays = UIFactory.NewRect(cardReveal, "Rays");
            UIFactory.Place(rays, new Vector2(0.5f, 0.5f), new Vector2(0f, cardY), new Vector2(10f, 10f));
            for (var i = 0; i < 2; i++)
            {
                var flare = UIFactory.Rect(rays, "Flare" + i, WithAlpha(UIFactory.Evolved, 0f), ArtLibrary.LensFlare);
                flare.raycastTarget = false;
                UIFactory.Place(flare.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(UIFactory.Pick(1100f, 1000f), 70f));
                flare.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);
            }

            var heading = UIFactory.Label(cardReveal, "Heading", headingText, 58, UIFactory.Evolved,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, UIFactory.Pick(360f, 500f)),
                new Vector2(1000f, 80f));
            heading.characterSpacing = 8f;
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 32f;
            heading.fontSizeMax = 58f;
            UIFactory.AddUnderlay(heading);
            UIFactory.GlowShadow(heading, WithAlpha(UIFactory.Evolved, 0.5f), 28f, 0.05f);
            heading.gameObject.SetActive(false);

            var subtitle = UIFactory.Label(cardReveal, "Subtitle", subtitleText, 30, subtitleColor,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, UIFactory.Pick(300f, 430f)),
                new Vector2(1000f, 44f));
            subtitle.characterSpacing = 6f;
            subtitle.gameObject.SetActive(false);

            var slot = UIFactory.NewRect(cardReveal, "CardSlot");
            UIFactory.Place(slot, new Vector2(0.5f, 0.5f), new Vector2(0f, cardY), new Vector2(DeckCardWidth, DeckCardHeight));
            revealWidget = CardWidget.Create(slot, "NewCard", new Vector2(DeckCardWidth, DeckCardHeight));
            UIFactory.Place(revealWidget.root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DeckCardWidth, DeckCardHeight));
            revealWidget.Bind(new RuntimeCard(-1, card, null), 0, previewTint: UIFactory.Evolved);
            revealWidget.interactable = false;
            revealWidget.SetRestScale(UIFactory.Pick(1.45f, 1.9f));
            revealWidget.ShowUnlockMystery();

            var body = UIFactory.Label(cardReveal, "Body", bodyText, UIFactory.TextBody, BonusInk,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, UIFactory.Pick(-330f, -440f)),
                new Vector2(980f, 60f));
            body.enableAutoSizing = true;
            body.fontSizeMin = 24f;
            body.fontSizeMax = UIFactory.TextBody;
            body.gameObject.SetActive(false);

            var hint = UIFactory.Label(cardReveal, "Hint",
                L("TOQUE PARA CONTINUAR", "TAP TO CONTINUE", "TOCA PARA CONTINUAR"), 28, UIFactory.InkDim,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, UIFactory.Pick(-400f, -520f)),
                new Vector2(1000f, 44f));
            hint.characterSpacing = 6f;
            hint.gameObject.SetActive(false);

            cardReveal.SetAsLastSibling();
            FadeScreenIn(UIFactory.Group(cardReveal), 0.2f);
            Juice.PopIn(revealWidget.root, revealWidget.RestScale, 0.4f, 0f, true);
            // The halo starts faint while the card charges, then blooms on the flip.
            Juice.TintGraphic(halo, WithAlpha(UIFactory.Evolved, 0.12f), 0.6f);
            SyncAdNavigation();

            // The charge: three knocks from inside the silhouette, each a little harder.
            var host = cardReveal.gameObject;
            for (var i = 0; i < 3; i++)
            {
                var strength = 0.04f + 0.03f * i;
                LeanTween.delayedCall(host, 0.45f + 0.28f * i, () =>
                {
                    if (revealShown || revealWidget == null) return;
                    Juice.Punch(revealWidget.root, strength, 0.2f);
                    Sfx.Play(Sound.Select);
                }).setIgnoreTimeScale(true);
            }
            LeanTween.delayedCall(host, 1.35f, RevealCard).setIgnoreTimeScale(true);
        }

        /// <summary>The flip: the silhouette becomes the real card.</summary>
        private void RevealCard()
        {
            if (revealShown || cardReveal == null || revealWidget == null) return;
            revealShown = true;

            revealWidget.Bind(new RuntimeCard(-1, revealCard, null), 0);
            Juice.PopIn(revealWidget.root, revealWidget.RestScale, 0.45f, 0f, true);
            Juice.ScreenFlash(new Color(1f, 0.86f, 0.5f, 0.55f), 0.55f);
            Sfx.Play(Sound.UpgradeChosen);

            var halo = cardReveal.Find("Halo").GetComponent<Image>();
            Juice.TintGraphic(halo, WithAlpha(UIFactory.Evolved, 0.5f), 0.35f);
            Juice.PopIn(halo.transform, 1f, 0.5f, 0f, true);
            var rays = cardReveal.Find("Rays");
            for (var i = 0; i < rays.childCount; i++)
                Juice.TintGraphic(rays.GetChild(i).GetComponent<Image>(), WithAlpha(UIFactory.Evolved, 0.55f), 0.35f);
            LeanTween.rotateAroundLocal(rays.gameObject, Vector3.forward, 360f, 16f).setLoopClamp()
                .setIgnoreTimeScale(true);

            var heading = cardReveal.Find("Heading");
            heading.gameObject.SetActive(true);
            Juice.PopIn(heading, 1f, 0.35f, 0f, true);
            var subtitle = cardReveal.Find("Subtitle");
            subtitle.gameObject.SetActive(true);
            Juice.PopIn(subtitle, 1f, 0.35f, 0.08f, true);
            var body = cardReveal.Find("Body");
            body.gameObject.SetActive(true);
            Juice.PopIn(body, 1f, 0.35f, 0.16f, true);

            LeanTween.delayedCall(cardReveal.gameObject, 0.6f, () =>
            {
                if (cardReveal == null) return;
                revealReady = true;
                var hint = cardReveal.Find("Hint");
                hint.gameObject.SetActive(true);
                Juice.BreatheUI(hint.GetComponent<TMP_Text>(), 0.45f, 1f, 1.1f);
            }).setIgnoreTimeScale(true);
        }

        /// <summary>A tap during the charge skips to the reveal; after it, closes.</summary>
        private void CloseCardReveal()
        {
            if (cardReveal == null) return;
            if (!revealShown)
            {
                RevealCard();
                return;
            }
            if (!revealReady) return;
            Sfx.Play(Sound.Back);
            DismissCardReveal(true);
        }

        /// <summary>Closes the reveal at once (a page opening over it, a layout rebuild).</summary>
        private void DismissCardReveal(bool runCallback)
        {
            if (cardReveal == null) return;
            var reveal = cardReveal;
            var closed = revealClosed;
            cardReveal = null;
            revealWidget = null;
            revealClosed = null;
            HideScreen(reveal, () => { if (reveal != null) Destroy(reveal.gameObject); });
            SyncAdNavigation();
            if (runCallback && closed != null) closed();
        }
    }
}
