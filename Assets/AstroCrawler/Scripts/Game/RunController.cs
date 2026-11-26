// AstroCrawler - the run state machine (GDD 2).
//
// One run is: use the selected ship and stage, pick a starting upgrade, and
// survive ten waves that follow one another without a break. Kills drop XP;
// XP raises the ship's level, and every level is one permanent upgrade pick,
// taken in the middle of the fight with the game frozen. The boss sheds XP as
// it loses health, so the last wave has picks too. Death at any point ends it.
// This class owns that loop and nothing else - combat lives in CombatWorld,
// card rules in CardSystem, presentation in GameUI.

using System;

using UnityEngine;

namespace AstroCrawler
{
    public class RunController : MonoBehaviour
    {
        /// <summary>A full run is ten waves (GDD 1).</summary>
        public const int WavesPerRun = 10;

        public RunPhase phase { get; private set; }
        public int waveIndex { get; private set; }
        public int stageIndex { get; private set; }
        public bool isEndless { get; private set; }
        public bool endlessUnlocked => StageCatalog.EndlessUnlocked(config);
        public int completedWaves { get; private set; }
        public int earnedCredits => runCreditsPaid;
        public string modeName => isEndless ? StageCatalog.EndlessName : StageCatalog.Name(stageIndex);
        public int waveTrackLength => isEndless ? StageCatalog.EndlessCycleLength : WavesPerRun;
        public int waveTrackIndex => isEndless ? Mathf.Max(0, waveIndex - 1) % StageCatalog.EndlessCycleLength : waveIndex - 1;

        public bool SelectEndless()
        {
            if (phase != RunPhase.ShipSelection || !endlessUnlocked) return false;
            isEndless = true;
            return true;
        }

        /// <summary>Back to the campaign, on the stage that was selected before.</summary>
        public void SelectCampaign()
        {
            if (phase == RunPhase.ShipSelection) isEndless = false;
        }

        /// <summary>Endless points of this run (EndlessScore); 0 in the campaign.</summary>
        public int score { get; private set; }

        /// <summary>True when the endless run that just ended beat the local record.</summary>
        public bool newRecord { get; private set; }

        /// <summary>Ship level inside this run. Starts at 1; each further level is one pick.</summary>
        public int level { get; private set; }

        /// <summary>XP collected towards the next level.</summary>
        public int xp { get; private set; }

        public int xpToNext
        {
            get { return config != null ? config.progression.XpForLevel(level) : 1; }
        }

        public float xpFraction
        {
            get { return isMaxLevel ? 1f : Mathf.Clamp01(xp / (float)xpToNext); }
        }

        /// <summary>True once the ship reached the level cap; no more XP is dropped or counted.</summary>
        public bool isMaxLevel
        {
            get
            {
                if (isEndless) return level >= 12;
                return config != null && config.progression.maxLevel > 0 &&
                    level >= config.progression.maxLevel;
            }
        }

        /// <summary>
        /// Levels already earned beyond the pick on screen. Several can arrive
        /// together; the pick screen uses this to name the level it is paying.
        /// </summary>
        public int pendingPicksAfterThis
        {
            get { return Mathf.Max(0, pendingPicks - 1); }
        }

        /// <summary>Enemies left in the current wave; see WaveController.RemainingEnemies.</summary>
        public int enemiesRemaining
        {
            get { return waves != null ? waves.RemainingEnemies() : 0; }
        }

        private const string SelectedStageKey = "AstroCrawler.SelectedStage";

        public CardSystem cards { get; private set; }
        public PlayerShip ship { get; private set; }
        public ShipDefinition shipDefinition { get; private set; }
        public PassiveUpgrades passiveUpgrades { get; private set; }

        /// <summary>Which base deck and exclusive cards can come up (CardUnlocks).</summary>
        public CardUnlocks cardUnlocks { get; private set; }

        /// <summary>Which ships the player owns (Vanguarda-7 free, the rest bought).</summary>
        public ShipUnlocks shipUnlocks { get; private set; }
        private float xpRemainder;

        public event Action<RunPhase> onPhaseChanged;
        public event Action<WaveDefinition> onWaveStarted;
        public event Action<int> onWaveCleared;

        /// <summary>Raised when collected XP crosses a level, with the new level.</summary>
        public event Action<int> onLevelUp;

        private GameConfig config;
        private CombatWorld world;
        private EffectRunner effects;
        private WaveController waves;

