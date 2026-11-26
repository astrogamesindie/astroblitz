// AstroCrawler - records the gameplay shots used by the promotional trailer.
//
// Use in Play Mode: Tools > AstroCrawler > Trailer > Gravar cenas do trailer.
// The Game view switches to 1080 x 1920, the game is driven by script in
// English (menu, jump, card picks, special, boss, hangar, upgrades, base deck)
// and every frame is written as a JPG to <project>/Trailer/raw/<shot>/ at a
// fixed 30 fps (Time.captureDeltaTime), so slow captures never drop frames.
// Taps and other moments the edit needs are logged in Trailer/raw/events.csv.
// The save is captured first (ProgressSnapshot) and put back at the end;
// nothing the shoot unlocks or buys stays in the save.
// Editor only: private game methods are reached through reflection.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

using Object = UnityEngine.Object;

namespace AstroCrawler
{
    public static class AstroCrawlerTrailerCapture
    {
        public const int Fps = 30;
        private const int JpgQuality = 93;
        private const int BusyStage = 7;
        private const int Width = 1080;
        private const int Height = 1920;

        // Cards that read well on video, best first.
        private static readonly string[] VisualCards =
        {
            "double_attack", "chain_lightning", "missiles", "orbital_blades", "support_drones", "diagonal_shot",
            "plasma_aura", "black_hole", "solar_nova", "orbital_strike", "wild_volley", "piercing_shot", "shrapnel",
            "railgun"
        };

        private static string root;
        private static string shotDir;
        private static string shotName;
        private static int frame;
        private static readonly StringBuilder events = new StringBuilder();
        private static bool active;
        private static bool keepAlive;
        private static bool autoPick;
        private static string snapshot;
        private static int savedLanguage;

        public static bool Active { get { return active; } }

        [MenuItem("Tools/AstroCrawler/Trailer/Gravar cenas do trailer", false, 120)]
        public static void Record()
        {
            var game = GameManager.Instance;
            if (!EditorApplication.isPlaying || game == null || game.run == null || game.ui == null)
            {
                Debug.LogWarning("[AstroCrawler] Trailer: entre no Play Mode e espere o menu antes de gravar.");
                return;
            }
            if (active) return;
            if (Screen.width != Width || Screen.height != Height)
            {
                Debug.LogWarning("[AstroCrawler] Trailer: o Game view precisa estar em " + Width + " x " + Height +
                    " (Tools > AstroCrawler > Tela > Celular retrato 16:9); está em " + Screen.width + " x " + Screen.height + ".");
                return;
            }
            active = true;

            root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Trailer", "raw");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(root);
            events.Length = 0;
            events.AppendLine("shot,frame,kind,x,y,info");
            Status("start");

            game.StartCoroutine(Grabber());
            game.StartCoroutine(Helpers(game.run));
            game.StartCoroutine(Director(game));
        }

        [MenuItem("Tools/AstroCrawler/Trailer/Gravar cenas do trailer", true)]
        private static bool CanRecord()
        {
            return EditorApplication.isPlaying && !active;
        }

        /// <summary>Stops the shoot early and puts the save back.</summary>
        [MenuItem("Tools/AstroCrawler/Trailer/Parar e restaurar o save", false, 121)]
        public static void Abort()
        {
            if (!active) return;
            var game = GameManager.Instance;
            if (game != null) game.StopAllCoroutines();
            Restore(game);
        }

        [MenuItem("Tools/AstroCrawler/Trailer/Parar e restaurar o save", true)]
        private static bool CanAbort()
        {
            return active;
        }

        // ------------------------------------------------------------------
        // Shot list
        // ------------------------------------------------------------------

