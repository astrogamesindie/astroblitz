// AstroCrawler - where the coin burst (CoinBurst) is wired into the screens.
//
//   - Menu rewards (daily, video, welcome login): the coins burst out of the
//     reward, the popup gets out of the way, and they land on the menu's
//     credits chip, which counts up.
//   - End of run: the result screen has its own credits chip (built at
//     runtime, so the baked layout does not change); it shows the balance
//     before the run, and the coins fly from the "+N CREDITS" line into it.

using TMPro;

using UnityEngine;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private const float CoinPopupCloseDelay = 0.28f;
        private const float EndCoinsDelay = 0.65f;

        private RectTransform endCreditsChip;
        private TMP_Text endCreditsValue;

        // ------------------------------------------------------------------
        // Menu
        // ------------------------------------------------------------------

        /// <summary>
        /// Coins from <paramref name="source"/> into the menu credits chip, for
        /// <paramref name="amount"/> credits already added to the wallet.
        /// </summary>
        private void FlyCreditsToMenu(Transform source, int amount)
        {
            if (amount <= 0 || source == null || canvas == null || mainMenu == null || menuCredits == null) return;
            var chip = mainMenu.Find("Credits") as RectTransform;
            if (chip == null) return;
            var landing = chip.Find("Icon") as RectTransform;
            var to = run.passiveUpgrades.credits;
            // highlight: the menu chip is usually covered (rewards/welcome
            // backdrop, the opaque release notes, the Ajustes page), so a lit
            // copy of it is drawn on top for the coins to land on.
            CoinBurst.Play(canvas.transform, source.position, landing != null ? landing : chip, chip, menuCredits,
                Mathf.Max(0, to - amount), to, UIFactory.Evolved, RefreshMainMenu, true);
        }

        /// <summary>Closes a popup a beat after a claim, so the coins land on a visible counter.</summary>
        private void ClosePopupAfterCoins(System.Action close)
        {
            LeanTween.delayedCall(gameObject, CoinPopupCloseDelay, () =>
            {
                if (this != null) close();
            }).setIgnoreTimeScale(true);
        }

        // ------------------------------------------------------------------
        // Result screen
        // ------------------------------------------------------------------

        private void EnsureEndCreditsChip()
        {
            if (endCreditsChip != null || endScreen == null) return;

            var chip = UIFactory.Panel9(endScreen, "Credits", UIFactory.PanelDeep);
            chip.raycastTarget = false;
            endCreditsChip = chip.rectTransform;
            UIFactory.Place(endCreditsChip, new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(300f, UIFactory.Chip));
            HudFrame(chip, UIFactory.Evolved);

            var icon = UIFactory.Rect(endCreditsChip, "Icon", UIFactory.Evolved, ArtLibrary.IconCredits);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(56f, 56f));

            endCreditsValue = UIFactory.Label(endCreditsChip, "Value", "0", 46, UIFactory.Evolved,
                TextAlignmentOptions.Right, true);
            endCreditsValue.raycastTarget = false;
            UIFactory.Place(endCreditsValue.rectTransform, new Vector2(1f, 0.5f), new Vector2(-26f, 0f),
                new Vector2(190f, 64f));
        }

        /// <summary>
        /// Called by ShowEndScreen once the body text is set. <paramref name="creditsTextIndex"/>
        /// is where the "+N" credits line starts in the body's source text.
        /// </summary>
        private void PlayEndCredits(int earned, int creditsTextIndex)
        {
            EnsureEndCreditsChip();
            if (endCreditsChip == null) return;

            var to = run.passiveUpgrades.credits;
            var from = Mathf.Max(0, to - Mathf.Max(0, earned));
            endCreditsValue.text = from.ToString();
            endCreditsChip.SetAsLastSibling();
            Juice.PopIn(endCreditsChip, 1f, 0.35f, 0.2f, true);
            if (earned <= 0) return;

            LeanTween.delayedCall(gameObject, EndCoinsDelay, () =>
            {
                if (this == null || endScreen == null || !endScreen.gameObject.activeInHierarchy) return;
                var landing = endCreditsChip.Find("Icon") as RectTransform;
                CoinBurst.Play(canvas.transform, CreditsLineWorld(creditsTextIndex),
                    landing != null ? landing : endCreditsChip, endCreditsChip, endCreditsValue, from, to,
                    UIFactory.Evolved);
            }).setIgnoreTimeScale(true);
        }

        /// <summary>The centre of the body line holding the credits, in world space; the body's centre as a fallback.</summary>
        private Vector3 CreditsLineWorld(int textIndex)
        {
            if (endBody == null) return Vector3.zero;
            endBody.ForceMeshUpdate();
            var info = endBody.textInfo;
            for (var i = 0; i < info.characterCount; i++)
            {
                var character = info.characterInfo[i];
                if (character.index < textIndex) continue;
                var line = info.lineInfo[character.lineNumber];
                var local = new Vector3((line.lineExtents.min.x + line.lineExtents.max.x) * 0.5f,
                    (line.ascender + line.descender) * 0.5f, 0f);
                return endBody.transform.TransformPoint(local);
            }
            return endBody.transform.position;
        }
    }
}