        // Levels earned whose pick has not been made yet. XP can arrive while
        // the game is paused for something else, or several levels at once.
        private int pendingPicks;

        // The pick shown before the first wave: confirming it starts the run
        // instead of resuming a fight.
        private bool startingPick;

        // A wave that was beaten while a pick was on screen; it is completed
        // when the fight resumes, or the run would stall with an empty arena.
        private bool waveClearPending;

        // Counts down the breather between a cleared wave and the next one.
        private float nextWaveTimer = -1f;

        private int bossDropsGiven;
        private float runTime;
        private bool reviveUsed;
        private int runCreditsPaid;

        /// <summary>True when the run that just ended beat its stage for the first time.</summary>
        public bool stageNewlyCleared { get; private set; }
        public bool CanRevive => !isEndless && phase == RunPhase.Defeat && !reviveUsed && ship != null && !ship.isAlive;

        public float runDuration
        {
            get { return runTime; }
        }

        public void Initialize(GameConfig config, CombatWorld world, EffectRunner effects, WaveController waves)
        {
            this.config = config;
            this.world = world;
            this.effects = effects;
            this.waves = waves;
            passiveUpgrades = new PassiveUpgrades();
            cardUnlocks = new CardUnlocks(config, passiveUpgrades);
            shipUnlocks = new ShipUnlocks(passiveUpgrades);
            stageIndex = Mathf.Clamp(PlayerPrefs.GetInt(SelectedStageKey, 0), 0, StageCatalog.Count - 1);

            cards = new CardSystem();
            level = 1;

            waves.onWaveCleared -= HandleWaveCleared;
            waves.onWaveCleared += HandleWaveCleared;
            world.onEnemyKilled -= HandleEnemyKilled;
            world.onEnemyKilled += HandleEnemyKilled;

            SetPhase(RunPhase.ShipSelection);
        }

        private void OnDestroy()
        {
            if (waves != null) waves.onWaveCleared -= HandleWaveCleared;
            if (world != null) world.onEnemyKilled -= HandleEnemyKilled;
        }

        private void Update()
        {
            if (phase != RunPhase.WaveInProgress)
            {
                return;
            }

            var dt = Time.deltaTime;
            runTime += dt;
            if (ship != null) ship.TickSpecial(dt);

            TickBossDrops();

            if (nextWaveTimer >= 0f)
            {
                nextWaveTimer -= dt;
                if (nextWaveTimer < 0f)
                {
                    StartNextWave();
                }
            }

            // Not while time is stopped: the pause menu and the upgrade
            // details hold the game at zero and a pick opening underneath
            // them would be answered blind.
            if (pendingPicks > 0 && Time.timeScale > 0f)
            {
                OpenLevelPick();
            }
        }

        // ------------------------------------------------------------------
        // Run lifecycle
        // ------------------------------------------------------------------

        /// <summary>
        /// Selects a stage for a ship. Refused (false) when that ship has not
        /// beaten the stage before it.
        /// </summary>
        public bool SelectStage(int index, string shipId)
        {
            if (phase != RunPhase.ShipSelection || index < 0 || index >= StageCatalog.Count) return false;
            if (!StageProgress.IsUnlocked(shipId, index)) return false;
            isEndless = false;
            SetStage(index);
            return true;
        }

        /// <summary>
        /// A different ship was picked: go back to the stage that ship is
        /// working on, which is not necessarily the one the last ship was on.
        /// </summary>
        public void SyncStageToShip(string shipId)
        {
            if (phase != RunPhase.ShipSelection) return;
            SetStage(StageProgress.CurrentStage(shipId));
        }

        /// <summary>Pulls the selected stage back inside what the ship has unlocked.</summary>
        public void ClampStageToShip(string shipId)
        {
            if (!StageProgress.IsUnlocked(shipId, stageIndex))
            {
                SetStage(StageProgress.CurrentStage(shipId));
            }
        }

        private void SetStage(int index)
        {
            stageIndex = Mathf.Clamp(index, 0, StageCatalog.Count - 1);
            PlayerPrefs.SetInt(SelectedStageKey, stageIndex);
            ProgressSnapshot.SaveLocal();
        }

        /// <summary>Starts a run in a given stage, which also becomes the selected one.</summary>
        public void BeginRun(ShipDefinition definition, int stage)
        {
            isEndless = false;
            stageIndex = Mathf.Clamp(stage, 0, StageCatalog.Count - 1);
            PlayerPrefs.SetInt(SelectedStageKey, stageIndex);
            ProgressSnapshot.SaveLocal();
            BeginRun(definition);
        }

