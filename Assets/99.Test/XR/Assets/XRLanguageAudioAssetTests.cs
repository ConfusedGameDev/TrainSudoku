using System;
using NUnit.Framework;
using TrainSudoku.XR.Editor;
using TrainSudoku.XR.Rules;
using UnityEditor;

namespace TrainSudoku.XR.Tests
{
    /// <summary>
    /// XR10's assets (XR-PRD 9): the `XR` String Table answers every key the code reads in all four locales, the XR ja
    /// face holds every character of the ja copy, and the cue library has a slot for every cue. Kept apart from the rules
    /// tests because these need the Editor and the assets, which the engine-free rules suite must not.
    /// </summary>
    public class XRLanguageAudioAssetTests
    {
        private const string CuesPath = "Assets/03.Data/XR/Audio/XRCues.asset";

        [Test]
        public void EveryKeyHasCopyInEveryLocaleAndEveryJapaneseCharacterIsBaked()
        {
            // The check logs what is missing as an error, which fails the test on its own and names the gap.
            Assert.IsTrue(XRLanguageAudioSetup.Check(), "Run Window > TrainSudoku > XR > Write XR String Table, then Rebuild XR ja Font Atlas.");
        }

        [Test]
        public void EveryCueHasASlotInTheLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<XRCueLibrary>(CuesPath);
            Assert.IsNotNull(library, $"No cue library at {CuesPath}.");
            foreach (XRCue cue in Enum.GetValues(typeof(XRCue)))
            {
                if (cue == XRCue.None) continue;
                Assert.IsNotNull(library.Find(cue), $"{cue} has no slot: run Window > TrainSudoku > XR > Write XR Cue Library.");
            }
        }

        [Test]
        public void ACueWithAClipIsAudible()
        {
            var library = AssetDatabase.LoadAssetAtPath<XRCueLibrary>(CuesPath);
            Assert.IsNotNull(library);
            foreach (var entry in library.Entries)
                if (entry.clip != null) Assert.Greater(entry.volume, 0f, $"{entry.cue} has a clip at volume 0.");
        }
    }
}
