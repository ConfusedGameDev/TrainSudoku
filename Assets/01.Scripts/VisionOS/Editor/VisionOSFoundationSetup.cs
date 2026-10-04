using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.VisionOS;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.VisionOS;

namespace TrainSudoku.VisionOS.Editor
{
    /// <summary>
    /// The visionOS half of the XR foundation: the render pipeline asset, the visionOS XR loader and the
    /// Metal/Mixed player settings. <c>Window > TrainSudoku > VisionOS</c>.
    /// </summary>
    /// <remarks>
    /// A parallel path to <c>XRFoundationSetup</c>, not a call into it: that class is Quest-owned, its
    /// steps are private, and it hard-codes <c>BuildTargetGroup.Android</c> and
    /// <c>Assets/Scenes/XR.unity</c>. Making it general would be a seam commit on <c>feat/MetaXR</c>
    /// (V-h) for no gain, since almost none of its body applies here.
    ///
    /// **Every step is idempotent and re-runnable**, which is the one improvement over the Quest setup:
    /// XR2's equivalent was a one-off snippet through the Unity MCP bridge, recorded in
    /// <c>Docs/XR-Agent.md</c> prose rather than in code, so it could only be repeated by hand.
    /// </remarks>
    static class VisionOSFoundationSetup
    {
        const BuildTargetGroup k_Group = BuildTargetGroup.VisionOS;

        const string k_GraphicsFolder = "Assets/02.Graphics/VisionOS";
        const string k_PipelinePath = k_GraphicsFolder + "/VisionOS_RPAsset.asset";
        const string k_RendererPath = k_GraphicsFolder + "/VisionOS_Renderer.asset";

        // Copied from the Quest's asset, not from the phone's Mobile_RPAsset. The Quest one already carries
        // XR-PRD 8 (Forward, no post, HDR off, 4x MSAA) and, more importantly, the shadow tuning for a 6 cm
        // board: 2.5 m shadow distance on one 2048 cascade, where the phone's 50 m striped a hand's shadow.
        const string k_SourcePipeline = "Assets/02.Graphics/XR/XR_RPAsset.asset";
        const string k_SourceRenderer = "Assets/02.Graphics/XR/XR_Renderer.asset";

        // Both must be non-empty or visionOS refuses the matching ARKit request *without ever prompting*,
        // because an empty string writes no Info.plist key at all.
        const string k_HandsUsage = "Tsugi tracks your hands so you can pick up track pieces and lay them on the board.";
        const string k_WorldSensingUsage = "Tsugi looks for a flat surface so the railway platform can sit on your real table.";

        /// <summary>Metal app mode requires visionOS 2.0 or newer; Unity defaults to 1.0.</summary>
        const string k_MinimumOSVersion = "2.0";

        /// <summary>
        /// Every setup step, in order, for
        /// <c>-executeMethod TrainSudoku.VisionOS.Editor.VisionOSFoundationSetup.ConfigureAll</c>.
        /// </summary>
        /// <remarks>
        /// The whole point of having this: XR2's equivalent for the Quest was typed into the MCP bridge once
        /// and survives only as prose in <c>Docs/XR-Agent.md</c>. Each step below is idempotent, so this can
        /// be re-run after any editor upgrade or package re-resolve to put the project back.
        /// </remarks>
        public static void ConfigureAll()
        {
            CreatePipeline();
            ConfigureXR();
            CreatePanelSurfaceMaterial();
        }

        const string k_PanelShader = "TrainSudoku/VisionOS/Panel Surface";

        /// <summary>
        /// Must match <c>VisionOSPanelSurface.MaterialResource</c>. Under a <c>Resources</c> folder so the runtime can
        /// load it by name, and so the shader it references is guaranteed into the build — a shader reached only
        /// through <c>Shader.Find</c> at runtime is stripped and comes back null on the headset.
        /// </summary>
        const string k_PanelMaterialPath = k_GraphicsFolder + "/Resources/VisionOSPanelSurface.mat";

