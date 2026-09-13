using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace TrainSudoku.XR.Editor
{
    /// <summary>
    /// Builds the XR assets that need the Editor's own APIs rather than hand-written YAML. XR2: the Quest render
    /// pipeline, a copy of the phone's mobile one re-tuned per XR-PRD section 8, and <c>Scenes/XR.unity</c> with an AR
    /// Session, the XRI hands-and-controllers rig with a passthrough camera, and a board-scale test cube. XR4: the
    /// <see cref="XRBoardAssets"/> and the board demo in that scene. The build profile, XR Plug-in Management and the
    /// package samples are set up by hand; <c>Docs/XR-Agent.md</c> lists the steps. None of it touches the phone's
    /// assets or open scene.
    /// </summary>
    static class XRFoundationSetup
    {
        const string GraphicsFolder = "Assets/02.Graphics/XR";
        const string PipelinePath = GraphicsFolder + "/XR_RPAsset.asset";
        const string RendererPath = GraphicsFolder + "/XR_Renderer.asset";
        const string SourcePipeline = "Assets/02.Graphics/RenderPipeline/Mobile_RPAsset.asset";
        const string SourceRenderer = "Assets/02.Graphics/RenderPipeline/Mobile_Renderer.asset";
        const string ScenePath = "Assets/Scenes/XR.unity";

        const string DataFolder = "Assets/03.Data/XR";
        const string BoardAssetsPath = DataFolder + "/XRBoardAssets.asset";
        const string NetworkPath = "Assets/03.Data/Levels/Network.asset";
        const string BoardDemoName = "Board Demo";

        // The phone's art hooks. The XR asset is seeded from them once, field by field, and is XR's own after that.
        static readonly string[] PhoneArtAssets = { "Assets/03.Data/Board/TrackAssets.asset", "Assets/03.Data/Board/TrainAssets.asset" };

        // The XRI "Hands Interaction Demo" rig: XR Origin with hands and controllers, switched by
        // the XR Input Modality Manager. It is a variant of the Starter Assets rig, which carries the
        // Input Action Manager that enables their actions; that base rig is the controllers-only fallback.
        const string HandsRigPrefab = "XR Origin Hands (XR Rig)";
        const string ControllersRigPrefab = "XR Origin (XR Rig)";

        // One board cell (6 cm, XR-PRD X6), 60 cm ahead at about waist height.
        static readonly Vector3 CubePosition = new Vector3(0f, 0.9f, 0.6f);
        const float CubeSize = 0.06f;

        [MenuItem("Window/TrainSudoku/XR/Create Render Pipeline")]
        static void CreatePipeline()
        {
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath) != null)
            {
                Debug.Log($"[XR] {PipelinePath} already exists; left as it is.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(GraphicsFolder))
                AssetDatabase.CreateFolder("Assets/02.Graphics", "XR");
            if (!AssetDatabase.CopyAsset(SourceRenderer, RendererPath) || !AssetDatabase.CopyAsset(SourcePipeline, PipelinePath))
            {
                Debug.LogError($"[XR] Could not copy {SourcePipeline} and {SourceRenderer}.");
                return;
            }

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (renderer == null || pipeline == null)
            {
                Debug.LogError("[XR] The copied pipeline or renderer is not a URP Universal one.");
                return;
            }

            // XR-PRD section 8: Forward, post-processing off, HDR off, 4x MSAA.
            renderer.renderingMode = RenderingMode.Forward;
            renderer.postProcessData = null;
            EditorUtility.SetDirty(renderer);

            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 4;
            var serialized = new SerializedObject(pipeline);
            var renderers = serialized.FindProperty("m_RendererDataList");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            // Shadows sized for a 6 cm board rather than a phone scene. The mobile asset's 50 m distance on one 1024 map
            // makes a shadow texel about 10 cm, wider than a cell, so a hand's shadow on the board came out striped.
            // 2.5 m covers the board and the hands over it at about 2.5 mm a texel.
            serialized.FindProperty("m_ShadowDistance").floatValue = 2.5f;
            serialized.FindProperty("m_MainLightShadowmapResolution").intValue = 2048;
            serialized.FindProperty("m_ShadowCascadeCount").intValue = 1;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.FindProperty("m_SoftShadowQuality").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            AssetDatabase.SaveAssets();
            Selection.activeObject = pipeline;
            Debug.Log($"[XR] Created {PipelinePath} with {RendererPath}. Assign it on the Meta Quest build profile.");
        }

        [MenuItem("Window/TrainSudoku/XR/Build XR Scene")]
        static void BuildScene()
        {
            var rig = FindPrefab(HandsRigPrefab);
            if (rig == null)
            {
                rig = FindPrefab(ControllersRigPrefab);
                if (rig == null)
                {
                    Debug.LogError("[XR] No XRI rig prefab found. Import the XR Interaction Toolkit samples " +
                                   "'Starter Assets' and 'Hands Interaction Demo', and XR Hands 'HandVisualizer', first.");
                    return;
                }
                Debug.LogWarning($"[XR] '{HandsRigPrefab}' not found; using the controllers-only '{ControllersRigPrefab}'.");
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Build XR Scene", $"{ScenePath} exists. Replace it?", "Replace", "Cancel"))
                return;

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));

                var rigInstance = (GameObject)PrefabUtility.InstantiatePrefab(rig, scene);
                var origin = rigInstance.GetComponentInChildren<XROrigin>();
                var camera = origin != null ? origin.Camera : rigInstance.GetComponentInChildren<Camera>();
                if (camera == null)
                {
                    Debug.LogError($"[XR] '{rig.name}' has no camera.");
                    return;
                }
                // Passthrough is layered behind the camera image, so the camera must clear to
                // transparent black; a skybox or an opaque colour would cover it.
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                if (!camera.TryGetComponent<ARCameraManager>(out _))
                    camera.gameObject.AddComponent<ARCameraManager>();

                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Board Scale Cube (6 cm)";
                cube.transform.SetPositionAndRotation(CubePosition, Quaternion.identity);
                cube.transform.localScale = Vector3.one * CubeSize;

                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log($"[XR] Built {ScenePath} around '{rig.name}'. Put it alone in the Meta Quest profile's scene list.");
            }
            finally
            {
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// XR4: adds the self-running board demo to <c>XR.unity</c>, wired to the shipped network and to
        /// <see cref="XRBoardAssets"/>, which it seeds from the phone's art hooks the first time. Running it again only
        /// re-wires the existing demo object.
        /// </summary>
        [MenuItem("Window/TrainSudoku/XR/Add Board Demo to XR Scene")]
        static void AddBoardDemo()
        {
            var boardAssets = EnsureBoardAssets();
            var network = AssetDatabase.LoadAssetAtPath<ScriptableObject>(NetworkPath);
            if (network == null)
            {
                Debug.LogError($"[XR] No network at {NetworkPath}.");
                return;
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == BoardDemoName);
                if (root == null)
                {
                    root = new GameObject(BoardDemoName);
                    SceneManager.MoveGameObjectToScene(root, scene);
                }
                if (!root.TryGetComponent<XRBoardDemo>(out var demo)) demo = root.AddComponent<XRBoardDemo>();

                var serialized = new SerializedObject(demo);
                serialized.FindProperty("network").objectReferenceValue = network;
                serialized.FindProperty("assets").objectReferenceValue = boardAssets;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[XR] '{BoardDemoName}' in {ScenePath} plays the network from {NetworkPath}.");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// The XR board's art hooks. Created the first time by copying every field whose name matches from the phone's
        /// TrackAssets and TrainAssets — the same kit meshes, models and tuning — through serialized properties, so this
        /// editor code never names a phone type.
        /// </summary>
        static XRBoardAssets EnsureBoardAssets()
        {
            var existing = AssetDatabase.LoadAssetAtPath<XRBoardAssets>(BoardAssetsPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets/03.Data", "XR");
            var asset = ScriptableObject.CreateInstance<XRBoardAssets>();
            AssetDatabase.CreateAsset(asset, BoardAssetsPath);

            var target = new SerializedObject(asset);
            foreach (var path in PhoneArtAssets)
            {
                var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (source == null) continue;
                var property = new SerializedObject(source).GetIterator();
                for (var enter = true; property.NextVisible(enter); enter = false)
                    if (property.name != "m_Script" && target.FindProperty(property.propertyPath) != null)
                        target.CopyFromSerializedProperty(property);
            }

            target.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[XR] Created {BoardAssetsPath} from the phone's track and train assets.");
            return asset;
        }

        static GameObject FindPrefab(string name) =>
            AssetDatabase.FindAssets(name + " t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path) == name)
                .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
                .FirstOrDefault();
    }
}
