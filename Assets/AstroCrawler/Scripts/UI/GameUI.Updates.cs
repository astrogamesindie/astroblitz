using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstroCrawler
{
    public partial class GameUI
    {
        private StoreUpdateGate updateGate;
        private RectTransform updateScreen;
        private TMP_Text updateTitle, updateBody;
        private Button updateButton, updateRetryButton;
        private bool updatePaused;
        private float timeBeforeUpdate;
        private CanvasGroup updateBackgroundControls;
        private bool controlsBeforeUpdate;

        private void BuildUpdateScreen()
        {
            if (updateGate != null) updateGate.Changed -= RefreshUpdateScreen;
            updateGate = StoreUpdateGate.EnsureCreated();
            if (updateGate == null) return;
            updateGate.Changed += RefreshUpdateScreen;
            if (updateScreen != null) { RefreshUpdateScreen(); return; }
            Image backdrop;
            updateScreen = UIFactory.Page(safeRoot, "MandatoryUpdate", new Color(0.02f, 0.03f, 0.06f, 1f), out backdrop);
            backdrop.raycastTarget = true;
            updateBackgroundControls = UIFactory.Group(safeRoot);
            UIFactory.Group(updateScreen).ignoreParentGroups = true;
            var panel = UIFactory.Panel9(updateScreen, "Panel", UIFactory.PanelSoft);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                UIFactory.Pick(new Vector2(1200f, 660f), new Vector2(1000f, 760f)));
            updateTitle = UIFactory.Label(panel.transform, "Title", "", 46, UIFactory.Accent,
                TextAlignmentOptions.Center, true);
            UIFactory.Place(updateTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                new Vector2(UIFactory.Pick(1100f, 940f), 90f));
            updateBody = UIFactory.Label(panel.transform, "Body", "", UIFactory.TextBody, UIFactory.Ink,
                TextAlignmentOptions.Center);
            UIFactory.Place(updateBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 35f),
                new Vector2(UIFactory.Pick(1080f, 920f), 260f));
            updateButton = UIFactory.Button(panel.transform, "Update", "", UIFactory.Primary,
                UIFactory.Ink, UIFactory.TextButton, Sound.Tap);
            UIFactory.Place(updateButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 155f), new Vector2(700f, 100f));
            Rebind(updateButton, () => updateGate.UpdateNow());
            updateRetryButton = UIFactory.Button(panel.transform, "Retry", "", UIFactory.PanelDeep,
                UIFactory.Ink, UIFactory.TextBody, Sound.Tap);
            UIFactory.Place(updateRetryButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(700f, 90f));
            Rebind(updateRetryButton, () => updateGate.CheckNow());
            RefreshUpdateScreen();
        }

        private void RefreshUpdateScreen()
        {
            if (updateGate == null || updateScreen == null) return;
            var blocked = updateGate.BlocksPlay && !ReleaseNotesState.Pending && !IntroScreenOpen;
            updateScreen.gameObject.SetActive(blocked);
            if (blocked)
            {
                updateScreen.SetAsLastSibling();
                if (!updatePaused)
                {
                    if (JuiceDirector.Instance != null) JuiceDirector.Instance.ClearHitStop();
                    timeBeforeUpdate = Time.timeScale;
                    Time.timeScale = 0f;
                    updatePaused = true;
                    controlsBeforeUpdate = updateBackgroundControls.interactable;
                    updateBackgroundControls.interactable = false;
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                }
            }
            else if (updatePaused)
            {
                Time.timeScale = run != null && (run.phase == RunPhase.UpgradeSelection || run.phase == RunPhase.Defeat)
                    ? 0f : run != null && run.phase == RunPhase.WaveInProgress ? timeBeforeUpdate : 1f;
                updatePaused = false;
                updateBackgroundControls.interactable = controlsBeforeUpdate;
            }
            updateTitle.text = updateGate.Required
                ? L("ATUALIZAÇÃO NECESSÁRIA", "UPDATE REQUIRED", "ACTUALIZACIÓN NECESARIA")
                : L("CONSULTANDO A LOJA", "CHECKING THE STORE", "CONSULTANDO LA TIENDA");
            updateBody.text = updateGate.Checking ? L("Verificando se existe uma nova versão…", "Checking for a newer version…", "Buscando una versión más reciente…") :
                updateGate.Launching ? L("Conclua a atualização no Google Play para continuar.", "Complete the Google Play update to continue.", "Completa la actualización de Google Play para continuar.") :
                updateGate.Status == "error" || updateGate.Status == "cancelled" ?
                    L("A atualização ainda é necessária. Tente novamente ou abra a loja para atualizar.", "The update is still required. Try again or open the store to update.", "La actualización sigue siendo necesaria. Reintenta o abre la tienda para actualizar.") :
                    L("Existe uma versão mais recente na loja. Atualize o Astro Blitz para continuar jogando.", "A newer version is available in the store. Update Astro Blitz to keep playing.", "Hay una versión más reciente en la tienda. Actualiza Astro Blitz para seguir jugando.");
            SetButtonLabel(updateButton, L("ATUALIZAR AGORA", "UPDATE NOW", "ACTUALIZAR AHORA"));
            SetButtonLabel(updateRetryButton, L("VERIFICAR NOVAMENTE", "CHECK AGAIN", "VOLVER A COMPROBAR"));
            updateButton.gameObject.SetActive(updateGate.Required);
            updateRetryButton.gameObject.SetActive(updateGate.Required);
            updateButton.interactable = updateGate.Required && !updateGate.Checking && !updateGate.Launching;
            updateRetryButton.interactable = !updateGate.Checking && !updateGate.Launching;
        }
    }
}
