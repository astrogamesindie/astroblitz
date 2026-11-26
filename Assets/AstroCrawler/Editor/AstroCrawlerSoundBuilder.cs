// AstroCrawler - builds the SoundLibrary and sets mobile import settings.
//
// The mix lives in the table below: which clip each moment uses and how loud.
// The source clips came from the SCI-FI UI SFX pack and the Shift UI kit, and
// only the ones listed here were kept (Assets/AstroCrawler/Audio); the rest of
// both packs was moved out of the project to _Archive/.
//
// Re-running this overwrites the cue list, so tune volumes here rather than in
// the inspector if you want them to survive the next run.

using UnityEditor;

using UnityEngine;

namespace AstroCrawler
{
    public static class AstroCrawlerSoundBuilder
    {
        public const string AudioFolder = "Assets/AstroCrawler/Audio";
        public const string LibraryPath = AstroCrawlerAssetBuilder.DatabaseFolder + "/SoundLibrary.asset";

        private struct Entry
        {
            public Sound sound;
            public string clip;
            public float volume;
            public string layer;
            public float layerVolume;
            public float pitchJitter;
            public float cooldown;
        }

        private static Entry E(Sound sound, string clip, float volume, float cooldown = 0f,
            float jitter = 0f, string layer = null, float layerVolume = 1f)
        {
            return new Entry
            {
                sound = sound, clip = clip, volume = volume, cooldown = cooldown,
                pitchJitter = jitter, layer = layer, layerVolume = layerVolume
            };
        }

        // Language of the mix: clicks are plain taps, rising intervals mean
        // forward or reward, falling ones back or loss, rings are credits and
        // shields, glitches are damage and errors, impacts are the big beats.
        private static readonly Entry[] Table =
        {
            E(Sound.Tap, "UI/Click_Mid-High.wav", 0.5f, 0.03f),
            E(Sound.Back, "UI/Click_Pitched_Down.wav", 0.6f, 0.03f),
            E(Sound.Hover, "UI/High_Click_1.wav", 0.16f, 0.06f),
            E(Sound.Select, "UI/Click_Scoop_Up.wav", 0.9f, 0.05f),
            E(Sound.Navigate, "UI/Click_Combo_3_High.wav", 0.4f, 0.05f),
            E(Sound.Toggle, "UI/Click_Combo_2_High.wav", 0.45f, 0.05f),
            E(Sound.Play, "UI/Start.ogg", 0.8f, 0.5f),
            E(Sound.CardsDealt, "UI/Click_Combo_4_High.wav", 0.4f, 0.2f),
            E(Sound.UpgradeChosen, "UI/Tone1B_MajorTriadUp.wav", 0.9f, 0.2f),
            E(Sound.Purchase, "UI/Ring_Pitched_Up.wav", 1f, 0.1f, 0f, "UI/Tone1B_OctaveUp.wav", 0.7f),
            E(Sound.Denied, "UI/Glitch_16.wav", 0.5f, 0.15f),
            E(Sound.PauseOpen, "UI/Tone1C_FifthDown.wav", 0.8f, 0.1f),
            E(Sound.PauseClose, "UI/Tone1C_FifthUp.wav", 0.8f, 0.1f),
            E(Sound.ConfirmPrompt, "UI/Tone1C_TritoneDown.wav", 0.6f, 0.1f),

            E(Sound.WaveStart, "Partida/Reverse_Ring_2.wav", 0.55f, 0.5f),
            E(Sound.WaveCleared, "Partida/Tone1A_MajorTriadUp.wav", 0.8f, 0.5f),
            E(Sound.BossIncoming, "Partida/Impact_2_Top_End.wav", 0.8f, 1f),
            E(Sound.Victory, "Partida/Tone1A_OctaveUp.wav", 0.9f, 1f, 0f, "Partida/Impact_2_Reso.wav", 0.6f),
            E(Sound.Defeat, "Partida/Tone1C_MinorTriadDown.wav", 0.9f, 1f, 0f, "Partida/Glitch_14.wav", 0.5f),

            E(Sound.ShipHit, "Partida/Glitch_5.wav", 0.4f, 0.12f, 0.08f),
            E(Sound.ShieldBlock, "Partida/Ring_Pitched_Down.wav", 0.7f, 0.1f, 0.04f),
            E(Sound.Heal, "Partida/Tone1A_FifthUp.wav", 0.5f, 0.3f),
            E(Sound.EnemyDown, "Partida/Glitch_13.wav", 0.22f, 0.06f, 0.12f),
            E(Sound.BossDown, "Partida/Impact_2_Mid.wav", 0.9f, 1f),
            E(Sound.MissileLaunch, "Partida/Click_Combo_5.wav", 0.4f, 0.15f, 0.05f),
            E(Sound.AreaAbility, "Partida/Reverse_Ring_2_Low.wav", 0.7f, 0.1f, 0.05f),
        };

