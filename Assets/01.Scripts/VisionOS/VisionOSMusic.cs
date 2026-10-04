using TrainSudoku.Core;
using TrainSudoku.XR;
using TrainSudoku.XR.Rules;
using UnityEngine;

namespace TrainSudoku.VisionOS
{
    /// <summary>
    /// The Vision Pro edition's music: a background loop for the menus and maps, a quieter one while a station is
    /// played, and the line's own jingle from the moment a station is solved, over the train's run into the platform.
    /// </summary>
    /// <remarks>
    /// <b>Why it is here.</b> The XR edition plays no music at all — its wrist Settings has had a MUSIC volume since XR8
    /// with nothing reading it — and the phone's <c>MusicPlayer</c> lives in the Game assembly with empty slots. On
    /// 2026-10-04 the choice was visionOS first, without touching the phone or the Quest (VisionOS-PRD 2), so this is a
    /// self-installing component reading clips written by <c>VisionOSMusicComposer</c>.
    ///
    /// <b>The map follows the phone's</b> (<c>MusicPlan</c>, D22): one track for the menus, one for play, the pause
    /// keeping play's track (here at half volume rather than stopped, since the signboard's pause has no music of its
    /// own). Where it departs from the phone is the arrival: each line has a jingle, as each station on the Yamanote
    /// line has its own melody. It starts the moment the board is solved and the loop stops, plays while the train
    /// runs the rails, and fades out as the results come up; the loop fades back in with the next station
    /// (2026-10-04, headset: "the get to station jingle should play as soon as you complete the level while you see
    /// the train going through the rails and fade out when it gets to the destination").
    ///
    /// <b>Volumes are the wrist menu's.</b> The loops follow MUSIC, so MUSIC at 0 is the off switch the player asked
    /// for; the jingle follows EFFECTS, so switching the background music off keeps the arrival. Both are read from
    /// <see cref="PlayerPrefs"/> under <see cref="XRPreferences"/>' own keys, the store <c>XRGame</c> writes them to,
    /// because the game keeps its preferences object private.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class VisionOSMusic : MonoBehaviour
    {
        const string k_Tag = "[AVP music]";
        const string k_Folder = "VisionOSMusic/";

        /// <summary>Full-scale levels before the player's own volume: the loops sit well under the game.</summary>
        const float k_LoopLevel = 0.35f;
        const float k_JingleLevel = 0.8f;
        const float k_PauseDuck = 0.5f;
        const float k_FadeSeconds = 1.2f;
        const float k_JingleFadeSeconds = 1.5f;
        const float k_PreferencePoll = 0.25f;

        enum Track { None, Concourse, Platform }

        XRGame m_Game;
        GameFlow m_Flow;
        AudioSource m_Current;
        AudioSource m_Previous;
        AudioSource m_Jingle;
        AudioClip m_Concourse;
        AudioClip m_Platform;
        Track m_Track;
        float m_Duck = 1f;
        float m_Fade = 1f;
        float m_Music = 1f;
        float m_Effects = 1f;
        float m_NextPoll;

        /// <summary>The jingle's own fade: 1 while it plays, falling to 0 once the results show.</summary>
        float m_JingleGain = 1f;
        bool m_JingleFading;

#if UNITY_VISIONOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<VisionOSMusic>() != null) return;
            var host = new GameObject(nameof(VisionOSMusic));
            DontDestroyOnLoad(host);
            host.AddComponent<VisionOSMusic>();
        }
#endif

        void Awake()
        {
            m_Current = Source(true);
            m_Previous = Source(true);
            m_Jingle = Source(false);
        }