        private static IEnumerator Director(GameManager game)
        {
            var run = game.run;
            var ui = game.ui;
            var config = game.config;

            snapshot = ProgressSnapshot.Capture(config);
            File.WriteAllText(Path.Combine(root, "save_backup.json"), snapshot);
            savedLanguage = (int)GameLocalization.Current;

            StageProgress.readOnly = true;
            GameSpeed.forceNormal = true;
            AudioListener.pause = true;
            Application.targetFrameRate = -1;
            Time.captureDeltaTime = 1f / Fps;

            // English, every ship open and the cards that film well.
            GameLocalization.Set(GameLocalization.Language.English);
            Call(ui, "RefreshLanguage");
            GameLanguageFonts.ApplyScene();
            for (var i = 0; i < config.ships.Count; i++)
                PlayerPrefs.SetInt("AstroCrawler.Ships.v1.unlocked." + config.ships[i].id, 1);
            for (var i = 0; i < 9; i++)
                PlayerPrefs.SetInt("AstroCrawler.BaseDeck.v1.unlocked." + VisualCards[i], 1);
            run.passiveUpgrades.AddRewardedCredits(20000);
            var levels = new int[8];
            for (var i = 0; i < levels.Length; i++) levels[i] = 18;
            for (var i = 0; i < config.ships.Count; i++) run.passiveUpgrades.OverrideLevels(config.ships[i].id, levels);

            Call(ui, "SelectShip", "vanguard");
            Call(ui, "GoToMainMenu");
            yield return Wait(1f);

            // 1. Menu, the jump into the sector and the opening pick.
            Begin("01_menu_warp");
            yield return Wait(1.4f);
            Mark("play", ui.transform, "Play");
            Call(ui, "StartSelectedShip");
            yield return Until(() => run.phase == RunPhase.UpgradeSelection && !WarpTransition.Running, 8f);
            yield return Wait(1.4f);
            TapCard(ui, run);
            yield return Wait(2.6f);
            End();

            // Orion in a busy sector, built up off camera.
            yield return BuildRun(run, config.GetShip("orion"), 8);
            yield return Wait(3f);

            // 2. The fight: big meteors breaking apart, then interceptors.
            Begin("02_gameplay");
            yield return Wait(10f);
            End();
            JumpToWave(run, 9);
            yield return Wait(2.5f);
            Begin("02b_gameplay");
            yield return Wait(7f);
            End();

            // 3. A level-up pick in the middle of the wave.
            autoPick = false;
            Begin("03_levelup");
            yield return Wait(1f);
            run.AddXp(run.xpToNext);
            yield return Until(() => run.phase == RunPhase.UpgradeSelection, 3f);
            yield return Wait(1.5f);
            TapCard(ui, run);
            yield return Wait(2f);
            End();
            autoPick = true;

            // 5. The boss, from its entrance to the result screen.
            var world = Object.FindFirstObjectByType<CombatWorld>();
            Begin("05_boss");
            yield return Wait(0.5f);
            JumpToWave(run, RunController.WavesPerRun);
            Mark("boss", null, "start");
            var elapsed = 0f;
            var nextHit = 9f;
            while (elapsed < 22f && run.phase != RunPhase.Victory && run.phase != RunPhase.Defeat)
            {
                // Keep the clip short: after a while the boss loses health faster.
                if (elapsed >= nextHit && world != null && world.boss != null && world.boss.isAlive)
                {
                    world.boss.TakeDamage(world.boss.maxHp * 0.07f);
                    nextHit += 0.4f;
                }
                yield return null;
                elapsed += 1f / Fps;
            }
            Mark("result", null, run.phase.ToString());
            yield return Wait(5f);
            End();

            // 4. The Vanguard's special (Overdrive) in a crowded wave.
            run.Restart();
            yield return Wait(0.3f);
            yield return BuildRun(run, config.GetShip("vanguard"), 9);
            yield return Wait(2.5f);
            SetAuto(run.ship, "SpecialCooldownLeft", 0f);
            Begin("04_special");
            yield return Wait(1f);
            var ability = Object.FindFirstObjectByType<ShipAbilityButton>();
            if (ability != null) Mark("tap", ability.transform, "special");
            run.ActivateShipSpecial();
            yield return Wait(5f);
            End();

            // 6. Hangar: the five ships.
            keepAlive = false;
            autoPick = false;
            run.Restart();
            yield return Wait(0.5f);
            Call(ui, "GoToMainMenu");
            yield return Wait(0.3f);
            Call(ui, "OpenMenuSection", 1);
            yield return Wait(0.8f);
            Begin("06_hangar");
            yield return Wait(1f);
            for (var i = 0; i < 4; i++)
            {
                var next = FindButton(ui.transform, "ShipNext");
                if (next != null)
                {
                    Mark("tap", next.transform, "next");
                    next.onClick.Invoke();
                }
                yield return Wait(1.1f);
            }
            End();

            // 7. Permanent upgrades.
            Call(ui, "GoToMainMenu");
            yield return Wait(0.3f);
            Call(ui, "SelectShip", "vanguard");
            Call(ui, "OpenMenuSection", 3);
            yield return Wait(0.8f);
            Begin("07_upgrades");
            yield return Wait(1f);
            for (var i = 0; i < 3; i++)
            {
                Mark("buy", null, "passive");
                Call(ui, "BuyPassive", 1);
                yield return Wait(0.8f);
            }
            yield return Wait(0.8f);
            End();

            // 8. Base deck: buying a random card.
            Call(ui, "GoToMainMenu");
            yield return Wait(0.3f);
            Call(ui, "OpenMenuSection", 6);
            yield return Wait(0.8f);
            Begin("08_basedeck");
            yield return Wait(1f);
            Mark("buy", null, "card");
            Call(ui, "BuyBaseDeckCard");
            yield return Wait(3.5f);
            End();

            // 9. Main menu, for the end card.
            Call(ui, "GoToMainMenu");
            yield return Wait(0.6f);
            Begin("09_menu");
            yield return Wait(4f);
            End();

            Restore(game);
        }

