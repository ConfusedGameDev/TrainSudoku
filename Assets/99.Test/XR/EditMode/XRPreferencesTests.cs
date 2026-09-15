using NUnit.Framework;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>XR-PRD 6.4: the headset's settings, kept beside the board's anchor and never in the shared save.</summary>
    public class XRPreferencesTests
    {
        [Test]
        public void DefaultsToTheGivenHandFullVolumeAndTheSystemLanguage()
        {
            var preferences = new XRPreferences(new InMemoryPreferenceStore(), Hand.Left);
            Assert.AreEqual(Hand.Left, preferences.DominantHand);
            Assert.AreEqual(Hand.Right, preferences.WristHand);
            Assert.AreEqual(XRPreferences.VolumeSteps, preferences.MusicVolume);
            Assert.AreEqual(XRPreferences.VolumeSteps, preferences.EffectsVolume);
            Assert.AreEqual("", preferences.LocaleCode);
        }

        [Test]
        public void TheMenuIsWornOnTheOtherWrist()
        {
            var preferences = new XRPreferences(new InMemoryPreferenceStore(), Hand.Left);
            preferences.DominantHand = Hand.Right;
            Assert.AreEqual(Hand.Left, preferences.WristHand);
        }

        [Test]
        public void SettingsSurviveARestart()
        {
            var store = new InMemoryPreferenceStore();
            var first = new XRPreferences(store);
            first.DominantHand = Hand.Left;
            first.StepMusic(-3);
            first.StepEffects(-1);
            first.LocaleCode = "ja";

            var second = new XRPreferences(store, Hand.Right);
            Assert.AreEqual(Hand.Left, second.DominantHand);
            Assert.AreEqual(7, second.MusicVolume);
            Assert.AreEqual(9, second.EffectsVolume);
            Assert.AreEqual("ja", second.LocaleCode);
        }

        [Test]
        public void VolumeStaysInItsRange()
        {
            var preferences = new XRPreferences(new InMemoryPreferenceStore());
            Assert.AreEqual(10, preferences.StepMusic(5));
            Assert.AreEqual(0, preferences.StepMusic(-50));
            Assert.AreEqual(1, preferences.StepMusic(1));
            Assert.AreEqual(0.1, preferences.MusicLevel, 1e-9);
            Assert.AreEqual(1.0, preferences.EffectsLevel, 1e-9);
        }

        [Test]
        public void AStoredValueOutOfRangeIsClamped()
        {
            var store = new InMemoryPreferenceStore();
            store.SetInt(XRPreferences.MusicKey, 42);
            store.SetInt(XRPreferences.EffectsKey, -5);
            store.SetInt(XRPreferences.DominantHandKey, 7);

            var preferences = new XRPreferences(store, Hand.Left);
            Assert.AreEqual(XRPreferences.VolumeSteps, preferences.MusicVolume);
            Assert.AreEqual(0, preferences.EffectsVolume);
            Assert.AreEqual(Hand.Left, preferences.DominantHand, "an unknown hand falls back to the default");
        }

        [Test]
        public void NextLocaleWalksTheListAndWrapsRound()
        {
            var codes = new[] { "en", "ja", "es", "fr" };
            Assert.AreEqual("ja", XRPreferences.NextLocale(codes, "en"));
            Assert.AreEqual("en", XRPreferences.NextLocale(codes, "fr"));
            Assert.AreEqual("es", XRPreferences.NextLocale(codes, "JA"), "codes compare without case");
        }

        [Test]
        public void NextLocaleFromAnUnknownOneStartsAtTheFirst()
        {
            var codes = new[] { "en", "ja" };
            Assert.AreEqual("en", XRPreferences.NextLocale(codes, ""));
            Assert.AreEqual("en", XRPreferences.NextLocale(codes, "de"));
            Assert.AreEqual("de", XRPreferences.NextLocale(new string[0], "de"), "no locales: nothing changes");
        }
    }
}
