#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// The edits the generated Xcode project needs that Player Settings cannot express.
    /// </summary>
    /// <remarks>
    /// <b>This exists because <see cref="BuildScript"/> is deliberately a Replace, not an Append.</b> Its own remarks
    /// spell out the consequence: a Replace regenerates the Xcode project from Unity's template every time, so any
    /// by-hand change made in Xcode is silently dropped on the next export. Anything the build needs must therefore
    /// be expressed in Player Settings or here. This is "here".
    ///
    /// The one key written is <c>ITSAppUsesNonExemptEncryption</c>. Without it, App Store Connect blocks every single
    /// upload behind the export-compliance question ("Does your app use encryption?") and will not let the build go
    /// to TestFlight until a human answers it — per upload, forever. Tsugi has no networking code at all, so it uses
    /// no encryption beyond what the OS does on its own, and the honest answer is <c>false</c>. Declaring it in the
    /// plist answers the question once, at build time, and the prompt never appears again.
    /// </remarks>
    public static class IosPostProcess
    {
        /// <summary>
        /// Runs after Unity has written the Xcode project. The callback order is above Unity's own (0) so the plist
        /// this edits is the finished one rather than a copy Unity is about to overwrite.
        /// </summary>
        public class WriteExportCompliance : IPostprocessBuildWithReport
        {
            public int callbackOrder => 100;

            public void OnPostprocessBuild(BuildReport report)
            {
                if (report.summary.platform != BuildTarget.iOS) return;

                var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
                if (!File.Exists(plistPath))
                {
                    // Not fatal: a failed or cancelled build has no plist, and throwing here would mask the real error.
                    Debug.LogWarning($"IosPostProcess: no Info.plist at {plistPath}; export compliance not declared.");
                    return;
                }

                var plist = new PlistDocument();
                plist.ReadFromFile(plistPath);
                plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
                plist.WriteToFile(plistPath);

                Debug.Log("IosPostProcess: ITSAppUsesNonExemptEncryption=false written to Info.plist.");
            }
        }
    }
}
#endif