        // ------------------------------------------------------------------
        // Recording
        // ------------------------------------------------------------------

        private static void Begin(string name)
        {
            shotName = name;
            shotDir = Path.Combine(root, name);
            Directory.CreateDirectory(shotDir);
            frame = 0;
            Status("shot " + name);
        }

        private static void End()
        {
            Status("done " + shotName + " frames=" + frame);
            shotDir = null;
        }

        private static IEnumerator Grabber()
        {
            var endOfFrame = new WaitForEndOfFrame();
            var lastFrame = -1;
            var gaps = 0;
            var wrongSize = false;
            while (active)
            {
                yield return endOfFrame;
                if (shotDir == null)
                {
                    lastFrame = -1;
                    continue;
                }
                // A frame the Game view skipped would be a jump in the clip.
                if (lastFrame >= 0 && Time.frameCount != lastFrame + 1 && ++gaps % 10 == 1)
                    Status("gap at " + shotName + " frame " + frame + " (" + gaps + " gaps)");
                lastFrame = Time.frameCount;
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (!wrongSize && (texture.width != Width || texture.height != Height))
                {
                    wrongSize = true;
                    Status("WRONG capture size " + texture.width + "x" + texture.height + " at " + shotName);
                }
                var bytes = texture.EncodeToJPG(JpgQuality);
                Object.Destroy(texture);
                File.WriteAllBytes(Path.Combine(shotDir, frame.ToString("D5") + ".jpg"), bytes);
                frame++;
            }
        }

        /// <summary>Keeps the ship alive and answers picks while the camera is off them.</summary>
        private static IEnumerator Helpers(RunController run)
        {
            var waiting = 0;
            while (active)
            {
                var ship = run.ship;
                if (keepAlive && ship != null && ship.isAlive && ship.hpFraction < 0.45f) ship.Heal(ship.maxHp);

                if (autoPick && run.phase == RunPhase.UpgradeSelection && !WarpTransition.Running)
                {
                    if (++waiting > 3)
                    {
                        waiting = 0;
                        var best = Best(run);
                        if (best != null) run.ConfirmUpgrade(best);
                    }
                }
                else
                {
                    waiting = 0;
                }
                yield return null;
            }
        }