        private const string MusicClip = "Musica/Background.ogg";

        [MenuItem("Tools/AstroCrawler/Gerar SoundLibrary", false, 44)]
        public static void BuildSoundLibrary()
        {
            ApplyAudioImportSettings();

            AstroCrawlerAssetBuilder.EnsureFolder(AstroCrawlerAssetBuilder.DatabaseFolder);
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.cues.Clear();
            var missing = 0;
            for (var i = 0; i < Table.Length; i++)
            {
                var entry = Table[i];
                var clip = Load(entry.clip);
                if (clip == null)
                {
                    missing++;
                    continue;
                }
                library.cues.Add(new SoundLibrary.Cue
                {
                    sound = entry.sound,
                    clip = clip,
                    volume = entry.volume,
                    layer = entry.layer != null ? Load(entry.layer) : null,
                    layerVolume = entry.layerVolume,
                    pitchJitter = entry.pitchJitter,
                    cooldown = entry.cooldown
                });
            }
            library.music = Load(MusicClip);
            EditorUtility.SetDirty(library);

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(AstroCrawlerAssetBuilder.DatabasePath);
            if (database != null)
            {
                database.sounds = library;
                EditorUtility.SetDirty(database);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AstroCrawler] SoundLibrary: " + library.cues.Count + " sons" +
                      (missing > 0 ? ", " + missing + " clipes nao encontrados" : "") +
                      (database != null ? ", ligada ao GameDatabase." : "; GameDatabase ausente."));
        }

        private static AudioClip Load(string relative)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + relative);
            if (clip == null)
            {
                Debug.LogWarning("[AstroCrawler] Clipe nao encontrado: " + AudioFolder + "/" + relative);
            }
            return clip;
        }

        /// <summary>
        /// Phone-friendly import: effects mono and ADPCM (cheap to decode, a
        /// third of the size), short ones decompressed up front so the first
        /// tap has no latency; the music streamed as Vorbis.
        /// </summary>
        [MenuItem("Tools/AstroCrawler/Aplicar importacao de audio (mobile)", false, 45)]
        public static void ApplyAudioImportSettings()
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                {
                    continue;
                }

                var isMusic = path.Contains("/Musica/");
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var settings = importer.defaultSampleSettings;

                if (isMusic)
                {
                    importer.forceToMono = false;
                    settings.loadType = AudioClipLoadType.Streaming;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = 0.5f;
                }
                else
                {
                    importer.forceToMono = true;
                    settings.loadType = clip != null && clip.length > 1.5f
                        ? AudioClipLoadType.CompressedInMemory
                        : AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = AudioCompressionFormat.ADPCM;
                }
                settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
                settings.preloadAudioData = !isMusic;

                importer.defaultSampleSettings = settings;
                importer.loadInBackground = isMusic;
                importer.SaveAndReimport();
            }

            // The two large decorative textures are drawn small; full size
            // would cost several MB of texture memory on a phone for a glow.
            LimitTexture(AstroCrawlerAssetBuilder.ResourcesFolder + "/UI/Lens Flare.png", 512);
            LimitTexture(AstroCrawlerAssetBuilder.ResourcesFolder + "/UI/Background Glow.png", 256);
        }

        private static void LimitTexture(string path, int size)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.maxTextureSize == size)
            {
                return;
            }
            importer.maxTextureSize = size;
            importer.SaveAndReimport();
        }
    }
}
