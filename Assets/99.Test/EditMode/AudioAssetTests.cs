using System;
using System.Linq;
using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// The Unity half of the audio layer (M22) that can be checked without ears: the cue numbering the library asset
    /// depends on, the shipped slot assets, and the rule that every slot left empty is a supported state.
    /// </summary>
    public class AudioAssetTests
    {
        private const string CuesPath = "Assets/03.Data/Audio/UiAudioCues.asset";
        private const string MusicPath = "Assets/03.Data/Audio/Music.asset";

        /// <summary>
        /// The only mechanical enforcement of the append-only rule. The library asset stores each cue as its int, so
        /// renumbering a member silently rewires a clip to a different sound.
        /// </summary>
        [Test]
        public void CueNumberingIsFixed()
        {
            Assert.AreEqual(0, (int)AudioCue.UiClick);
            Assert.AreEqual(1, (int)AudioCue.UiBack);
            Assert.AreEqual(2, (int)AudioCue.UiConfirm);
            Assert.AreEqual(3, (int)AudioCue.Place);
            Assert.AreEqual(4, (int)AudioCue.Erase);
            Assert.AreEqual(5, (int)AudioCue.Error);
            Assert.AreEqual(6, (int)AudioCue.FinalPiece, "Renamed from Win in M22; the number is what the asset stores.");
            Assert.AreEqual(7, (int)AudioCue.TrainStart);
            Assert.AreEqual(8, (int)AudioCue.MapOpen);
            Assert.AreEqual(9, (int)AudioCue.StationSelect);
            Assert.AreEqual(10, (int)AudioCue.LineUnlocked);
            Assert.AreEqual(11, (int)AudioCue.StarAwarded);
            Assert.AreEqual(12, (int)AudioCue.CellSelect);
            Assert.AreEqual(13, (int)AudioCue.SideChosen);
            Assert.AreEqual(14, (int)AudioCue.TrainMoving);
            Assert.AreEqual(15, (int)AudioCue.EraseHold);
            Assert.AreEqual(16, (int)AudioCue.LineCleared);
            Assert.AreEqual(17, (int)AudioCue.WinFanfareOne);
            Assert.AreEqual(18, (int)AudioCue.WinFanfareTwo);
            Assert.AreEqual(19, (int)AudioCue.WinFanfareThree);
            Assert.AreEqual(20, Enum.GetValues(typeof(AudioCue)).Length, "A new cue needs a line here, and must be appended.");
        }

        [Test]
        public void LoopsNeverBuzz()
        {
            Assert.AreEqual(HapticFeel.None, Haptics.FeelOf(AudioCue.TrainMoving));
            Assert.AreEqual(HapticFeel.None, Haptics.FeelOf(AudioCue.EraseHold));
        }

        [Test]
        public void TheBoardTicksKeepTheirFeel()
        {
            // Before M22 BoardView played these as bare haptics; routing them through a cue must not change the feel.
            Assert.AreEqual(HapticFeel.Selection, Haptics.FeelOf(AudioCue.CellSelect));
            Assert.AreEqual(HapticFeel.Selection, Haptics.FeelOf(AudioCue.SideChosen));
        }

        [Test]
        public void TheCueLibraryHasASlotForEveryCue()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioCueLibrary>(CuesPath);
            Assert.IsNotNull(library, CuesPath);

            var so = new SerializedObject(library);
            var entries = so.FindProperty("entries");
            var cues = Enumerable.Range(0, entries.arraySize)
                .Select(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("cue").intValue)
                .ToList();

            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
                Assert.Contains((int)cue, cues, $"No slot for {cue} in {CuesPath}.");
        }

        [Test]
        public void TheFanfaresRideThroughAScreenChange()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioCueLibrary>(CuesPath);
            Assert.IsNotNull(library, CuesPath);

            var so = new SerializedObject(library);
            var entries = so.FindProperty("entries");
            bool Holds(AudioCue cue)
            {
                for (var i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("cue").intValue == (int)cue)
                        return entry.FindPropertyRelative("holdThroughTransition").boolValue;
                }

                return false;
            }

            Assert.IsTrue(Holds(AudioCue.WinFanfareOne));
            Assert.IsTrue(Holds(AudioCue.WinFanfareTwo));
            Assert.IsTrue(Holds(AudioCue.WinFanfareThree));
            Assert.IsTrue(Holds(AudioCue.LineCleared));

            // And the clicks are the ones a transition cuts, which is the whole brief.
            Assert.IsFalse(Holds(AudioCue.UiClick));
            Assert.IsFalse(Holds(AudioCue.UiBack));
            Assert.IsFalse(Holds(AudioCue.UiConfirm));
        }

        [Test]
        public void TheMusicLibraryHasASlotForEveryTrack()
        {
            var library = AssetDatabase.LoadAssetAtPath<MusicLibrary>(MusicPath);
            Assert.IsNotNull(library, MusicPath);

            var so = new SerializedObject(library);
            var entries = so.FindProperty("entries");
            var tracks = Enumerable.Range(0, entries.arraySize)
                .Select(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("track").intValue)
                .ToList();

            foreach (MusicTrack track in Enum.GetValues(typeof(MusicTrack)))
            {
                if (track == MusicTrack.None) continue;
                Assert.Contains((int)track, tracks, $"No slot for {track} in {MusicPath}.");
            }
        }

        // ---- every slot empty is a supported state

        [Test]
        public void AnEmptyCueLibraryIsSilentNotBroken()
        {
            var library = ScriptableObject.CreateInstance<AudioCueLibrary>();
            try
            {
                Assert.IsFalse(library.TryGetClip(AudioCue.UiClick, out _, out _));
                Assert.IsNull(library.OutputFor(AudioCue.UiClick, false), "No group means the mixer's default, not an error.");
                Assert.IsNull(library.Mixer);
                Assert.IsFalse(library.HoldsThroughTransition(AudioCue.WinFanfareThree),
                    "An unwired cue is cancelled, which is the safe default.");
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void AnEmptyMusicLibraryIsSilentNotBroken()
        {
            var library = ScriptableObject.CreateInstance<MusicLibrary>();
            try
            {
                foreach (MusicTrack track in Enum.GetValues(typeof(MusicTrack)))
                    Assert.IsFalse(library.TryGet(track, out _));
                Assert.Greater(library.CrossfadeSeconds, 0f);
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void AMusicPlayerWithNoLibraryStillTracksTheState()
        {
            var go = new GameObject("MusicPlayerTest");
            try
            {
                var player = go.AddComponent<MusicPlayer>();
                Assert.DoesNotThrow(() =>
                {
                    player.Configure(null);
                    player.Play(MusicTrack.MainMenu);
                    player.SetPaused(true);
                    player.SetPaused(false);
                    player.Play(MusicTrack.PlayTwoStar);
                    player.Play(MusicTrack.None);
                });

                player.Play(MusicTrack.Map);
                Assert.AreEqual(MusicTrack.Map, player.Current,
                    "Silent, but the state machine is still exercised, so wiring a clip later changes nothing else.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CueCallsAreSafeWithNoPlayerInTheScene()
        {
            Assert.DoesNotThrow(() =>
            {
                AudioCuePlayer.CancelInFlight();
                AudioCuePlayer.PlayLoop(AudioCue.TrainMoving);
                AudioCuePlayer.SetLoopPitch(AudioCue.TrainMoving, 1.2f);
                AudioCuePlayer.SetLoopsPaused(true);
                AudioCuePlayer.SetLoopsPaused(false);
                AudioCuePlayer.StopLoop(AudioCue.TrainMoving);
                AudioCuePlayer.StopLoop(AudioCue.TrainMoving);
            });
        }
    }
}
