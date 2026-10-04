using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TrainSudoku.VisionOS.Editor
{
    /// <summary>
    /// The visionOS player build, as a static method so it can be driven with
    /// <c>-executeMethod TrainSudoku.VisionOS.Editor.VisionOSBuildScript.BuildVisionOS</c>.
    /// </summary>
    /// <remarks>
    /// Modelled on the phone's <c>Assets/01.Scripts/Editor/BuildScript.cs</c>, and it inherits that file's
    /// two hard-won rules.
    ///
    /// <b>The export is a Replace, never an Append.</b> An Append preserves by-hand edits to the generated
    /// Xcode project, which sounds helpful and is how the phone once shipped a build that could not launch:
    /// somebody had removed a framework from an Embed Frameworks phase and every later export carried the
    /// removal forward. The cost is that anything the build needs must live here or in Player Settings.
    ///
    /// <b>Read settings back; never echo the intention.</b> A log line that states what was asked for cannot
    /// report a setting that declined to change, and on iOS that is exactly what hid a silent failure.
    ///
    /// <b>V1 deliberately does not touch version or build numbers.</b> <c>PlayerSettings.bundleVersion</c> is
    /// global and shared with the shipped iPhone app, so a careless visionOS build could renumber a phone
    /// release. Versioning belongs to V5 (release readiness), on the build profile.
    /// </remarks>
    static class VisionOSBuildScript
    {
        /// <summary>Where the device Xcode project is written, relative to the project root.</summary>
        const string k_DeviceOutputPath = "Builds-VisionOS";

        /// <summary>
        /// Where a simulator export goes. Deliberately <b>not</b> the device folder: the export is a Replace,
        /// so sharing one would destroy the device project every time somebody wanted the simulator.
        /// </summary>
        const string k_SimulatorOutputPath = "Builds-VisionOS-Simulator";

        /// <summary>The command line writes the outcome here so a shell can poll it; a build outlives the call.</summary>
        const string k_ResultFile = "BuildResult.txt";

        const string k_ProfilePath = "Assets/Settings/Build Profiles/visionOS.asset";

        /// <summary>V-g. Set per build target, never globally, so the phone's identifier is untouched.</summary>
        const string k_BundleId = "com.GorillaGonzalez.Tsugi.VisionOS";

        [MenuItem("Window/TrainSudoku/VisionOS/Build")]
        static void BuildDeviceMenu() => Build(false);

        [MenuItem("Window/TrainSudoku/VisionOS/Build (Simulator)")]
        static void BuildSimulatorMenu() => Build(true);

        /// <summary>
        /// Command-line entry. Accepts <c>-simulator</c> and <c>-outputPath &lt;path&gt;</c>.
        /// </summary>
        public static void BuildVisionOS() => Build(HasFlag("-simulator"));

        static void Build(bool simulator)
        {
            var summary = new StringBuilder();
            try
            {
                // Set explicitly on BOTH paths, never inherited. sdkVersion is a *persistent* Player Setting,
                // so a simulator build leaves it flipped behind it, and the next device build would quietly
                // produce a binary that cannot install on a headset. Naming it on both paths is what makes
                // the simulator build safe to run.
                PlayerSettings.VisionOS.sdkVersion = simulator
                    ? VisionOSSdkVersion.Simulator
                    : VisionOSSdkVersion.Device;
                summary.AppendLine($"sdk={PlayerSettings.VisionOS.sdkVersion}");

                var named = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.VisionOS);
                if (PlayerSettings.GetApplicationIdentifier(named) != k_BundleId)
                    PlayerSettings.SetApplicationIdentifier(named, k_BundleId);
                summary.AppendLine($"bundleId={PlayerSettings.GetApplicationIdentifier(named)}");

                var outputPath = Argument("-outputPath");
                if (string.IsNullOrEmpty(outputPath))
                    outputPath = simulator ? k_SimulatorOutputPath : k_DeviceOutputPath;
                summary.AppendLine($"outputTo={outputPath}");

                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(k_ProfilePath);
                BuildReport report;

                if (profile != null)
                {
                    // The profile carries product name Tsugi Vision, the scene list and the visionOS render
                    // pipeline as overrides, so none of it lands on the global settings the phone owns. Make
                    // it active first: validation rules read the *active* profile, not the one being built.
                    BuildProfile.SetActiveBuildProfile(profile);
                    summary.AppendLine($"profile={k_ProfilePath}");
                    report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
                    {
                        buildProfile = profile,
                        locationPathName = outputPath,
                        options = BuildOptions.None,
                    });
                }
                else
                {
                    // V1 can build without one. The only loss is cosmetic — the app carries the phone's
                    // global product name — and product name cannot be set globally without renaming the
                    // phone app, which is the trap that once corrupted ProjectSettings.asset.
                    Debug.LogWarning($"[AVP] No build profile at {k_ProfilePath}; building from global " +
                        "settings. The app will carry the phone's product name until the profile exists.");
                    summary.AppendLine("profile=none");

                    var scenes = Scenes();
                    foreach (var scene in scenes) summary.AppendLine($"scene={scene}");

                    report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = outputPath,
                        target = BuildTarget.VisionOS,
                        targetGroup = BuildTargetGroup.VisionOS,
                        // No AcceptExternalModificationsToPlayer: see the remarks. This is a Replace.
                        options = BuildOptions.None,
                    });
                }

                var result = report.summary;
                summary.AppendLine($"result={result.result}");
                summary.AppendLine($"errors={result.totalErrors}");
                summary.AppendLine($"warnings={result.totalWarnings}");
                summary.AppendLine($"seconds={result.totalTime.TotalSeconds:F0}");
                summary.AppendLine($"outputPath={result.outputPath}");

                Write(summary.ToString());
                if (result.result != BuildResult.Succeeded)
                    throw new Exception($"Build did not succeed: {result.result}, {result.totalErrors} error(s).");

                Debug.Log($"[AVP] visionOS build succeeded in {result.totalTime.TotalSeconds:F0}s " +
                    $"-> {result.outputPath}");
            }
            catch (Exception e)
            {
                summary.AppendLine("result=Exception");
                summary.AppendLine($"message={e.Message}");
                Write(summary.ToString());
                Debug.LogError($"[AVP] {e}");
                throw;
            }
        }

        /// <summary>
        /// The scenes a profile-less build gets: the XR scene, named outright.
        /// </summary>
        /// <remarks>
        /// <b>Never the global Build Settings list.</b> That list is the phone's, and on this branch it holds
        /// <c>SampleScene.unity</c> alone — the Quest's scene list rides on the Meta Quest build profile as an
        /// override, exactly so the phone's stays untouched. A visionOS build that inherited it would quietly
        /// produce the phone game wrapped in an immersive space, and succeed while doing it. Editing the
        /// global list to fix that would break the phone instead, so the scene is named here and the list is
        /// left alone.
        /// </remarks>
        static string[] Scenes()
        {
            const string xrScene = "Assets/Scenes/XR.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(xrScene) == null)
                throw new InvalidOperationException($"{xrScene} is missing.");
            return new[] { xrScene };
        }

        /// <summary>Whether a bare <c>-name</c> switch is present on the command line.</summary>
        static bool HasFlag(string name)
        {
            foreach (var arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, name, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>One <c>-name value</c> pair off the command line, or null.</summary>
        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            return null;
        }

        static void Write(string text)
        {
            try
            {
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), k_ResultFile), text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AVP] Could not write {k_ResultFile}: {e.Message}");
            }
        }
    }
}
