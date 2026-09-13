using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace TrainSudoku.XR.Editor
{
    /// <summary>
    /// Puts <c>com.oculus.permission.USE_SCENE</c> back into the Quest manifest. OpenXR Meta 2.6.1 asks for it through
    /// XR Management's manifest <c>OverrideElements</c>, which matches existing elements by path alone. Its three
    /// <c>uses-permission</c> requests therefore overwrite one another, and only the last, <c>USE_ANCHOR_API</c>,
    /// survives. Without USE_SCENE declared, the runtime request for spatial data is refused outright and plane
    /// detection never starts, so the board could only float (XR-PRD 5.2).
    /// </summary>
    /// <remarks>
    /// It runs only when the OpenXR Planes feature is on for Android, the same condition OpenXR Meta uses, so a phone
    /// Android build is left alone. Remove it once the package merges its permissions correctly.
    /// </remarks>
    class XRManifestPermissions : IPostGenerateGradleAndroidProject
    {
        const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        const string ScenePermission = "com.oculus.permission.USE_SCENE";

        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (!PlanesEnabled()) return;

            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[XR] No manifest at {manifestPath}; {ScenePermission} not added.");
                return;
            }

            var document = new XmlDocument();
            document.Load(manifestPath);
            var manifest = document.DocumentElement;
            if (manifest == null) return;

            var declared = manifest.SelectNodes("uses-permission")?.Cast<XmlElement>()
                .Any(element => element.GetAttribute("name", AndroidNamespace) == ScenePermission) ?? false;
            if (declared) return;

            var permission = document.CreateElement("uses-permission");
            permission.SetAttribute("name", AndroidNamespace, ScenePermission);
            manifest.AppendChild(permission);
            document.Save(manifestPath);
            Debug.Log($"[XR] Added {ScenePermission} to {manifestPath}.");
        }

        static bool PlanesEnabled()
        {
            var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            return settings != null && settings.GetFeatures().Any(feature => feature.GetType().Name == "ARPlaneFeature" && feature.enabled);
        }
    }
}