        [MenuItem("Window/TrainSudoku/VisionOS/Create Panel Surface Material")]
        static void CreatePanelSurfaceMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(k_PanelMaterialPath) != null)
            {
                Debug.Log($"[AVP] {k_PanelMaterialPath} already exists.");
                return;
            }

            var shader = Shader.Find(k_PanelShader);
            if (shader == null)
            {
                Debug.LogError($"[AVP] Shader '{k_PanelShader}' not found; is VisionOSPanelSurface.shader imported?");
                return;
            }

            if (!AssetDatabase.IsValidFolder(k_GraphicsFolder + "/Resources"))
                AssetDatabase.CreateFolder(k_GraphicsFolder, "Resources");
            AssetDatabase.CreateAsset(new Material(shader) { name = "VisionOSPanelSurface" }, k_PanelMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AVP] Created {k_PanelMaterialPath} on '{k_PanelShader}'.");
        }

        [MenuItem("Window/TrainSudoku/VisionOS/Create Render Pipeline")]
        static void CreatePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(k_PipelinePath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(k_RendererPath);

            if (pipeline == null || renderer == null)
            {
                if (!AssetDatabase.IsValidFolder(k_GraphicsFolder))
                    AssetDatabase.CreateFolder("Assets/02.Graphics", "VisionOS");

                if (renderer == null && !AssetDatabase.CopyAsset(k_SourceRenderer, k_RendererPath) ||
                    pipeline == null && !AssetDatabase.CopyAsset(k_SourcePipeline, k_PipelinePath))
                {
                    Debug.LogError($"[AVP] Could not copy {k_SourcePipeline} and {k_SourceRenderer}.");
                    return;
                }

                pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(k_PipelinePath);
                renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(k_RendererPath);
                if (pipeline == null || renderer == null)
                {
                    Debug.LogError("[AVP] The copied pipeline or renderer is not a URP Universal one.");
                    return;
                }
            }

            // The visionOS package's Project Validation requires a depth texture, and in Metal app mode
            // AfterOpaques is the *only* copy-depth mode supported. Compositor Services reprojects the
            // submitted frame, and it needs a non-zero depth for every pixel to do it.
            renderer.copyDepthMode = CopyDepthMode.AfterOpaques;
            EditorUtility.SetDirty(renderer);

            pipeline.supportsCameraDepthTexture = true;

            // The Quest asset runs at 0.8 to hold 90 Hz on a Quest 3. Vision Pro renders at full scale and
            // buys its headroom with foveated rendering instead, which costs nothing where nobody is looking.
            pipeline.renderScale = 1f;

            var serialized = new SerializedObject(pipeline);
            var renderers = serialized.FindProperty("m_RendererDataList");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            AssetDatabase.SaveAssets();
            Selection.activeObject = pipeline;
            Debug.Log($"[AVP] {k_PipelinePath} ready with {k_RendererPath}: render scale 1, depth texture on, " +
                "copy depth AfterOpaques.");

            AssignToVisionOSQuality(pipeline);
        }

        const string k_QualityName = "visionOS";

        /// <summary>
        /// Points the visionOS platform at <see cref="k_PipelinePath"/> through a quality level of its own.
        /// </summary>
        /// <remarks>
        /// <b>Without this the visionOS build renders with the phone's pipeline.</b> The per-platform quality
        /// table has no visionOS entry, so the platform falls to level 0, <c>Mobile</c>, whose
        /// <c>Mobile_RPAsset</c> has no depth texture, HDR on and render scale 0.8. visionOS reprojects every
        /// frame from the depth the app submits, and on the first headset run (2026-09-22) the result was a
        /// screen of blinking green and pink pixels — while the game itself ran fine underneath, planes and all.
        ///
        /// The Quest solved the same problem the same way: its build profile appended the
        /// <c>Meta Quest (Build Profile)</c> level. XR-PRD 10.2 licenses one appended level for exactly this.
        /// It is excluded from every phone and Quest platform, so neither can ever build with it.
        /// </remarks>
        static void AssignToVisionOSQuality(UniversalRenderPipelineAsset pipeline)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (assets.Length == 0)
            {
                Debug.LogError("[AVP] Could not load ProjectSettings/QualitySettings.asset.");
                return;
            }

            var settings = new SerializedObject(assets[0]);
            var levels = settings.FindProperty("m_QualitySettings");

            var index = -1;
            for (var i = 0; i < levels.arraySize; i++)
                if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == k_QualityName)
                    index = i;

            if (index < 0)
            {
                // Duplicate the last level as the template: on this branch that is the Quest's, which already
                // carries XR-appropriate values (MSAA left to the pipeline asset, shadows sized for a board).
                levels.InsertArrayElementAtIndex(levels.arraySize - 1);
                index = levels.arraySize - 1;
            }

            var level = levels.GetArrayElementAtIndex(index);
            level.FindPropertyRelative("name").stringValue = k_QualityName;
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;

            var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
            string[] notHere = { "Standalone", "WebGL", "Android", "iPhone", "tvOS" };
            excluded.arraySize = notHere.Length;
            for (var i = 0; i < notHere.Length; i++)
                excluded.GetArrayElementAtIndex(i).stringValue = notHere[i];

            // The per-platform default is a serialized map: an array of { first: platform, second: level }.
            var defaults = settings.FindProperty("m_PerPlatformDefaultQuality");
            SerializedProperty entry = null;
            for (var i = 0; i < defaults.arraySize; i++)
                if (defaults.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue == "VisionOS")
                    entry = defaults.GetArrayElementAtIndex(i);
            if (entry == null)
            {
                defaults.InsertArrayElementAtIndex(defaults.arraySize);
                entry = defaults.GetArrayElementAtIndex(defaults.arraySize - 1);
                entry.FindPropertyRelative("first").stringValue = "VisionOS";
            }
            entry.FindPropertyRelative("second").intValue = index;

            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            // Read back through the same API the visionOS package's own Project Validation uses, never echoed.
            QualitySettings.GetRenderPipelineAssetsForPlatform(BuildTargetGroup.VisionOS.ToString(),
                out System.Collections.Generic.HashSet<UniversalRenderPipelineAsset> resolved);
            var names = resolved == null ? "none" : string.Join(", ", resolved.Select(p => p.name));
            Debug.Log($"[AVP] Quality level '{k_QualityName}' (index {index}) is visionOS's default. " +
                $"visionOS now resolves to: {names}");
        }

        [MenuItem("Window/TrainSudoku/VisionOS/Configure visionOS XR")]
        static void ConfigureXR()
        {
            if (!ConfigureLoader()) return;
            ConfigureSettings();
        }

        static bool ConfigureLoader()
        {
            // The per-build-target asset already exists in this project, with the Quest's Android entry in
            // it. We only ever append: Android keeps OpenXR, and iOS and Standalone keep no manager at all,
            // which is what stops Play on SampleScene from starting XR.
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey,
                    out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                Debug.LogError("[AVP] No XRGeneralSettingsPerBuildTarget. Open " +
                    "Project Settings > XR Plug-in Management once to create it, then re-run this.");
                return false;
            }

            // CreateDefaultManagerSettingsForBuildTarget **overwrites** any manager already there, so it is
            // called only when there is none. Calling it unconditionally would silently empty the loader
            // list on every re-run — the opposite of idempotent.
            if (perTarget.ManagerSettingsForBuildTarget(k_Group) == null)
                perTarget.CreateDefaultManagerSettingsForBuildTarget(k_Group);

            var manager = perTarget.ManagerSettingsForBuildTarget(k_Group);
            if (manager == null)
            {
                Debug.LogError($"[AVP] Could not create the XR manager for {k_Group}.");
                return false;
            }

            if (manager.activeLoaders.Any(loader => loader is VisionOSLoader))
            {
                Debug.Log($"[AVP] {nameof(VisionOSLoader)} is already assigned to {k_Group}.");
                return true;
            }

            // The full type name, as AssignLoader's own docs require.
            if (!XRPackageMetadataStore.AssignLoader(manager, typeof(VisionOSLoader).FullName, k_Group))
            {
                Debug.LogError($"[AVP] Could not assign {nameof(VisionOSLoader)} to {k_Group}.");
                return false;
            }

            EditorUtility.SetDirty(perTarget);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AVP] {nameof(VisionOSLoader)} assigned to {k_Group}. Android still uses OpenXR.");
            return true;
        }

        static void ConfigureSettings()
        {
            var settings = VisionOSSettings.GetOrCreateSettings();
            if (settings == null)
            {
                Debug.LogError("[AVP] Could not create the visionOS player settings.");
                return;
            }

            // X13: Metal mode, Mixed immersion, Full Space.
            settings.appMode = VisionOSSettings.AppMode.Metal;
            settings.metalImmersionStyle = VisionOSSettings.ImmersionStyle.Mixed;

            // What replaces XRDepthOcclusion on this platform. visionOS has no occlusion provider at all,
            // but the OS composites the player's own arms over the rendered content, which is the whole
            // reason XRDepthOcclusion was added at XR5 — the board drawn over the player's body.
            settings.upperLimbVisibility = VisionOSSettings.Visibility.Visible;

            // XR-PRD 8. URP only, which we are.
            settings.foveatedRendering = true;

            settings.handsTrackingUsageDescription = k_HandsUsage;
            settings.worldSensingUsageDescription = k_WorldSensingUsage;

            // The package's own Project Validation: "Metal, RealityKit, and Hybrid apps require minimum
            // visionOS version 2.0." Unity defaults this to 1.0, and the consequence is not a build error —
            // it is a *runtime* one. On a 1.0 deployment target the immersive space does not open, so the
            // display provider reports one render pass with one view (monoscopic), and URP's XRSystem
            // refuses that layout: "NotImplementedException: Unsupported XR layout: 1 render passes",
            // thrown every frame from CreateDefaultLayout. The app launches and renders nothing.
            // visionOS-specific, so the phone's target versions are untouched.
            if (PlayerSettings.VisionOS.targetOSVersionString != k_MinimumOSVersion)
                PlayerSettings.VisionOS.targetOSVersionString = k_MinimumOSVersion;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AVP] visionOS settings: appMode={settings.appMode}, " +
                $"immersion={settings.metalImmersionStyle}, upperLimbs={settings.upperLimbVisibility}, " +
                $"foveated={settings.foveatedRendering}, minOS={PlayerSettings.VisionOS.targetOSVersionString}, " +
                "usage descriptions set.");
        }

        /// <summary>
        /// Adds <see cref="VisionOSWorldSensing"/> to the open scene **without saving it**, for an Editor run.
        /// </summary>
        /// <remarks>
        /// On the headset the component installs itself, so the scene asset is never touched. This is only
        /// for pressing Play in the Editor. **Do not save the scene afterwards**: V1 runs the Quest's own
        /// <c>XR.unity</c>, which this branch keeps byte-identical.
        /// </remarks>
        [MenuItem("Window/TrainSudoku/VisionOS/Add World Sensing to Open Scene")]
        static void AddWorldSensing()
        {
            if (Object.FindAnyObjectByType<VisionOSWorldSensing>() != null)
            {
                Debug.Log("[AVP] World sensing is already in the open scene.");
                return;
            }

            var host = new GameObject(nameof(VisionOSWorldSensing));
            host.AddComponent<VisionOSWorldSensing>();
            Undo.RegisterCreatedObjectUndo(host, "Add World Sensing");
            Selection.activeObject = host;
            Debug.LogWarning("[AVP] World sensing added to the open scene for an Editor run. " +
                "Do NOT save the scene: XR.unity is Quest-owned and stays byte-identical on this branch.");
        }
    }
}
