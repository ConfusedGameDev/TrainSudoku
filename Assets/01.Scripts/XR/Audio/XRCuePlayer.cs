using TrainSudoku.XR.Rules;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// XR's sound (XR-PRD 9): every cue placed in the room where it happened (a piece cue from the piece, a board cue
    /// from the board, a UI cue from the sign or wrist pressed), at the effects volume the wrist menu sets, with the
    /// cue's buzz in the hand that caused it. A pool of voices, so cues overlap, and one looping voice for the train.
    /// </summary>
    /// <remarks>
    /// A tabletop game is heard from half a metre, so voices are full volume within 0.3 m and fall off to nothing by 6 m.
    /// The phone's audio layer is the phone's (X20): nothing here touches its cue player, library or mixer.
    /// </remarks>
    public sealed class XRCuePlayer : MonoBehaviour
    {
        private const int Voices = 8;
        private const float NearDistance = 0.3f;
        private const float FarDistance = 6f;
        private const float LoopFadeSeconds = 0.25f;

        private static XRCuePlayer _instance;

        private XRCueLibrary _library;
        private XRPreferences _preferences;
        private AudioSource[] _voices;
        private int _next;
        private AudioSource _loop;
        private Transform _loopFollows;
        private float _loopVolume;
        private float _loopTarget;
        private bool _loopPaused;

        public static XRCuePlayer Create(XRCueLibrary library, XRPreferences preferences)
        {
            var go = new GameObject("Cue Player");
            var player = go.AddComponent<XRCuePlayer>();
            player._library = library;
            player._preferences = preferences;
            player._voices = new AudioSource[Voices];
            for (var i = 0; i < Voices; i++) player._voices[i] = player.Voice($"Voice {i}");
            player._loop = player.Voice("Loop");
            player._loop.loop = true;
            _instance = player;
            XRHaptics.TakeOver();
            return player;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>The effects volume, 0 to 1, from the wrist menu's 0 to 10.</summary>
        private float Effects => _preferences != null ? (float)_preferences.EffectsLevel : 1f;

        /// <summary>Plays <paramref name="cue"/> at <paramref name="at"/>, and buzzes <paramref name="hand"/> if the cue is felt.</summary>
        public static void Play(XRCue cue, Vector3 at, Hand? hand = null)
        {
            if (_instance == null || cue == XRCue.None) return;
            _instance.PlayAt(cue, at, hand);
        }

        /// <summary>Starts <paramref name="cue"/> looping on <paramref name="follow"/> (the train), fading in.</summary>
        public static void StartLoop(XRCue cue, Transform follow)
        {
            if (_instance == null) return;
            _instance.BeginLoop(cue, follow);
        }

        /// <summary>Fades the loop out.</summary>
        public static void StopLoop()
        {
            if (_instance != null) _instance._loopTarget = 0f;
        }

        /// <summary>The pause holds the loop where it is; it never plays on through a paused game (6.3).</summary>
        public static void PauseLoop(bool paused)
        {
            if (_instance == null || _instance._loop == null || paused == _instance._loopPaused) return;
            _instance._loopPaused = paused;
            if (paused) _instance._loop.Pause();
            else _instance._loop.UnPause();
        }

        private void PlayAt(XRCue cue, Vector3 at, Hand? hand)
        {
            var entry = _library != null ? _library.Find(cue) : null;
            if (entry == null) return;
            if (hand.HasValue && XRCueMap.IsFelt(cue)) XRHaptics.Play(hand.Value, entry.hapticAmplitude, entry.hapticSeconds);
            if (entry.clip == null) return;

            var voice = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            voice.transform.position = at;
            voice.spatialBlend = entry.spatial ? 1f : 0f;
            voice.Stop();
            voice.clip = entry.clip;
            voice.volume = entry.volume * Effects;
            voice.Play();
        }

        private void BeginLoop(XRCue cue, Transform follow)
        {
            var entry = _library != null ? _library.Find(cue) : null;
            _loopFollows = follow;
            _loopPaused = false;
            if (entry == null || entry.clip == null)
            {
                _loop.Stop();
                return;
            }

            if (_loop.clip != entry.clip || !_loop.isPlaying)
            {
                _loop.clip = entry.clip;
                _loop.spatialBlend = entry.spatial ? 1f : 0f;
                _loopVolume = 0f;
                _loop.volume = 0f;
                _loop.Play();
            }

            _loopTarget = entry.volume;
        }

        private void Update()
        {
            if (_loop == null || !_loop.isPlaying || _loopPaused) return;
            if (_loopFollows != null) _loop.transform.position = _loopFollows.position;
            _loopVolume = Mathf.MoveTowards(_loopVolume, _loopTarget, Time.unscaledDeltaTime / LoopFadeSeconds);
            _loop.volume = _loopVolume * Effects;
            if (_loopTarget <= 0f && _loopVolume <= 0f) _loop.Stop();
        }

        private AudioSource Voice(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = NearDistance;
            source.maxDistance = FarDistance;
            source.dopplerLevel = 0f;
            return source;
        }
    }
}
