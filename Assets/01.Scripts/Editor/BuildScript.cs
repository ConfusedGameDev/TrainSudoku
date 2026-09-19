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

        /// <summary>
        /// Where a simulator export goes. Deliberately <b>not</b> <see cref="DefaultOutputPath"/>: the export is a
        /// Replace, so sharing the folder would destroy the real device project every time somebody wanted to look
        /// at the iPad layout.
        /// </summary>
        private const string SimulatorOutputPath = "Builds-Simulator";

        /// <summary>The command line writes the outcome here so a shell can poll it; a batchmode build outlives the call.</summary>
        private const string ResultFile = "BuildResult.txt";

        [MenuItem("Window/TrainSudoku/Build iOS")]
        public static void BuildIOSMenu() => Build(false);

        /// <summary>
        /// A build that runs in the iOS Simulator rather than on a phone. Its only purpose is looking at the game on
        /// a screen nobody owns — the 13" iPad, which the App Store demands screenshots of because the export is
        /// Universal. It is never the build that ships.
        /// </summary>
        [MenuItem("Window/TrainSudoku/Build iOS (Simulator)")]
        public static void BuildIOSSimulatorMenu() => Build(true);

        /// <summary>
        /// Command-line entry. Accepts <c>-buildNumber N</c>, <c>-bundleVersion X.Y.Z</c> and <c>-simulator</c>;
        /// without them the values already in Player Settings are used, so a careless run cannot silently renumber a
        /// release.
        /// </summary>
        public static void BuildIOS() => Build(HasFlag("-simulator"));

        private static void Build(bool simulator)
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

                // Set explicitly on every build, never inherited. sdkVersion is a *persistent* Player Setting, so a
                // simulator build leaves it flipped behind it: without this line the next device build would quietly
                // produce a binary that cannot install on a phone and is rejected on upload, with nothing in the
                // build log saying why. Naming it on both paths is what makes the simulator build safe to run.
                PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
                summary.AppendLine($"sdk={PlayerSettings.iOS.sdkVersion}");

                // A simulator build has to be arm64, or it cannot be installed on an Apple Silicon Mac — and Unity
                // defaults this to 0, x86_64. The failure it causes is thoroughly misleading: the export succeeds,
                // xcodebuild reports BUILD SUCCEEDED, and only `simctl install` objects, with "This app needs to be
                // updated by the developer to work on this version of iPadOS". The real cause is the line beneath
                // that one, "Failed to find matching arch". Forcing ARCHS=arm64 on xcodebuild does not rescue it
                // either, because Unity ships its own prebuilt simulator libraries (baselib.a, lib_burst_generated.a)
                // carrying only the slice this setting asked for, so the link fails instead of the install.
                //
                // NOT through PlayerSettings.SetPropertyInt. That call compiles, runs, throws nothing, and silently
                // does nothing at all: it no-ops on a property name it does not recognise, and the serialized YAML
                // key "iOSSimulatorArchitecture" is evidently not the name it wants. A build driven through it came
                // out x86_64 with the setting still sitting at 0. simulatorSdkArchitecture is the real, typed,
                // public property.
                if (simulator) PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;

                // Read back, never echoed. The first version of this line printed "simulatorArchitecture=arm64"
                // unconditionally, and that is exactly what hid the silent failure above — a log that states an
                // intention cannot report a setting that declined to change. Anything here that reports what was
                // asked for rather than what is true is worse than no log at all.
                summary.AppendLine($"simulatorArchitecture={PlayerSettings.iOS.simulatorSdkArchitecture}");

                var scenes = EnabledScenes();
                if (scenes.Length == 0) throw new InvalidOperationException("No enabled scenes in Build Settings.");

                var outputPath = Argument("-outputPath");
                if (string.IsNullOrEmpty(outputPath)) outputPath = simulator ? SimulatorOutputPath : DefaultOutputPath;
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

        /// <summary>Whether a bare <c>-name</c> switch is present on the command line.</summary>
        private static bool HasFlag(string name)
        {
            foreach (var arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, name, StringComparison.Ordinal))
                    return true;
            return false;
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
