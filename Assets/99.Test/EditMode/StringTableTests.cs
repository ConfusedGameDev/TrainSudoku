using System.Collections.Generic;
using NUnit.Framework;
using TrainSudoku.Core;
using UnityEditor;
using UnityEngine.Localization.Tables;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// The `UI` String Table (work order 3): one row per key, filled in for every locale that ships. Added at M21,
    /// when the tutorial put eight more keys in and nothing would have noticed a locale left behind.
    /// </summary>
    public class StringTableTests
    {
        private const string Folder = "Assets/03.Data/Localization/UI";
        private static readonly string[] Locales = { "en", "ja", "es", "fr" };

        private static SharedTableData LoadShared()
        {
            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>($"{Folder}/UI Shared Data.asset");
            Assert.IsNotNull(shared, $"Missing {Folder}/UI Shared Data.asset");
            return shared;
        }

        [Test]
        public void EveryKeyIsTranslatedInEveryLocale()
        {
            var shared = LoadShared();
            var missing = new List<string>();

            foreach (var locale in Locales)
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"{Folder}/UI_{locale}.asset");
                Assert.IsNotNull(table, $"Missing {Folder}/UI_{locale}.asset");

                foreach (var entry in shared.Entries)
                {
                    var value = table.GetEntry(entry.Id);
                    if (value == null || string.IsNullOrWhiteSpace(value.LocalizedValue))
                        missing.Add($"{locale}/{entry.Key}");
                }
            }

            Assert.IsEmpty(missing, "Untranslated rows: " + string.Join(", ", missing));
        }

        /// <summary>
        /// The tutorial names its rows in Core so the state machine can be tested without the localisation package.
        /// That only holds up while the names match the table.
        /// </summary>
        [Test]
        public void EveryTutorialKeyNamedInCoreExistsInTheTable()
        {
            var shared = LoadShared();
            var keys = new[]
            {
                TutorialKeys.SelectFirst, TutorialKeys.SelectAuto, TutorialKeys.SelectNext,
                TutorialKeys.SideFirst, TutorialKeys.SideSecond, TutorialKeys.SideNext,
                TutorialKeys.Clue, TutorialKeys.Mistake, TutorialKeys.Erase, TutorialKeys.Unlocked,
                TutorialKeys.Refused, TutorialKeys.NoteFixed, TutorialKeys.NoteOverfull,
            };

            foreach (var key in keys)
                Assert.IsNotNull(shared.GetEntry(key), $"TutorialKeys names '{key}', which the UI table has no row for");
        }

        /// <summary>
        /// The briefing's three cards. Their keys live in the element rather than in Core, because nothing in Core
        /// knows the rules are explained before play — so this is the only thing holding them to the table.
        /// </summary>
        [Test]
        public void EveryBriefingCardHasATitleAndABody()
        {
            var shared = LoadShared();
            var keys = new[]
            {
                "tutorial.brief1_title", "tutorial.brief1_body",
                "tutorial.brief2_title", "tutorial.brief2_body",
                "tutorial.brief3_title", "tutorial.brief3_body",
                "tutorial.brief_next", "tutorial.brief_start",
            };

            foreach (var key in keys)
                Assert.IsNotNull(shared.GetEntry(key), $"The tutorial briefing shows '{key}', which has no row");
        }
    }
}