        AudioSource Source(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        void OnDestroy()
        {
            if (m_Flow != null) m_Flow.StateChanged -= OnStateChanged;
        }

        void Update()
        {
            if (m_Flow == null && !Hook()) return;

            if (Time.unscaledTime >= m_NextPoll)
            {
                m_NextPoll = Time.unscaledTime + k_PreferencePoll;
                m_Music = PlayerPrefs.GetInt(XRPreferences.MusicKey, XRPreferences.VolumeSteps) / (float)XRPreferences.VolumeSteps;
                m_Effects = PlayerPrefs.GetInt(XRPreferences.EffectsKey, XRPreferences.VolumeSteps) / (float)XRPreferences.VolumeSteps;
            }

            // The crossfade, on unscaled time like every other motion in the game.
            m_Fade = Mathf.MoveTowards(m_Fade, 1f, Time.unscaledDeltaTime / k_FadeSeconds);
            var level = k_LoopLevel * m_Music * m_Duck;
            m_Current.volume = m_Track == Track.None ? Mathf.MoveTowards(m_Current.volume, 0f, Time.unscaledDeltaTime / k_FadeSeconds * k_LoopLevel) : level * m_Fade;
            m_Previous.volume = level * (1f - m_Fade);
            if (m_Fade >= 1f && m_Previous.isPlaying) m_Previous.Stop();
            if (m_Track == Track.None && m_Current.isPlaying && m_Current.volume <= 0f) m_Current.Stop();
            if (m_JingleFading)
            {
                m_JingleGain = Mathf.MoveTowards(m_JingleGain, 0f, Time.unscaledDeltaTime / k_JingleFadeSeconds);
                if (m_JingleGain <= 0f && m_Jingle.isPlaying) m_Jingle.Stop();
            }

            m_Jingle.volume = k_JingleLevel * m_Effects * m_JingleGain;
        }

        bool Hook()
        {
            if (m_Game == null) m_Game = FindAnyObjectByType<XRGame>();
            if (m_Game == null || m_Game.Flow == null) return false;

            m_Flow = m_Game.Flow;
            m_Flow.StateChanged += OnStateChanged;
            m_Concourse = Resources.Load<AudioClip>(k_Folder + "Bgm-Concourse");
            m_Platform = Resources.Load<AudioClip>(k_Folder + "Bgm-Platform");
            Debug.Log($"{k_Tag} Listening to the flow; loops {(m_Concourse != null && m_Platform != null ? "loaded" : "MISSING — run Compose Music")}.");
            Apply(m_Flow.State, m_Flow.State);
            return true;
        }

        void OnStateChanged(GameState previous, GameState current) => Apply(previous, current);

        void Apply(GameState previous, GameState current)
        {
            m_Duck = current == GameState.Pause ? k_PauseDuck : 1f;
            switch (current)
            {
                case GameState.Play:
                case GameState.Pause:
                    Play(Track.Platform);
                    break;
                case GameState.TrainRun:
                    // Solved: the loop gives way to the line's jingle for the run into the platform.
                    Play(Track.None);
                    Arrive(m_Flow.CurrentLineIndex);
                    break;
                case GameState.Win:
                    Play(Track.None);
                    m_JingleFading = true;
                    break;
                default:
                    Play(Track.Concourse);
                    break;
            }
        }

        /// <summary>Crossfades to <paramref name="track"/>; the track already playing carries on where it is.</summary>
        void Play(Track track)
        {
            if (track == m_Track) return;
            m_Track = track;
            if (track == Track.None) return;

            var clip = track == Track.Concourse ? m_Concourse : m_Platform;
            if (clip == null) return;

            // The outgoing loop becomes the previous source and fades from wherever it is.
            (m_Previous, m_Current) = (m_Current, m_Previous);
            m_Current.clip = clip;
            m_Current.volume = 0f;
            m_Current.Play();
            m_Fade = 0f;
            Debug.Log($"{k_Tag} {track}.");
        }

        /// <summary>The line's jingle, once. More lines than jingles reuse them in order.</summary>
        void Arrive(int line)
        {
            if (line < 0) return;
            var clip = Resources.Load<AudioClip>($"{k_Folder}Jingle-{line % 24:00}");
            if (clip == null) return;
            m_Jingle.Stop();
            m_JingleFading = false;
            m_JingleGain = 1f;
            m_Jingle.clip = clip;
            m_Jingle.Play();
            Debug.Log($"{k_Tag} Arrival jingle for line {line}.");
        }
    }
}