        public bool CanActivateSpecial => phase == RunPhase.WaveInProgress && Time.timeScale > 0f &&
            ship != null && ship.isAlive && ship.Special != null && ship.Special.kind != ShipSpecialKind.None &&
            ship.SpecialCooldownLeft <= 0f && ship.SpecialActiveLeft <= 0f &&
            (StoreUpdateGate.Instance == null || !StoreUpdateGate.Instance.BlocksPlay) &&
            (AndroidAds.Instance == null || !AndroidAds.Instance.Showing);

        public bool ActivateShipSpecial()
        {
            return CanActivateSpecial && ship.ActivateSpecial();
        }

        public void BeginRun(ShipDefinition definition)
        {
            if (StoreUpdateGate.Instance != null && StoreUpdateGate.Instance.BlocksPlay) return;
            if (isEndless && !endlessUnlocked) return;
            // Restarting from the pause menu ends the endless run that was going.
            if (phase == RunPhase.WaveInProgress || phase == RunPhase.UpgradeSelection) RecordEndlessScore();
            TearDown();

            shipDefinition = definition;
            runTime = 0f;
            waveIndex = 0;

            var shipId = definition.id;
            cards.StartRun(definition, config, card => cardUnlocks.IsUnlocked(card, shipId));
            effects.ResetForRun();

            var prefab = PrefabLibrary.Active != null ? PrefabLibrary.Active.playerShip : null;
            if (prefab != null)
            {
                ship = Instantiate(prefab);
            }
            else
            {
                ship = new GameObject("PlayerShip").AddComponent<PlayerShip>();
            }
            ship.Setup(definition, world, effects, passiveUpgrades);
            ship.onDied += HandleShipDied;

            effects.AttachRun(ship, cards);

            // Level 1 comes with a pick, so no ship meets wave 1 bare.
            cards.PrepareChoices();
            if (cards.Choices.Count == 0)
            {
                StartNextWave();
                return;
            }

            startingPick = true;
            Time.timeScale = 0f;
            SetPhase(RunPhase.UpgradeSelection);
        }

        private void TearDown()
        {
            waves.Stop();
            world.ClearAll();
            world.ClearPool();
            Projectile.ResetPool();
            Impact.ClearAll();
            XpOrb.ClearAll();

            level = 1;
            xp = 0;
            xpRemainder = 0f;
            pendingPicks = 0;
            startingPick = false;
            waveClearPending = false;
            nextWaveTimer = -1f;
            bossDropsGiven = 0;
            reviveUsed = false;
            runCreditsPaid = 0;
            completedWaves = 0;
            stageNewlyCleared = false;
            score = 0;
            newRecord = false;

            if (JuiceDirector.Instance != null)
            {
                // A hit stop surviving a teardown would start the next run at
                // 5% speed.
                JuiceDirector.Instance.ClearHitStop();
                JuiceDirector.Instance.ClearNumbers();
            }

            if (ship != null)
            {
                ship.onDied -= HandleShipDied;
                Destroy(ship.gameObject);
                ship = null;
            }

            Time.timeScale = 1f;
        }

        /// <summary>Abandons the current run and returns to ship selection.</summary>
        public void Restart()
        {
            // Keep the finished stage on the result screen, then select its
            // successor before the menu refreshes. Defeat keeps the same stage.
            if (!isEndless && phase == RunPhase.Victory && shipDefinition != null && !StageProgress.readOnly)
            {
                var next = Mathf.Min(stageIndex + 1, StageCatalog.Count - 1);
                if (StageProgress.IsUnlocked(shipDefinition.id, next)) SetStage(next);
            }
            // Quitting an endless run from the pause menu still counts its points.
            if (phase == RunPhase.WaveInProgress || phase == RunPhase.UpgradeSelection) RecordEndlessScore();
            TearDown();
            effects.ResetForRun();
            SetPhase(RunPhase.ShipSelection);
        }

        // ------------------------------------------------------------------
        // Waves
        // ------------------------------------------------------------------