        private static void Restore(GameManager game)
        {
            shotDir = null;
            keepAlive = false;
            autoPick = false;
            try
            {
                if (game != null && game.run != null)
                {
                    if (game.run.phase != RunPhase.ShipSelection) game.run.Restart();
                    ProgressSnapshot.Apply(game.config, snapshot);
                    game.run.passiveUpgrades.ReloadLevels();
                }
                GameLocalization.Set((GameLocalization.Language)savedLanguage);
                if (game != null && game.ui != null) Call(game.ui, "RefreshLanguage");
                PlayerPrefs.Save();
            }
            finally
            {
                StageProgress.readOnly = false;
                GameSpeed.forceNormal = false;
                AudioListener.pause = false;
                Time.captureDeltaTime = 0f;
                File.WriteAllText(Path.Combine(root, "events.csv"), events.ToString());
                Status("finished");
                active = false;
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static IEnumerator Wait(float seconds)
        {
            var frames = Mathf.RoundToInt(seconds * Fps);
            for (var i = 0; i < frames; i++) yield return null;
        }

        private static IEnumerator Until(Func<bool> condition, float timeout)
        {
            var frames = Mathf.RoundToInt(timeout * Fps);
            for (var i = 0; i < frames && !condition(); i++) yield return null;
        }

        private static RuntimeCard Best(RunController run)
        {
            var choices = run.cards.Choices;
            if (choices.Count == 0) return null;
            var best = 0;
            var bestRank = int.MaxValue;
            for (var i = 0; i < choices.Count; i++)
            {
                var rank = Array.IndexOf(VisualCards, choices[i].definition.id);
                if (rank < 0) rank = 100 + i;
                if (rank < bestRank)
                {
                    bestRank = rank;
                    best = i;
                }
            }
            return choices[best];
        }

        private static void TapCard(GameUI ui, RunController run)
        {
            var widgets = Get(ui, "evolutionWidgets") as List<CardWidget>;
            var best = Best(run);
            if (widgets == null || best == null) return;
            for (var i = 0; i < widgets.Count; i++)
            {
                if (widgets[i] == null || widgets[i].card != best) continue;
                Mark("tap", widgets[i].transform, "card " + best.definition.id);
                var button = widgets[i].GetComponent<Button>();
                if (button != null) button.onClick.Invoke();
                return;
            }
        }

        /// <summary>Starts a run in the busy sector and levels the ship up off camera.</summary>
        private static IEnumerator BuildRun(RunController run, ShipDefinition ship, int wave)
        {
            if (run.phase != RunPhase.ShipSelection)
            {
                run.Restart();
                yield return Wait(0.3f);
            }
            autoPick = true;
            keepAlive = true;
            run.BeginRun(ship, BusyStage);
            yield return Until(() => run.phase == RunPhase.WaveInProgress, 4f);
            for (var i = 0; i < 8; i++)
            {
                run.AddXp(run.xpToNext);
                yield return Wait(0.25f);
                yield return Until(() => run.phase == RunPhase.WaveInProgress, 4f);
            }
            JumpToWave(run, wave);
        }

        /// <summary>Editor test hook: starts wave <paramref name="wave"/> of the current run right away.</summary>
        public static void JumpToWaveForTest(RunController run, int wave)
        {
            if (run != null) JumpToWave(run, wave);
        }

        private static void JumpToWave(RunController run, int wave)
        {
            SetAuto(run, "waveIndex", wave - 1);
            Call(run, "StartNextWave");
        }

        private static Button FindButton(Transform rootTransform, string name)
        {
            var buttons = rootTransform.GetComponentsInChildren<Button>(false);
            for (var i = 0; i < buttons.Length; i++)
                if (buttons[i].name == name) return buttons[i];
            var all = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }

        /// <summary>Logs a moment of the current shot; a UI transform also gives its screen position.</summary>
        private static void Mark(string kind, Transform target, string info)
        {
            var x = -1f;
            var y = -1f;
            var rect = target as RectTransform;
            if (rect != null)
            {
                var canvas = rect.GetComponentInParent<Canvas>();
                var camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.rootCanvas.worldCamera : null;
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                var point = RectTransformUtility.WorldToScreenPoint(camera, (corners[0] + corners[2]) * 0.5f);
                x = point.x;
                y = Screen.height - point.y;
            }
            events.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:0},{4:0},{5}",
                shotName ?? "-", frame, kind, x, y, info));
        }

        private static void Status(string line)
        {
            if (root == null) return;
            File.AppendAllText(Path.Combine(root, "status.txt"),
                DateTime.Now.ToString("HH:mm:ss") + " " + line + "\n");
        }

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static object Call(object target, string method, params object[] args)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var methods = type.GetMethods(Any | BindingFlags.DeclaredOnly);
                for (var i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m.Name != method) continue;
                    var parameters = m.GetParameters();
                    if (parameters.Length < args.Length) continue;
                    var values = new object[parameters.Length];
                    var ok = true;
                    for (var p = 0; p < parameters.Length; p++)
                    {
                        if (p >= args.Length)
                        {
                            if (!parameters[p].HasDefaultValue) { ok = false; break; }
                            values[p] = parameters[p].DefaultValue;
                            continue;
                        }
                        var kind = parameters[p].ParameterType;
                        values[p] = kind.IsEnum && args[p] is int ? Enum.ToObject(kind, args[p]) : args[p];
                    }
                    if (!ok) continue;
                    return m.Invoke(target, values);
                }
            }
            Status("missing method " + method);
            return null;
        }

        private static object Get(object target, string field)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var info = type.GetField(field, Any | BindingFlags.DeclaredOnly);
                if (info != null) return info.GetValue(target);
            }
            Status("missing field " + field);
            return null;
        }

        /// <summary>Sets an auto-property's private backing field.</summary>
        private static void SetAuto(object target, string property, object value)
        {
            var name = "<" + property + ">k__BackingField";
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var info = type.GetField(name, Any | BindingFlags.DeclaredOnly);
                if (info == null) continue;
                info.SetValue(target, value);
                return;
            }
            Status("missing property " + property);
        }
    }
}
