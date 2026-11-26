// AstroCrawler - resets everything static at the start of a play session.
//
// This project has "Enter Play Mode Options" enabled with both Domain Reload and
// Scene Reload disabled (ProjectSettings/EditorSettings.asset,
// m_EnterPlayModeOptions: 3). That makes entering play mode fast, at the price
// of C# statics surviving from the previous session while every UnityEngine.Object
// they point at has been destroyed.
//
// The symptom that led here: LeanTween guards its setup with "if (tweens == null)",
// so on the second play session it skipped creating its ~LeanTween updater - the
// object that had just been destroyed. Tweens still registered and never ticked,
// which left every screen frozen at the alpha its fade-in started from, with
// nothing in the console to explain it.
//
// RuntimeInitializeOnLoadMethod with SubsystemRegistration runs at the start of
// every play session whether or not the domain reloaded, which is exactly the
// hook this needs.

using UnityEngine;

namespace AstroCrawler
{
    public static class StaticState
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            // Nulls LeanTween's arrays so the next init() rebuilds the updater.
            LeanTween.reset();

            SpriteFactory.Clear();
            ArtLibrary.Clear();
            GameLocalization.ResetSession();
            UIFactory.ResetFont();
            UIFactory.ResetGlowTweens();
            Juice.ResetPunches();
            GameSpeed.Reset();
            StageProgress.Reset();

            Impact.Dispose();
            Projectile.ResetPool();
            XpOrb.ResetPool();
            BlackHole.Active.Clear();
            PrefabLibrary.SetActive(null);
        }
    }
}
