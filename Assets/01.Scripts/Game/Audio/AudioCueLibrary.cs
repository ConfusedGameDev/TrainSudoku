using System;
using UnityEngine;
using UnityEngine.Audio;

namespace TrainSudoku.Game
{
    /// <summary>Maps each <see cref="AudioCue"/> to a clip. Cues without a clip are silent.</summary>
    /// <remarks>
    /// Every slot a human fills lives on this asset rather than on the generated <c>Audio</c> scene object, because
    /// <see cref="GameManager.Generate"/> destroys and rebuilds its children — a group dragged onto the scene object
    /// would be lost on the next re-bake.
    /// </remarks>
    [CreateAssetMenu(menuName = "TrainSudoku/Audio Cue Library", fileName = "AudioCueLibrary")]
    public sealed class AudioCueLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public AudioCue cue;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;

            [Tooltip("Optional: routes this one cue somewhere other than the library's Output — the board on its own " +
                     "fader, say. Empty uses Output.")]
            public AudioMixerGroup output;

            [Tooltip("Keeps this cue playing through a screen change. Clear (the default) means a transition fades it " +
                     "out, which is what a click or a board tick wants. Tick it on the fanfares.")]
            public bool holdThroughTransition;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        [Header("Mixer")]
        [Tooltip("The bus every cue plays through unless its own entry overrides it. Empty routes to the mixer's " +
                 "default group, which is correct behaviour, not a fault.")]
        [SerializeField] private AudioMixerGroup output = null;

        [Tooltip("The bus for looping cues (the train, the erase hold). Empty falls back to Output.")]
        [SerializeField] private AudioMixerGroup loopOutput = null;

        [Tooltip("The exposed mixer parameter the mute toggle cuts. Must match the name in the Audio Mixer window's " +
                 "Exposed Parameters list; when it does not, mute falls back to a global volume.")]
        [SerializeField] private string masterVolumeParameter = "MasterVolume";

        public AudioMixerGroup Output => output;
        public AudioMixerGroup LoopOutput => loopOutput != null ? loopOutput : output;
        public string MasterVolumeParameter => masterVolumeParameter;

        /// <summary>The mixer itself, read off whichever group was dragged in — so there is no separate slot to fill.</summary>
        public AudioMixer Mixer
        {
            get
            {
                if (output != null) return output.audioMixer;
                return loopOutput != null ? loopOutput.audioMixer : null;
            }
        }

        public bool TryGetClip(AudioCue cue, out AudioClip clip, out float volume)
        {
            if (TryGet(cue, out var entry))
            {
                clip = entry.clip;
                volume = entry.volume <= 0f ? 1f : entry.volume;
                return true;
            }

            clip = null;
            volume = 0f;
            return false;
        }

        /// <summary>The whole entry, for the caller that needs its routing or its transition policy.</summary>
        public bool TryGet(AudioCue cue, out Entry entry)
        {
            if (entries != null)
            {
                foreach (var candidate in entries)
                {
                    if (candidate.cue != cue || candidate.clip == null) continue;
                    entry = candidate;
                    return true;
                }
            }

            entry = default;
            return false;
        }

        /// <summary>Where a cue plays: its own override, else the library's bus. Null is legal and means the default group.</summary>
        public AudioMixerGroup OutputFor(AudioCue cue, bool looping)
        {
            if (TryGet(cue, out var entry) && entry.output != null) return entry.output;
            return looping ? LoopOutput : output;
        }

        /// <summary>True when a screen change should leave this cue alone. Unwired cues are cancelled, which is the safe default.</summary>
        public bool HoldsThroughTransition(AudioCue cue) => TryGet(cue, out var entry) && entry.holdThroughTransition;
    }
}
