using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace TrainSudoku.XR.Editor
{
    /// <summary>
    /// The store build (XR11): the Meta Quest profile, then signed with the release key in place of the debug one.
    /// </summary>
    /// <remarks>
    /// The key and its password live <b>outside the repo</b>, in the user's home folder: <c>~/.keystores/tsugi-xr.keystore</c>
    /// and <c>tsugi-xr.credentials.txt</c> beside it (lines <c>alias: ...</c> and <c>password (store and key): ...</c>).
    /// Every update of a published app must be signed with the same key, so losing it means the app can never be
    /// updated: keep a copy somewhere safe.
    ///
    /// The APK is <b>re-signed after the build</b> with the SDK's <c>apksigner</c> rather than by handing Unity the
    /// keystore. The build profile carries its own player settings, so <c>PlayerSettings.Android.keystoreName</c> set
    /// from code lands on the global settings and the profile build ignores it: the first attempt came out
    /// debug-signed. Signing afterwards also keeps the key's path out of every project file. The password reaches
    /// <c>apksigner</c> through an environment variable, never the command line.
    /// </remarks>
    public static class XRReleaseBuild
    {
        public const string ProfilePath = "Assets/Settings/Build Profiles/Meta Quest.asset";
        public const string Output = "builds/TsugiXR-release.apk";
        const string PasswordVariable = "TSUGI_XR_KEY_PASSWORD";

        static string KeyFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".keystores");

        [MenuItem("Window/TrainSudoku/XR/Build Release APK")]
        public static void BuildFromMenu() => Debug.Log($"[XR release] {Build()}");

        /// <summary>Builds and signs the APK. Returns a one-line result, never the password.</summary>
        public static string Build()
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
            if (profile == null) return $"failed: no build profile at {ProfilePath}";

            var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = Output,
                options = BuildOptions.None,
            });
            var summary = report.summary;
            var built = $"result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} seconds={(int)summary.totalTime.TotalSeconds}";
            return summary.result != BuildResult.Succeeded ? built : $"{built}; {Sign(Output)}";
        }

        /// <summary>Replaces the signature on <paramref name="apk"/> with the release key's.</summary>
        public static string Sign(string apk)
        {
            var keystore = Path.Combine(KeyFolder, "tsugi-xr.keystore");
            var credentials = Path.Combine(KeyFolder, "tsugi-xr.credentials.txt");
            if (!File.Exists(apk)) return $"sign failed: no APK at {apk}";
            if (!File.Exists(keystore) || !File.Exists(credentials)) return $"sign failed: no release key at {keystore}";

            string alias = null, password = null;
            foreach (var line in File.ReadAllLines(credentials))
            {
                var colon = line.IndexOf(':');
                if (colon < 0) continue;
                var value = line.Substring(colon + 1).Trim();
                if (line.StartsWith("alias")) alias = value;
                else if (line.StartsWith("password")) password = value;
            }

            if (string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(password)) return "sign failed: the credentials file has no alias or password";

            var tools = Path.Combine(AndroidExternalToolsSettings.sdkRootPath ?? "", "build-tools");
            var signer = Directory.Exists(tools)
                ? Directory.GetDirectories(tools).OrderByDescending(folder => folder, StringComparer.Ordinal)
                    .Select(folder => Path.Combine(folder, "lib", "apksigner.jar")).FirstOrDefault(File.Exists)
                : null;
            if (signer == null) return $"sign failed: no apksigner under {tools}";

            var java = Path.Combine(AndroidExternalToolsSettings.jdkRootPath ?? "", "bin", "java.exe");
            if (!File.Exists(java)) java = Path.Combine(AndroidExternalToolsSettings.jdkRootPath ?? "", "bin", "java");

            var start = new ProcessStartInfo(java,
                // Meta's checks name APK signature scheme v2; left alone, apksigner writes v3 only for a minimum API of 32.
                $"-jar \"{signer}\" sign --v2-signing-enabled true --v3-signing-enabled true --v4-signing-enabled false " +
                $"--ks \"{keystore}\" --ks-key-alias \"{alias}\" " +
                $"--ks-pass env:{PasswordVariable} --key-pass env:{PasswordVariable} \"{Path.GetFullPath(apk)}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.EnvironmentVariables[PasswordVariable] = password;

            using (var process = Process.Start(start))
            {
                var error = process.StandardError.ReadToEnd();
                process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0
                    ? $"signed {apk} with the release key"
                    : $"sign failed (exit {process.ExitCode}): {error.Trim()}";
            }
        }
    }
}
