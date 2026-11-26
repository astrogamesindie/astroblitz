using UnityEngine;
using TMPro;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private ShipAbilityButton specialControl;

        private void BuildSpecialControl()
        {
            using var gameplayFrames = UIFactory.GameplayFrames();
            if (specialControl == null)
            {
                var prefab = Resources.Load<ShipAbilityButton>("AstroCrawler/ShipAbilityButton");
                if (prefab == null) { Debug.LogError("[AstroCrawler] Missing ShipAbilityButton prefab."); return; }
                specialControl = Instantiate(prefab, hudRoot);
            }
            specialControl.Bind(run);
        }

        private bool BuildSpecialDetailsIcon(int slot)
        {
            using var gameplayFrames = UIFactory.GameplayFrames();
            var special = run?.ship?.Special;
            if (special == null || special.kind == ShipSpecialKind.None) return false;

            // A round gold energy badge with a question mark, distinct from the
            // hexagonal upgrade badges and their coloured level numbers.
            var button = UIFactory.RoundButton(upgradeIcons, "SpecialDetails", ArtLibrary.IconEnergy,
                ShipAbilityButton.ColorFor(run.ship.definition), IconSize, Sound.Tap);
            var holder = (RectTransform)button.transform;
            UIFactory.Place(holder, new Vector2(0f, 1f),
                new Vector2(slot % IconColumns * IconStep, slot / IconColumns * -IconStep),
                new Vector2(IconSize, IconSize));
            var question = UIFactory.Label(holder, "Question", "?", 24,
                Color.white, TextAlignmentOptions.BottomRight, true);
            UIFactory.Place(question.rectTransform, new Vector2(1f, 0f), new Vector2(2f, -2f),
                new Vector2(26f, 28f));
            question.raycastTarget = false;
            UIFactory.AddUnderlay(question);
            button.onClick.AddListener(ShowSpecialDetails);
            return true;
        }

        private void ShowSpecialDetails()
        {
            var ship = run?.ship;
            var special = ship?.Special;
            if (special == null || special.kind == ShipSpecialKind.None || !ship.isAlive ||
                upgradeDetailsScreen == null || upgradeDetailsScreen.gameObject.activeSelf) return;

            var title = GameLocalization.Choose(special.name, special.nameEn, special.nameEs);
            var description = GameLocalization.Choose(special.description, special.descriptionEn, special.descriptionEs);
            description += "\n" + L("Recarga: ", "Cooldown: ", "Recarga: ") + ship.SpecialCooldown.ToString("0.#") + "s";
            if (special.duration > 0f)
                description += "\n" + L("Duração: ", "Duration: ", "Duración: ") + special.duration.ToString("0.#") + "s";

            var status = ship.SpecialActiveLeft > 0f
                ? L("ATIVA · ", "ACTIVE · ", "ACTIVA · ") + Mathf.CeilToInt(ship.SpecialActiveLeft) + "s"
                : ship.SpecialCooldownLeft > 0f
                    ? L("EM RECARGA · ", "RECHARGING · ", "EN RECARGA · ") + Mathf.CeilToInt(ship.SpecialCooldownLeft) + "s"
                    : L("PRONTA", "READY", "LISTA");
            if (ship.SpecialActiveLeft > 0f)
                description += "\n" + L("Recarga restante: ", "Cooldown left: ", "Recarga restante: ") +
                    Mathf.CeilToInt(ship.SpecialCooldownLeft) + "s";

            var icon = ShipAbilityButton.IconFor(special.kind);
            SetLabel(upgradeDetailsScreen, "Heading", L("HABILIDADE ESPECIAL", "SPECIAL ABILITY", "HABILIDAD ESPECIAL"));
            upgradeDetailsCard.BindInformation(title, description, status,
                ShipAbilityButton.ColorFor(ship.definition), icon);
            ShowDetailsOverlay();
        }
    }
}
