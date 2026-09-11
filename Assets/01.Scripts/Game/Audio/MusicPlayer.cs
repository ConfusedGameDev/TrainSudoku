using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Plays the music, one <see cref="AudioSource"/> per track so each keeps its own mixer group and its own playback
    /// position. Every change of track is a crossfade; a pause is a pause, not a fade to silence.
    /// </summary>
    /// <remarks>
    /// Fades run on <b>unscaled</b> time, like every other animation in the game (<c>Motion</c>, <c>PieceView</c>,
    /// <c>PopScale</c>). Nothing in this project touches <c>Time.timeScale</c>, but a fade that silently depended on it
    /// is the bug that is invisible until the day something does.
    /// </remarks>
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private MusicLibrary library;

        private readonly Dictionary<MusicTrack, AudioSource> _sources = new Dictionary<MusicTrack, AudioSource>();
        private readonly List<AudioSource> _pausedByUs = new List<AudioSource>();

        private MusicTrack _target = MusicTrack.None;
        private MusicTrack _from = MusicTrack.None;
        private float _fadeSeconds;
        private float _fadeElapsed;
        private bool _fading;
        private bool _paused;

        /// <summary>Rides 0 to 1 when the game resumes, multiplying whatever the crossfade is doing rather than fighting it.</summary>
        private float _resumeGain = 1f;

        /// <summary>The track being played or faded towards.</summary>
        public MusicTrack Current => _target;

        public static MusicPlayer Create(Transform parent, MusicLibrary library)
        {
            var go = new GameObject("Music");
            go.transform.SetParent(parent, false);
            var player = go.AddComponent<MusicPlayer>();
            player.library = library;
            return player;
        }

        /// <summary>Re-reads the library at launch, so filling a clip or a group in later never needs the scene touched.</summary>
        public void Configure(MusicLibrary music)
        {
            if (music != null) library = music;
            foreach (var pair in _sources)
            {
                if (pair.Value == null || library == null) continue;
                if (!library.TryGet(pair.Key, out var entry)) continue;
                pair.Value.clip = entry.clip;
                pair.Value.loop = entry.loop;
                pair.Value.outputAudioMixerGroup = entry.output;
            }
        }

        /// <summary>
        /// Crossfades to a track. Asking for the track already playing — or already being faded towards — does nothing,
        /// which is what keeps the map track unbroken as the player crosses between the network and a line's map.
        /// </summary>
        public void Play(MusicTrack track)
        {
            if (track == _target) return;

            _from = _target;
            _target = track;
            _fadeSeconds = library != null ? library.CrossfadeSeconds : 1.5f;
            _fadeElapsed = 0f;
            _fading = true;

            var incoming = SourceFor(track);
            if (incoming != null && !incoming.isPlaying)
            {
                incoming.volume = 0f;
                incoming.Play();
            }
        }

        /// <summary>
        /// Pauses every playing source, keeping its position, and un-pauses exactly what it paused. It has to remember
        /// which: a crossfade can be caught mid-flight by the pause, and both sides of it have to come back.
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (paused == _paused) return;
            _paused = paused;

            if (paused)
            {
                _pausedByUs.Clear();
                foreach (var source in _sources.Values)
                {
                    if (source == null || !source.isPlaying) continue;
                    source.Pause();
                    _pausedByUs.Add(source);
                }

                return;
            }

            foreach (var source in _pausedByUs)
                if (source != null) source.UnPause();
            _pausedByUs.Clear();

            var fade = library != null ? library.ResumeFadeSeconds : 0.3f;
            _resumeGain = fade > 0f ? 0f : 1f;
        }

        private AudioSource SourceFor(MusicTrack track)
        {
            if (track == MusicTrack.None || library == null) return null;
            if (_sources.TryGetValue(track, out var existing) && existing != null) return existing;
            if (!library.TryGet(track, out var entry)) return null;

            var go = new GameObject($"Music {track}");
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = entry.loop;
            source.clip = entry.clip;
            source.outputAudioMixerGroup = entry.output;
            source.volume = 0f;
            _sources[track] = source;
            return source;
        }

        private float VolumeOf(MusicTrack track) =>
            library != null && library.TryGet(track, out var entry) ? entry.volume : 1f;

        private void Update()
        {
            // A fade caught by the pause holds where it was and carries on from there when the game resumes.
            if (_paused) return;

            if (_resumeGain < 1f)
            {
                var fade = library != null ? library.ResumeFadeSeconds : 0.3f;
                _resumeGain = fade <= 0f ? 1f : Mathf.Clamp01(_resumeGain + Time.unscaledDeltaTime / fade);
                if (!_fading) ApplySettledVolumes();
            }

            if (!_fading) return;

            _fadeElapsed += Time.unscaledDeltaTime;
            var t = _fadeSeconds <= 0f ? 1f : Mathf.Clamp01(_fadeElapsed / _fadeSeconds);

            // Equal power, so the crossfade does not dip in the middle the way a linear pair does.
            var rising = Mathf.Sqrt(t);
            var falling = Mathf.Sqrt(1f - t);

            var outgoing = _from != MusicTrack.None && _sources.TryGetValue(_from, out var o) ? o : null;
            if (outgoing != null) outgoing.volume = VolumeOf(_from) * falling * _resumeGain;

            var incoming = _target != MusicTrack.None && _sources.TryGetValue(_target, out var i) ? i : null;
            if (incoming != null) incoming.volume = VolumeOf(_target) * rising * _resumeGain;

            if (t < 1f) return;

            _fading = false;
            _from = MusicTrack.None;

            // Anything that is not the target stops holding a voice.
            foreach (var pair in _sources)
            {
                if (pair.Key == _target || pair.Value == null) continue;
                pair.Value.Stop();
                pair.Value.volume = 0f;
            }

            ApplySettledVolumes();
        }

        /// <summary>Holds the target at its authored level once no crossfade is running, so the resume ramp still lands.</summary>
        private void ApplySettledVolumes()
        {
            if (_target == MusicTrack.None) return;
            if (_sources.TryGetValue(_target, out var source) && source != null)
                source.volume = VolumeOf(_target) * _resumeGain;
        }
    }
}
