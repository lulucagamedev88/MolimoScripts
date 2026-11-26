using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// Pool de AudioSources pré-criados. Zero alocações em runtime.
    /// Suporta Preload (clip em RAM), Warmup (aloca vozes para evitar hitch
    /// no primeiro Play de cada som) e Play com round-robin + steal.
    /// </summary>
    [DisallowMultipleComponent]
    public class SFXPool : MonoBehaviour
    {
        [Header("size of the Pool")]
        [Tooltip("in the. total of AudioSources pre-created in the Initialize. " +
                 "values typical: 12-16 is the Player, 8-12 is enemies.")]
        [SerializeField] private int poolSize = 16;

        [Header("configuration default")]
        [Range(0f, 1f)]
        [Tooltip("0 = 2D, 1 = 3D. can be override in Play().")]
        [SerializeField] private float spatialBlend = 1f;

        [Tooltip("Linear prevents cost of curves in muitos sounds simultaneous.")]
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;

        private readonly List<AudioSource> _sources = new List<AudioSource>();
        private int _stealIndex;
        private bool _initialized;

        private void Awake() => Initialize();

        // ============================================================
        // Factory — creates in the container if still not existir
        // ============================================================
        /// <summary>
        /// Returns or create.
        /// </summary>
        public static SFXPool GetOrCreate(Transform container)
        {
            if (container == null) return null;
            SFXPool pool = container.GetComponent<SFXPool>();
            if (pool == null) pool = container.gameObject.AddComponent<SFXPool>();
            pool.Initialize();
            return pool;
        }

        /// <summary>
        /// Performs the initialize operation.
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // pre-creates TUDO of once → zero allocation during gameplay
            for (int i = 0; i < poolSize; i++)
                CreateSource();
        }

        private AudioSource CreateSource()
        {
            var go = new GameObject($"PooledSFX_{_sources.Count}");
            go.transform.SetParent(transform, false);

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = spatialBlend;
            src.rolloffMode = rolloffMode;
            src.dopplerLevel = 0f;

            _sources.Add(src);
            return src;
        }

        private AudioSource GetFreeSource()
        {
            for (int i = 0; i < _sources.Count; i++)
                if (!_sources[i].isPlaying) return _sources[i];

            // Pool full → rouba the more old
            AudioSource stolen = _sources[_stealIndex];
            _stealIndex = (_stealIndex + 1) % _sources.Count;
            stolen.Stop();
            return stolen;
        }

        // ============================================================
        // Play
        // ============================================================
        /// <summary>
        /// Performs the play operation.
        /// </summary>
        public void Play(SoundData data, Vector3 position)
            => Play(data, position, -1f);

        /// <param name="spatialBlendOverride">-1 = uses the value defined in the SoundData.</param>
        /// <summary>
        /// Performs the play operation.
        /// </summary>
        public void Play(SoundData data, Vector3 position, float spatialBlendOverride)
        {
            if (data == null) return;
            AudioClip clip = data.GetClip();
            if (clip == null) return;

            Initialize();

            AudioSource src = GetFreeSource();
            src.transform.position = position;
            src.clip = clip;
            src.volume = data.volume;
            src.pitch = data.GetPitch();
            src.loop = data.loop;
            src.outputAudioMixerGroup = data.mixerGroup;
            src.priority = data.priority;
            src.spatialBlend = spatialBlendOverride >= 0f ? spatialBlendOverride : data.spatialBlend;
            src.minDistance = data.minDistance > 0f ? data.minDistance : 1f;
            src.maxDistance = data.maxDistance > 0f ? data.maxDistance : 50f;
            src.Play(); // Play() (not PlayOneShot) → smaller latency
        }

        // ============================================================
        // Warmup — aloca voices once is eliminar hitch of the 1.º Play
        // ============================================================
        /// <summary>
        /// Toca cada clip mudo (volume 0) por 1 tick e para, alocando a voz
        /// interna do AudioSource. Deve ser chamado em Start (depois de Preload).
        /// Aceita vários arrays numa só chamada: Warmup(a, b, c).
        /// </summary>
        public void Warmup(params SoundData[][] groups)
        {
            if (groups == null || groups.Length == 0) return;
            Initialize();
            if (_sources.Count == 0) return;

            // uses the last source is minimizar conflito with any Play initial
            AudioSource src = _sources[_sources.Count - 1];

            float prevVolume = src.volume;
            float prevSpatial = src.spatialBlend;
            bool prevLoop = src.loop;
            AudioClip prevClip = src.clip;

            src.volume = 0f;
            src.spatialBlend = 0f;
            src.loop = false;

            foreach (var group in groups)
            {
                if (group == null) continue;
                foreach (var s in group)
                {
                    if (s == null) continue;
                    AudioClip clip = s.GetClip();
                    if (clip == null) continue;

                    src.clip = clip;
                    src.Play();
                    src.Stop();
                }
            }

            src.clip = prevClip;
            src.volume = prevVolume;
            src.spatialBlend = prevSpatial;
            src.loop = prevLoop;
        }

        // ============================================================
        // Preload — carrega clips is RAM
        // ============================================================
        /// <summary>Carrega the único array of SoundData for memória.</summary>
        /// <summary>
        /// Performs the preload operation.
        /// </summary>
        public static void Preload(SoundData[] sounds)
        {
            if (sounds == null) return;
            for (int i = 0; i < sounds.Length; i++)
            {
                if (sounds[i] == null) continue;
                AudioClip clip = sounds[i].GetClip();
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                    clip.LoadAudioData();
            }
        }

        /// <summary>Carrega vários arrays of the only time: Preload(the, b, c).</summary>
        /// <summary>
        /// Performs the preload operation.
        /// </summary>
        public static void Preload(params SoundData[][] groups)
        {
            if (groups == null) return;
            foreach (var g in groups) Preload(g);
        }
    }
}
