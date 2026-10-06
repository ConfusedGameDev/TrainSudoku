using TrainSudoku.Core;
using TrainSudoku.XR.Rules;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// XR's music (XR-PRD 9): a background loop for the placement and the maps, another while a station is played, and
    /// the line's own jingle from the moment a station is solved, over the train's run into the platform.
    /// <see cref="XRMusicPlan"/> decides what plays; this fades it.
    /// </summary>
    /// <remarks>
    /// Brought over from <c>feat/VisionOS</c> (<c>VisionOSMusic</c>, checked on the Vision Pro on 2026-10-04) on
    /// 2026-10-06. The phone's <c>MusicPlayer</c> is the phone's (X20): nothing here touches it, its library or its mixer.
    ///
    /// <b>The clips</b> are written by <c>XRMusicComposer</c> into a <c>Resources</c> folder and loaded by name, so
    /// there is no slot to wire and no scene step. A missing clip is silence, never an error.
    ///
    /// <b>Volumes are the wrist menu's.</b> The loops follow MUSIC, so MUSIC at 0 is the off switch; the jingle
    /// follows EFFECTS, so switching the background music off keeps the arrival. Music is not placed in the room: it
    /// plays in the head, unlike every cue.
    /// </remarks>
    public sealed class XRMusic : MonoBehaviour
    {
        private const string Folder = "XRMusic/";

        /// <summary>Full-scale levels before the player's own volume: the loops sit well under the game.</summary>
        private const float LoopLevel = 0.35f;
        private const float JingleLevel = 0.8f;
        private const float PauseDuck = 0.5f;
        private const float FadeSeconds = 1.2f;
        private const float JingleFadeSeconds = 1.5f;

        private GameFlow _flow;
        private XRPreferences _preferences;
        private AudioSource _current;
        private AudioSource _previous;
        private AudioSource _jingle;
        private AudioClip _concourse;
        private AudioClip _platform;
        private XRMusicLoop _loop;
        private float _duck = 1f;
        private float _fade = 1f;

        /// <summary>The jingle's own fade: 1 while it plays, falling to 0 once the results show.</summary>
        private float _jingleGain = 1f;
        private bool _jingleFading;

        /// <summary>The jingle of the line in play, loaded ahead of the solve so the winning drop never waits on it.</summary>
        private ResourceRequest _jingleRequest;
        private int _jingleLoading = -1;

        public static XRMusic Create(GameFlow flow, XRPreferences preferences)
        {
            var go = new GameObject("Music");
            var music = go.AddComponent<XRMusic>();
            music._flow = flow;
            music._preferences = preferences;
            music._current = music.Source(true);
            music._previous = music.Source(true);
            music._jingle = music.Source(false);
            music._concourse = Resources.Load<AudioClip>(Folder + "Bgm-Concourse");
            music._platform = Resources.Load<AudioClip>(Folder + "Bgm-Platform");
            if (music._concourse == null || music._platform == null)
                Debug.LogWarning("[XR music] The loops are missing: run Window > TrainSudoku > XR > Compose Music.", music);
            flow.StateChanged += music.OnStateChanged;
            music.Apply(flow.State);
            return music;
        }

        private void OnDestroy()
        {
            if (_flow != null) _flow.StateChanged -= OnStateChanged;
        }

        private float Music => _preferences != null ? (float)_preferences.MusicLevel : 1f;
        private float Effects => _preferences != null ? (float)_preferences.EffectsLevel : 1f;

        private void OnStateChanged(GameState previous, GameState current) => Apply(current);

        private void Apply(GameState state)
        {
            _duck = XRMusicPlan.Ducked(state) ? PauseDuck : 1f;
            Play(XRMusicPlan.Loop(state));
            if (state == GameState.Play) Preload(XRMusicPlan.Jingle(_flow.CurrentLineIndex));
            if (XRMusicPlan.StartsJingle(state)) Arrive(XRMusicPlan.Jingle(_flow.CurrentLineIndex));
            else if (XRMusicPlan.FadesJingle(state)) _jingleFading = true;
        }

        /// <summary>Crossfades to <paramref name="loop"/>; the loop already playing carries on where it is.</summary>
        private void Play(XRMusicLoop loop)
        {
            if (loop == _loop) return;
            _loop = loop;
            if (loop == XRMusicLoop.None) return;

            var clip = loop == XRMusicLoop.Concourse ? _concourse : _platform;
            if (clip == null) return;

            // The outgoing loop becomes the previous source and fades from wherever it is.
            (_previous, _current) = (_current, _previous);
            _current.clip = clip;
            _current.volume = 0f;
            _current.Play();
            _fade = 0f;
            Debug.Log($"[XR music] {loop}.");
        }

        private static string JinglePath(int jingle) => $"{Folder}Jingle-{jingle:00}";

        private void Preload(int jingle)
        {
            if (jingle < 0 || jingle == _jingleLoading) return;
            _jingleLoading = jingle;
            _jingleRequest = Resources.LoadAsync<AudioClip>(JinglePath(jingle));
        }

        /// <summary>The line's jingle, once.</summary>
        private void Arrive(int jingle)
        {
            if (jingle < 0) return;
            var loaded = jingle == _jingleLoading && _jingleRequest != null && _jingleRequest.isDone;
            var clip = loaded ? _jingleRequest.asset as AudioClip : Resources.Load<AudioClip>(JinglePath(jingle));
            if (clip == null) return;
            _jingle.Stop();
            _jingleFading = false;
            _jingleGain = 1f;
            _jingle.clip = clip;
            _jingle.volume = JingleLevel * Effects;
            _jingle.Play();
            Debug.Log($"[XR music] Arrival jingle {jingle:00}.");
        }

        private void Update()
        {
            // The crossfade, on unscaled time like every other motion in the game.
            var step = Time.unscaledDeltaTime / FadeSeconds;
            _fade = Mathf.MoveTowards(_fade, 1f, step);
            var level = LoopLevel * Music * _duck;
            _current.volume = _loop == XRMusicLoop.None ? Mathf.MoveTowards(_current.volume, 0f, step * LoopLevel) : level * _fade;
            _previous.volume = level * (1f - _fade);
            if (_fade >= 1f && _previous.isPlaying) _previous.Stop();
            if (_loop == XRMusicLoop.None && _current.isPlaying && _current.volume <= 0f) _current.Stop();

            if (_jingleFading)
            {
                _jingleGain = Mathf.MoveTowards(_jingleGain, 0f, Time.unscaledDeltaTime / JingleFadeSeconds);
                if (_jingleGain <= 0f && _jingle.isPlaying) _jingle.Stop();
            }

            _jingle.volume = JingleLevel * Effects * _jingleGain;
        }

        private AudioSource Source(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }
    }
}