        private void StartNextWave()
        {
            nextWaveTimer = -1f;
            waveIndex++;

            var wave = isEndless ? StageCatalog.GetEndlessWave(config, waveIndex) : StageCatalog.GetWave(config, stageIndex, waveIndex);
            if (wave == null)
            {
                Debug.LogError("[AstroCrawler] Stage " + (stageIndex + 1) + ", wave " +
                    waveIndex + " is not defined; ending the run.");
                FinishRun(!isEndless);
                return;
            }

            bossDropsGiven = 0;
            Time.timeScale = GameSpeed.Multiplier;
            if (phase != RunPhase.WaveInProgress)
            {
                SetPhase(RunPhase.WaveInProgress);
            }
            world.healthScale = isEndless ? StageCatalog.EndlessHealthScale(waveIndex) : StageCatalog.HealthScale(stageIndex, waveIndex);
            world.stageLevel = isEndless ? Mathf.Min(StageCatalog.Count - 1, 2 + (waveIndex - 1) / StageCatalog.EndlessCycleLength)
                : stageIndex;
            waves.StartWave(wave);

            if (onWaveStarted != null)
            {
                onWaveStarted(wave);
            }
        }

        private void HandleWaveCleared()
        {
            if (phase == RunPhase.UpgradeSelection && !startingPick)
            {
                waveClearPending = true;
                return;
            }
            if (phase != RunPhase.WaveInProgress)
            {
                return;
            }
            CompleteWave();
        }

        private void CompleteWave()
        {
            completedWaves = waveIndex;
            if (isEndless)
            {
                score += EndlessScore.ForWave(waveIndex);
                PayEndlessProgress();
            }
            if (onWaveCleared != null)
            {
                onWaveCleared(waveIndex);
            }

            if (!isEndless && waveIndex >= WavesPerRun)
            {
                Time.timeScale = 1f;
                FinishRun(true);
                return;
            }

            // The next wave follows by itself. The breather lets the last orbs
            // fly home and the "wave cleared" banner be read.
            nextWaveTimer = Mathf.Max(0f, config.progression.waveBreak);
        }

        private void PayEndlessProgress()
        {
            if (completedWaves == 0) return;
            passiveUpgrades.Award(completedWaves, false, shipDefinition.id, runCreditsPaid, StageCatalog.RewardScale(4));
            runCreditsPaid += passiveUpgrades.lastReward;
        }

        /// <summary>Saves the record and sends the run to the ranking (death or quitting mid-run).</summary>
        private void RecordEndlessScore()
        {
            // Debug runs (every stage open, free credits) stay out of the record and the ranking.
            if (!isEndless || score <= 0 || DebugMode.Enabled) return;
            newRecord = EndlessScore.Record(score);
            // Every run is sent, not only records: the platform keeps the best
            // one, and a record set offline still reaches the ranking later.
            var leaderboards = Leaderboards.EnsureCreated();
            if (leaderboards != null) leaderboards.Submit(Mathf.Max(score, EndlessScore.Best));
        }

        /// <summary>
        /// Evolution guarantee: a run that cleared at least GuaranteeMinWaves
        /// waves (win or loss) always leaves the player able to buy something -
        /// the ship's next level or the next base deck card, whichever is
        /// cheaper. Only the missing credits are paid. Deaths in the first
        /// waves are left out so dying on purpose does not farm levels.
        /// </summary>
        public const int GuaranteeMinWaves = 2;

        /// <summary>Guarantees paid in a row without a campaign victory (saved on the device).</summary>
        private const string GuaranteeStreakKey = "AstroCrawler.Guarantee.v1.streak";

        /// <summary>Guarantees in a row that keep the base requirement; each one after that asks one more wave.</summary>
        public const int GuaranteeFreeStreak = 2;

        /// <summary>Credits the guarantee added to the last result (already inside lastReward).</summary>
        public int guaranteeBonus { get; private set; }

        /// <summary>
        /// Waves a run must clear to be guaranteed. Losing on purpose in the
        /// first waves would farm levels, so after GuaranteeFreeStreak paid
        /// guarantees without a victory, each new one asks one wave more; a
        /// campaign victory starts over. Simulated: honest play is unchanged,
        /// dying in wave 3 on purpose drops from ~34 to ~9 levels per hour.
        /// </summary>
        public static int GuaranteeWavesNeeded
        {
            get
            {
                var streak = Mathf.Max(0, PlayerPrefs.GetInt(GuaranteeStreakKey, 0));
                return GuaranteeMinWaves + Mathf.Max(0, streak - GuaranteeFreeStreak);
            }
        }

