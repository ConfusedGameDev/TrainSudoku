using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace TrainSudoku.XR.Editor
{
    /// <summary>
    /// The three things the Horizon Store asks of an immersive app's manifest that Unity and OpenXR Meta do not write
    /// (XR11; listed in Docs/XR-Agent.md since the first APK): <c>installLocation="auto"</c>, a required
    /// <c>android.hardware.vr.headtracking</c> feature, and <c>excludeFromRecents</c> on the launch activity.
    /// </summary>
    /// <remarks>
    /// Only a manifest whose launch activity carries <c>com.oculus.intent.category.VR</c> is touched, so a phone Android
    /// build is left alone. Runs after <see cref="XRManifestPermissions"/>.
    /// </remarks>
    class XRStoreManifest : IPostGenerateGradleAndroidProject
    {
        const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        const string VrCategory = "com.oculus.intent.category.VR";
        const string HeadTracking = "android.hardware.vr.headtracking";

        public int callbackOrder => 1001;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // `path` is the unityLibrary module; the launcher module beside it carries installLocation.
            var library = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(library)) return;

            var document = new XmlDocument();
            document.Load(library);
            var manifest = document.DocumentElement;
            if (manifest == null) return;

            var launch = manifest.SelectNodes("application/activity")?.Cast<XmlElement>()
                .FirstOrDefault(activity => activity.SelectNodes("intent-filter/category")?.Cast<XmlElement>()
                    .Any(category => category.GetAttribute("name", AndroidNamespace) == VrCategory) ?? false);
            if (launch == null) return;

            launch.SetAttribute("excludeFromRecents", AndroidNamespace, "true");

            var declared = manifest.SelectNodes("uses-feature")?.Cast<XmlElement>()
                .Any(feature => feature.GetAttribute("name", AndroidNamespace) == HeadTracking) ?? false;
            if (!declared)
            {
                var feature = document.CreateElement("uses-feature");
                feature.SetAttribute("name", AndroidNamespace, HeadTracking);
                feature.SetAttribute("version", AndroidNamespace, "1");
                feature.SetAttribute("required", AndroidNamespace, "true");
                manifest.AppendChild(feature);
            }

            SetInstallLocation(manifest);
            document.Save(library);

            var launcher = Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml");
            if (File.Exists(launcher))
            {
                var launcherDocument = new XmlDocument();
                launcherDocument.Load(launcher);
                if (launcherDocument.DocumentElement != null)
                {
                    SetInstallLocation(launcherDocument.DocumentElement);
                    launcherDocument.Save(launcher);
                }
            }

            Debug.Log("[XR] Store manifest: installLocation auto, head tracking required, launch activity out of recents.");
        }

        static void SetInstallLocation(XmlElement manifest) => manifest.SetAttribute("installLocation", AndroidNamespace, "auto");
    }
}
