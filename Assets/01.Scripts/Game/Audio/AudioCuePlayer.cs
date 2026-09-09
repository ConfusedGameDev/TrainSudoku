using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>Plays <see cref="AudioCue"/>s through one AudioSource. Created by the <see cref="GameManager"/>; safe to call when absent.</summary>
    public sealed class AudioCuePlayer : MonoBehaviour
    {
        private static AudioCuePlayer _instance;

        private AudioCueLibrary _library;
        private AudioSource _source;

        public static AudioCuePlayer Create(Transform parent, AudioCueLibrary library)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            var player = go.AddComponent<AudioCuePlayer>();
            player._library = library;
            player._source = source;
            _instance = player;
            return player;
        }

        public static void Play(AudioCue cue)
        {
            if (_instance == null || _instance._library == null) return;
            if (_instance._library.TryGetClip(cue, out var clip, out var volume))
                _instance._source.PlayOneShot(clip, volume);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
