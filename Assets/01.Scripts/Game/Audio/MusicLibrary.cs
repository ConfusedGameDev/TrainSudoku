using System;
using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace TrainSudoku.Game
{
    /// <summary>Maps each <see cref="MusicTrack"/> to a clip and its own mixer group. Tracks without a clip are silent.</summary>
    /// <remarks>
    /// The slots live here, on an asset, and not on the generated <c>Music</c> scene object, because
    /// <see cref="GameManager.Generate"/> destroys and rebuilds its children.
    /// </remarks>
    [CreateAssetMenu(menuName = "TrainSudoku/Music Library", fileName = "Music")]
    public sealed class MusicLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public MusicTrack track;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;

            [Tooltip("This track's own mixer group. Empty routes to the mixer's default group.")]
            public AudioMixerGroup output;

            public bool loop;
        }

        [Tooltip("One row per track: Main Menu, Map, and the three in-game variations. A row with no clip is silent.")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        [Tooltip("Seconds to cross from one track to the next. Every change of track fades both ways.")]
        [Range(0.1f, 6f)] [SerializeField] private float crossfadeSeconds = 1.5f;

        [Tooltip("Seconds to fade back up when the game resumes from Pause.")]
        [Range(0f, 2f)] [SerializeField] private float resumeFadeSeconds = 0.3f;

        public float CrossfadeSeconds => crossfadeSeconds <= 0f ? 1.5f : crossfadeSeconds;
        public float ResumeFadeSeconds => resumeFadeSeconds;

        public bool TryGet(MusicTrack track, out Entry entry)
        {
            if (entries != null && track != MusicTrack.None)
            {
                foreach (var candidate in entries)
                {
                    if (candidate.track != track || candidate.clip == null) continue;
                    entry = candidate;
                    // The same rule AudioCueLibrary uses: an unset volume means full, not silent.
                    if (entry.volume <= 0f) entry.volume = 1f;
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
