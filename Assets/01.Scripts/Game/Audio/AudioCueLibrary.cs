using System;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>Maps each <see cref="AudioCue"/> to a clip. Cues without a clip are silent.</summary>
    [CreateAssetMenu(menuName = "TrainSudoku/Audio Cue Library", fileName = "AudioCueLibrary")]
    public sealed class AudioCueLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public AudioCue cue;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public bool TryGetClip(AudioCue cue, out AudioClip clip, out float volume)
        {
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry.cue != cue || entry.clip == null) continue;
                    clip = entry.clip;
                    volume = entry.volume <= 0f ? 1f : entry.volume;
                    return true;
                }
            }

            clip = null;
            volume = 0f;
            return false;
        }
    }
}