        private void ApplyEvolutionGuarantee(int clearedWaves, bool campaignVictory)
        {
            guaranteeBonus = 0;
            if (StageProgress.readOnly || shipDefinition == null) return;
            if (campaignVictory) PlayerPrefs.SetInt(GuaranteeStreakKey, 0);
            if (clearedWaves < GuaranteeWavesNeeded) return;
            var cheapest = CheapestEvolution(shipDefinition.id);
            if (cheapest == int.MaxValue) return;
            var missing = cheapest - passiveUpgrades.credits;
            if (missing <= 0) return;
            passiveUpgrades.AddToLastReward(missing);
            guaranteeBonus = missing;
            if (!campaignVictory) PlayerPrefs.SetInt(GuaranteeStreakKey, PlayerPrefs.GetInt(GuaranteeStreakKey, 0) + 1);
        }

        /// <summary>Price of the cheapest thing the player can still buy to get stronger; MaxValue when nothing is left.</summary>
        public int CheapestEvolution(string shipId)
        {
            var cheapest = int.MaxValue;
            if (passiveUpgrades.AvailableLevels(shipId) > 0) cheapest = passiveUpgrades.Cost(shipId, 1);
            if (cardUnlocks != null && cardUnlocks.LockedBaseCount() > 0)
                cheapest = Mathf.Min(cheapest, cardUnlocks.NextCost);
            return cheapest;
        }

        private void HandleShipDied()
        {
            // Freeze the actual wave, including its spawn coroutine, until revive or teardown.
            waves.SetSuspended(true);
            if (JuiceDirector.Instance != null) JuiceDirector.Instance.ClearHitStop();
            Time.timeScale = 0f;
            FinishRun(false);
        }

        public bool ReviveFromReward()
        {
            if (!CanRevive) return false;
            reviveUsed = true;
            ship.Revive();
            waves.SetSuspended(false);
            Time.timeScale = GameSpeed.Multiplier;
            SetPhase(RunPhase.WaveInProgress);
            return true;
        }

        private void FinishRun(bool victory)
        {
            if (phase == RunPhase.Victory || phase == RunPhase.Defeat) return;
            if (victory)
            {
                XpOrb.ClearAll();
                pendingPicks = 0;
                nextWaveTimer = -1f;
            }
            victory = victory && !isEndless;
            var cleared = victory ? WavesPerRun : completedWaves;
            passiveUpgrades.Award(cleared, victory, shipDefinition.id, runCreditsPaid,
                StageCatalog.RewardScale(isEndless ? 4 : stageIndex));
            ApplyEvolutionGuarantee(cleared, victory);
            runCreditsPaid += passiveUpgrades.lastReward;
            RecordEndlessScore();
            if (victory)
            {
                stageNewlyCleared = StageProgress.MarkCleared(shipDefinition.id, stageIndex);
                // Persist immediately in case the app closes on the result screen,
                // without changing stageIndex used by results/rewards/PlayNextLevel.
                var next = Mathf.Min(stageIndex + 1, StageCatalog.Count - 1);
                if (!StageProgress.readOnly && StageProgress.IsUnlocked(shipDefinition.id, next))
                {
                    PlayerPrefs.SetInt(SelectedStageKey, next);
                    ProgressSnapshot.SaveLocal();
                }
            }
            SetPhase(victory ? RunPhase.Victory : RunPhase.Defeat);
        }

        // ------------------------------------------------------------------
        // XP and levels
        // ------------------------------------------------------------------

        private void HandleEnemyKilled(Enemy enemy, bool byPlayerDamage)
        {
            // Scored before the XP checks: the level cap (12 in endless) stops XP, not points.
            if (isEndless && byPlayerDamage && phase == RunPhase.WaveInProgress && ship != null && ship.isAlive &&
                enemy.definition != null)
            {
                score += EndlessScore.ForKill(enemy.definition.xp, waveIndex);
            }
            // Ramming the hull pays nothing: XP is for what the build destroys.
            if (!byPlayerDamage || isMaxLevel || ship == null || !ship.isAlive || enemy.definition == null)
            {
                return;
            }
            XpOrb.Spawn(enemy.transform.position, enemy.definition.xp, config.progression, AddXp);
        }

