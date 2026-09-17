using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// The player build, as a static method so it can be driven from the command line with
    /// <c>-executeMethod TrainSudoku.Editor.BuildScript.BuildIOS</c>. Before this the project had no build script at
    /// all and every build was a hand-driven File &gt; Build Settings.
    /// </summary>
    /// <remarks>
    /// <b>The export is deliberately a Replace, not an Append.</b> Append preserves by-hand edits to the generated
    /// Xcode project, which sounds helpful and is the reason a build shipped that could not launch: someone had
    /// removed <c>UnityFramework.framework</c> from the app target's Embed Frameworks phase, and Append carried that
    /// removal into every later export. <c>MainApp/main.mm</c> loads the engine by path at runtime
    /// (<c>NSBundle bundleWithPath: …/Frameworks/UnityFramework.framework</c>), so a missing embed is not a link
    /// error — it is a silent exit on launch. A Replace regenerates the project from Unity's own template every
    /// time, which is the only way that phase is guaranteed correct.
    ///
    /// The cost is that any genuine by-hand Xcode change is dropped too, so anything the build needs must be
    /// expressed here or in Player Settings rather than in the Xcode project.
    /// </remarks>
    public static class BuildScript
    {
        /// <summary>
        /// Where the Xcode project is written, relative to the project root. <c>-outputPath</c> overrides it, which
        /// is what lets a build run out of a throwaway clone while writing into the real project's <c>Builds/</c>:
        /// the Editor holds a lock on the project it has open, so batchmode cannot drive the original directly.
        /// </summary>
        private const string DefaultOutputPath = "Builds";

        /// <summary>The command line writes the outcome here so a shell can poll it; a batchmode build outlives the call.</summary>
        private const string ResultFile = "BuildResult.txt";

        [MenuItem("Window/TrainSudoku/Build iOS")]
        public static void BuildIOSMenu() => BuildIOS();

        /// <summary>
        /// Command-line entry. Accepts <c>-buildNumber N</c> and <c>-bundleVersion X.Y.Z</c>; without them the
        /// values already in Player Settings are used, so a careless run cannot silently renumber a release.
        /// </summary>
        public static void BuildIOS()
        {
            var summary = new StringBuilder();
            try
            {
                var buildNumber = Argument("-buildNumber");
                if (!string.IsNullOrEmpty(buildNumber)) PlayerSettings.iOS.buildNumber = buildNumber;

                var version = Argument("-bundleVersion");
                if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;

                // The spec is a portrait game (PRD). An auto-rotating build is a bug that only shows on device.
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

                var scenes = EnabledScenes();
                if (scenes.Length == 0) throw new InvalidOperationException("No enabled scenes in Build Settings.");

                var outputPath = Argument("-outputPath");
                if (string.IsNullOrEmpty(outputPath)) outputPath = DefaultOutputPath;
                summary.AppendLine($"outputTo={outputPath}");

                summary.AppendLine($"version={PlayerSettings.bundleVersion}");
                summary.AppendLine($"buildNumber={PlayerSettings.iOS.buildNumber}");
                summary.AppendLine($"bundleId={PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS)}");
                summary.AppendLine($"orientation={PlayerSettings.defaultInterfaceOrientation}");
                foreach (var scene in scenes) summary.AppendLine($"scene={scene}");

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.iOS,
                    targetGroup = BuildTargetGroup.iOS,
                    // No AcceptExternalModificationsToPlayer: see the remarks above. This is a Replace.
                    options = BuildOptions.None,
                };

                var report = BuildPipeline.BuildPlayer(options);
                var result = report.summary;
                summary.AppendLine($"result={result.result}");
                summary.AppendLine($"errors={result.totalErrors}");
                summary.AppendLine($"warnings={result.totalWarnings}");
                summary.AppendLine($"seconds={result.totalTime.TotalSeconds:F0}");
                summary.AppendLine($"outputPath={result.outputPath}");

                Write(summary.ToString());
                if (result.result != BuildResult.Succeeded)
                    throw new Exception($"Build did not succeed: {result.result}, {result.totalErrors} error(s).");

                Debug.Log($"BuildScript: iOS build succeeded in {result.totalTime.TotalSeconds:F0}s -> {result.outputPath}");
            }
            catch (Exception e)
            {
                summary.AppendLine($"result=Exception");
                summary.AppendLine($"message={e.Message}");
                Write(summary.ToString());
                Debug.LogError($"BuildScript: {e}");
                throw;
            }
        }

        private static string[] EnabledScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled && !string.IsNullOrEmpty(scene.path)) scenes.Add(scene.path);
            return scenes.ToArray();
        }

        /// <summary>One <c>-name value</c> pair off the command line, or null.</summary>
        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            return null;
        }

        private static void Write(string text)
        {
            try
            {
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), ResultFile), text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"BuildScript: could not write {ResultFile}: {e.Message}");
            }
        }
    }
}
