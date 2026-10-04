using System;
using System.Collections.Generic;
using TrainSudoku.XR.Rules;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// What each <see cref="XRCue"/> sounds and feels like (XR-PRD 9): a clip, its volume, whether it is placed in the
    /// room, and the buzz a controller gives the hand that caused it. Every slot a person fills is here, on
    /// <c>03.Data/XR/Audio/XRCues.asset</c>, never on a scene object. An empty clip is a supported state: the cue is
    /// silent, and its haptic still plays.
    /// </summary>
    [CreateAssetMenu(menuName = "TrainSudoku/XR/Cue Library", fileName = "XRCues")]
    public sealed class XRCueLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public XRCue cue;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;

            [Tooltip("Heard from where it happened. Off only for a cue that has no place, which XR has none of yet.")]
            public bool spatial = true;

            [Tooltip("Controller buzz for the hand that caused it, 0 for none. Bare hands feel nothing, so nothing may rely on it.")]
            [Range(0f, 1f)] public float hapticAmplitude;

            [Min(0f)] public float hapticSeconds;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<XRCue, Entry> _byCue;

        public IReadOnlyList<Entry> Entries => entries;

        public Entry Find(XRCue cue)
        {
            if (_byCue == null)
            {
                _byCue = new Dictionary<XRCue, Entry>();
                foreach (var entry in entries)
                    if (entry != null && !_byCue.ContainsKey(entry.cue)) _byCue[entry.cue] = entry;
            }

            return _byCue.TryGetValue(cue, out var found) ? found : null;
        }

        /// <summary>The Editor's setup writes the slots; a slot already filled keeps its clip.</summary>
        public void SetEntries(List<Entry> value)
        {
            entries = value;
            _byCue = null;
        }

        private void OnValidate() => _byCue = null;
    }
}
