// AstroCrawler - roguelike arena shooter with a card-driven ability system.
// Data taxonomy. Kept as a single file so the whole vocabulary of the game is
// readable in one place (the JSON configuration under Resources/ uses these names).

namespace AstroCrawler
{
    /// <summary>
    /// Which deck a card belongs to. Common cards exist in every ship's deck;
    /// the other categories are the per-ship exclusive cards.
    /// </summary>
    public enum CardCategory
    {
        Common,
        Vanguard,
        Bastion,
        Spectre,
        Aurora,
        Orion,
        Noel
    }

    /// <summary>
    /// The concrete behaviour a card triggers when played. Each value maps to a
    /// branch in <see cref="EffectRunner"/>.
    /// </summary>
    public enum CardEffectId
    {
        // Common (GDD 3)
        DoubleAttack,
        Shield,
        Missiles,
        BlackHole,
        PiercingShot,

        // Vanguarda-7 (GDD 3.1.1)
        EngineOverload,
        ImpactCharge, // Serialized legacy ID: now Salva Restauradora (missiles on healing).
        WildVolley,
        BattleFury,

        // Bastiao Aegis (GDD 3.1.2)
        RepulsionField,
        EmergencyRepair,
        DebrisWall,
        CoreOverload,

        // Espectro Nulo (GDD 3.1.3)
        DualGravityWell,
        TemporalDistortion,
        ChainDetonation,
        SupportDrones,
        // Append new effects to preserve the numeric IDs serialized in assets.
        TemporalParalysis,
        SpecialCooldown,
        ArmorPlating,
        BossHunter,
        GuaranteedCritical,
        ChainLightning,
        SiegeRound,

        // Base deck (shared by every ship, unlocked by purchase).
        OrbitalBlades,
        Shrapnel,
        DiagonalShot,
        PlasmaAura,
        SolarNova,
        OrbitalStrike,
        GravityMagnet,
        NanoRepair,
        PhoenixCore,

        // Exclusive cards unlocked by the ship's upgrade level.
        KillStreak,        // Vanguarda-7
        Execution,
        OverdriveFury,
        Thorns,            // Bastiao Aegis
        SpecialDuration,
        ShieldMend,
        TemporalRift,      // Espectro Nulo
        ParadoxRecharge,
        TemporalEcho,
        Overheal,          // Aurora Medica
        VitalPulse,
        EmergencyProtocol,
        Railgun,           // Orion
        TargetMark,
        AmplifyShockwave,
        BouncingSnowball,
        GiantSnowball,
        FrostCircle,
        GiftBombs,
        IceShards,
        Snowstorm
    }

    /// <summary>
    /// Enemy archetypes (GDD 5).
    /// </summary>
    public enum EnemyKind
    {
        MeteorSmall,
        MeteorMedium,
        MeteorLarge,
        Scout,
        Interceptor,
        Frigate,
        AberrantCore,
        // Appended: the boss's spawn pods and its energy orbs. New values go at
        // the end so saved assets keep their numbers.
        Cocoon,
        BossOrb
    }

    /// <summary>
    /// How an enemy travels towards the player ship.
    /// </summary>
    public enum MoveBehaviour
    {
        /// <summary>Straight line towards the ship's position at spawn time.</summary>
        Straight,
        /// <summary>Straight line with a slow sinusoidal route variation.</summary>
        Drift,
        /// <summary>Fast, homes continuously on the ship.</summary>
        Dive,
        /// <summary>Homes on the ship while weaving sideways.</summary>
        Zigzag,
        /// <summary>Slow, relentless homing. Used by elites.</summary>
        Siege,
        /// <summary>Boss: parks at its station, sways, fires, lays cocoons and splits in two at half health.</summary>
        Core,
        /// <summary>Thrown by the boss to a point, stays there and hatches ships unless destroyed.</summary>
        Anchor
    }

    /// <summary>
    /// Top level state of a run.
    /// </summary>
    public enum RunPhase
    {
        ShipSelection,
        UpgradeSelection,
        WaveInProgress,
        Victory,
        Defeat
    }

    /// <summary>
    /// Which kind of run is selected. Squadron flies the free default ship and
    /// trades every third level pick for an allied ship on a corner.
    /// </summary>
    public enum RunMode
    {
        Campaign,
        Endless,
        Squadron
    }
}