        /// <summary>
        /// Credits collected XP. Only counts here; the pick itself opens from
        /// Update, because this is reached from inside a kill that is still
        /// being processed when the orb budget is full.
        /// </summary>
        public void AddXp(int amount)
        {
            if (amount <= 0 || ship == null || !ship.isAlive ||
                phase == RunPhase.Victory || phase == RunPhase.Defeat || phase == RunPhase.ShipSelection)
            {
                return;
            }

            // Ima Gravitacional: bonus XP, keeping the fraction for the next orb.
            var bonus = effects != null ? effects.XpBonus : 0f;
            if (bonus > 0f || isEndless)
            {
                var scaled = amount * (1f + bonus) * (isEndless ? StageCatalog.EndlessXpScale : 1f) + xpRemainder;
                amount = Mathf.FloorToInt(scaled);
                xpRemainder = scaled - amount;
            }
            xp += amount;
            while (!isMaxLevel && xp >= xpToNext)
            {
                xp -= xpToNext;
                level++;
                pendingPicks++;
                if (onLevelUp != null)
                {
                    onLevelUp(level);
                }
            }
            if (isMaxLevel)
            {
                xp = 0;
            }
        }

        /// <summary>
        /// The boss is one enemy and would otherwise pay XP only by dying, when
        /// the run is already over. Instead it sheds a full level of XP each
        /// time its health crosses an evenly spaced mark.
        /// </summary>
        private void TickBossDrops()
        {
            var boss = world.boss;
            var steps = config.progression.bossLevelDrops;
            if (boss == null || !boss.isAlive || steps <= 0 || isMaxLevel)
            {
                return;
            }

            // The whole fight's health: the two halves after the split count together.
            while (bossDropsGiven < steps &&
                   world.BossHealthFraction <= 1f - (bossDropsGiven + 1f) / (steps + 1f))
            {
                bossDropsGiven++;
                XpOrb.Burst(boss.transform.position, xpToNext, config.progression.bossDropOrbs,
                    config.progression, AddXp);
            }
        }

        private void OpenLevelPick()
        {
            cards.PrepareChoices();
            if (cards.Choices.Count == 0)
            {
                // Every upgrade in the deck is already evolved: the level
                // still counts, there is just nothing left to choose.
                pendingPicks = 0;
                return;
            }

            if (JuiceDirector.Instance != null)
            {
                // Its saved time scale must not come back over the freeze.
                JuiceDirector.Instance.ClearHitStop();
            }
            Time.timeScale = 0f;
            SetPhase(RunPhase.UpgradeSelection);
        }

        // ------------------------------------------------------------------
        // Player actions
        // ------------------------------------------------------------------

        /// <summary>
        /// Commits the upgrade for the level on screen. The starting pick
        /// launches wave 1; every other one returns to the fight it froze,
        /// unless more levels are waiting, in which case the next pick follows.
        /// </summary>
        public bool ConfirmUpgrade(RuntimeCard chosen)
        {
            if (phase != RunPhase.UpgradeSelection || ship == null || !ship.isAlive)
            {
                return false;
            }

            if (!cards.Acquire(chosen))
            {
                return false;
            }

            if (startingPick)
            {
                startingPick = false;
                effects.ResolvePermanent(chosen);
                StartNextWave();
                return true;
            }

            effects.ResolvePermanent(chosen);

            pendingPicks = Mathf.Max(0, pendingPicks - 1);
            if (pendingPicks > 0)
            {
                cards.PrepareChoices();
                if (cards.Choices.Count > 0)
                {
                    SetPhase(RunPhase.UpgradeSelection);
                    return true;
                }
                pendingPicks = 0;
            }

            Time.timeScale = GameSpeed.Multiplier;
            SetPhase(RunPhase.WaveInProgress);

            if (waveClearPending)
            {
                waveClearPending = false;
                CompleteWave();
            }
            return true;
        }

        /// <summary>
        /// Puts a new GameSpeed into effect. Only a running wave plays at
        /// speed; a frozen screen (upgrade pick, pause) picks it up when it
        /// lets go of time.
        /// </summary>
        public void ApplyGameSpeed()
        {
            if (phase != RunPhase.WaveInProgress || Time.timeScale == 0f)
            {
                return;
            }

            // A hit stop would restore the old speed when it ends.
            if (JuiceDirector.Instance != null)
            {
                JuiceDirector.Instance.ClearHitStop();
            }
            Time.timeScale = GameSpeed.Multiplier;
        }

        private void SetPhase(RunPhase next)
        {
            phase = next;
            if (onPhaseChanged != null)
            {
                onPhaseChanged(next);
            }
        }
    }
}
