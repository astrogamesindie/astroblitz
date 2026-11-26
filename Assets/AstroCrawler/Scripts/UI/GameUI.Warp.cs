// AstroCrawler - every way into a stage goes through the warp.

using System;

using UnityEngine;

namespace AstroCrawler
{
    public partial class GameUI
    {
        /// <summary>
        /// Plays the jump to <paramref name="stage"/> (WarpTransition), with
        /// <paramref name="ship"/> in the middle of the star field, and runs
        /// <paramref name="begin"/> - which starts the run - at its flash. The
        /// whole UI fades out under the jump and is back, already on the new
        /// phase, when the flash clears.
        /// </summary>
        private void WarpToStage(int stage, ShipDefinition ship, Action begin)
        {
            if (begin == null || WarpTransition.Running) return;
            // The menu banner would sit on top of the jump; the new phase brings it back if it belongs.
            if (ads != null) ads.SetMenuVisible(false);
            var shipTint = ship != null ? SpriteFactory.ParseColor(ship.tint) : UIFactory.Accent;
            var started = WarpTransition.Play(StageCatalog.Accent(stage), shipTint,
                L("SALTO ESPACIAL", "SPACE JUMP", "SALTO ESPACIAL"), run.isEndless ? StageCatalog.EndlessName : StageCatalog.Name(stage),
                canvas != null ? UIFactory.Group(canvas) : null, begin, ship != null ? ship.id : null);
            if (!started) begin();
        }
    }
}
