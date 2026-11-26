// AstroCrawler - plays the interface and combat sounds, and the menu music.
//
// A fixed set of voices is reused round-robin instead of an AudioSource per
// sound: a busy wave fires a kill, a missile volley and a hit in the same
// frame, and allocating for each would show up on a phone. Audio ignores
// Time.timeScale, which is what the pause and the upgrade screen need - both
// freeze the game and both still click.

using System.Collections.Generic;

using UnityEngine;

namespace AstroCrawler
{
    /// <summary>Every moment in the game that makes a sound.</summary>
    public enum Sound
    {
        None,

        // Interface
        Tap,
        Back,
        Hover,
        Select,
        Navigate,
        Toggle,
        Play,
        CardsDealt,
        UpgradeChosen,
        Purchase,
        Denied,
        PauseOpen,
        PauseClose,
        ConfirmPrompt,

        // Run
        WaveStart,
        WaveCleared,
        BossIncoming,
        Victory,
        Defeat,

        // Combat
        ShipHit,
        ShieldBlock,
        Heal,
        EnemyDown,
        BossDown,
        MissileLaunch,
        AreaAbility
    }

    /// <summary>Call site shorthand: <c>Sfx.Play(Sound.Tap)</c>, safe before audio exists.</summary>
    public static class Sfx
    {
        public static void Play(Sound sound)
        {
            var director = SoundDirector.Instance;
            if (director != null)
            {
                director.Play(sound);
            }
        }
    }

    public class SoundDirector : MonoBehaviour
    {
        public static SoundDirector Instance { get; private set; }

        private const int Voices = 12;
        private const float MusicFade = 1.2f;

        private const string SfxKey = "AstroCrawler.Audio.Sfx";
        private const string MusicKey = "AstroCrawler.Audio.Music";
        private const string MasterKey = "AstroCrawler.Audio.Master";

        [SerializeField] private SoundLibrary library;

        private readonly Dictionary<Sound, SoundLibrary.Cue> cues = new Dictionary<Sound, SoundLibrary.Cue>();
        private readonly Dictionary<Sound, float> lastPlayed = new Dictionary<Sound, float>();
        // NonSerialized: the editor serializes private fields across a script
        // reload, and an array restored as empty (or a source restored as a
        // fake null) would skip the creation below and throw on first use.
        [System.NonSerialized] private AudioSource[] voices;
        [System.NonSerialized] private AudioSource music;
        private int nextVoice;
        private bool inRun;

        public bool sfxEnabled { get; private set; }
        public bool musicEnabled { get; private set; }

        public float masterVolume
        {
            get { return AudioListener.volume; }
        }

        private void Awake()
        {
            Instance = this;
        }

        public void Initialize(SoundLibrary source)
        {
            if (source != null)
            {
                library = source;
            }

            cues.Clear();
            lastPlayed.Clear();
            if (library != null)
            {
                for (var i = 0; i < library.cues.Count; i++)
                {
                    var cue = library.cues[i];
                    if (cue != null && cue.clip != null)
                    {
                        cues[cue.sound] = cue;
                    }
                }
            }

            if (voices == null || voices.Length != Voices || voices[0] == null)
            {
                voices = new AudioSource[Voices];
                for (var i = 0; i < voices.Length; i++)
                {
                    voices[i] = NewSource(false);
                }
            }
            if (music == null)
            {
                music = NewSource(true);
            }

            sfxEnabled = PlayerPrefs.GetInt(SfxKey, 1) == 1;
            musicEnabled = PlayerPrefs.GetInt(MusicKey, 1) == 1;
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));

            music.clip = library != null ? library.music : null;
            music.volume = 0f;
            if (musicEnabled && music.clip != null)
            {
                music.Play();
            }
        }

        private AudioSource NewSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        public void Play(Sound sound)
        {
            if (!sfxEnabled || sound == Sound.None || voices == null)
            {
                return;
            }

            SoundLibrary.Cue cue;
            if (!cues.TryGetValue(sound, out cue))
            {
                return;
            }

            var now = Time.unscaledTime;
            float last;
            if (cue.cooldown > 0f && lastPlayed.TryGetValue(sound, out last) && now - last < cue.cooldown)
            {
                return;
            }
            lastPlayed[sound] = now;

            var pitch = cue.pitchJitter > 0f ? 1f + Random.Range(-cue.pitchJitter, cue.pitchJitter) : 1f;
            PlayClip(cue.clip, cue.volume, pitch);
            if (cue.layer != null)
            {
                PlayClip(cue.layer, cue.layerVolume, pitch);
            }
        }

        private void PlayClip(AudioClip clip, float volume, float pitch)
        {
            // Round-robin: when all voices are busy the oldest one is cut, which
            // on UI sounds is never the one the player is listening to.
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.Stop();
            voice.clip = clip;
            voice.volume = volume;
            voice.pitch = pitch;
            voice.Play();
        }

        /// <summary>Ducks the music under combat and brings it back in the menus.</summary>
        public void SetInRun(bool running)
        {
            inRun = running;
        }

        public void SetSfxEnabled(bool enabled)
        {
            sfxEnabled = enabled;
            PlayerPrefs.SetInt(SfxKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetMusicEnabled(bool enabled)
        {
            musicEnabled = enabled;
            PlayerPrefs.SetInt(MusicKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            if (music == null || music.clip == null)
            {
                return;
            }
            if (enabled && !music.isPlaying)
            {
                music.volume = 0f;
                music.Play();
            }
        }

        public void SetMasterVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MasterKey, AudioListener.volume);
        }

        /// <summary>Persists the slider once the drag ends, not on every frame of it.</summary>
        public void SaveSettings()
        {
            PlayerPrefs.Save();
        }

        private void Update()
        {
            if (music == null || music.clip == null)
            {
                return;
            }

            var target = !musicEnabled || library == null ? 0f
                : inRun ? library.musicInRunVolume : library.musicVolume;
            music.volume = Mathf.MoveTowards(music.volume, target,
                Time.unscaledDeltaTime / MusicFade * Mathf.Max(0.05f, library != null ? library.musicVolume : 1f));

            if (!musicEnabled && music.isPlaying && music.volume <= 0.001f)
            {
                music.Stop();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
