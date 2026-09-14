using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace TrainSudoku.XR.Editor
{
    /// <summary>
    /// Builds the XR assets that need the Editor's own APIs rather than hand-written YAML. XR2: the Quest render
    /// pipeline, a copy of the phone's mobile one re-tuned per XR-PRD section 8, and <c>Scenes/XR.unity</c> with an AR
    /// Session, the XRI hands-and-controllers rig with a passthrough camera, and a board-scale test cube. XR4: the
    /// <see cref="XRBoardAssets"/> and the game in that scene. The build profile, XR Plug-in Management and the
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
        const string GameName = "Game";

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
        /// XR4, XR7: the game in <c>XR.unity</c>: the <see cref="XRGame"/> shell on a "Game" object, wired to the shipped
        /// network and to <see cref="XRBoardAssets"/>, which it seeds from the phone's art hooks the first time. A scene from
        /// before XR7 has a "Board Demo" object that ran the stand-in XR7 deleted; that object is taken over, its missing
        /// script removed and its grab input kept. Running it again only re-wires.
        /// </summary>
        [MenuItem("Window/TrainSudoku/XR/Add Game to XR Scene")]
        static void AddGame()
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
                var roots = scene.GetRootGameObjects();
                var root = roots.FirstOrDefault(go => go.name == GameName);
                if (root == null) root = roots.FirstOrDefault(go => go.name == BoardDemoName);
                if (root == null)
                {
                    root = new GameObject(GameName);
                    SceneManager.MoveGameObjectToScene(root, scene);
                }

                root.name = GameName;
                var removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                if (!root.TryGetComponent<XRGame>(out var game)) game = root.AddComponent<XRGame>();

                var serialized = new SerializedObject(game);
                serialized.FindProperty("network").objectReferenceValue = network;
                serialized.FindProperty("assets").objectReferenceValue = boardAssets;
                var placement = roots.Select(go => go.GetComponentInChildren<XRBoardPlacement>(true)).FirstOrDefault(p => p != null);
                if (placement != null) serialized.FindProperty("placement").objectReferenceValue = placement;
                if (root.TryGetComponent<XRIGrabInput>(out var input)) serialized.FindProperty("grabInput").objectReferenceValue = input;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[XR] '{GameName}' in {ScenePath} plays the network from {NetworkPath}" +
                          (removed > 0 ? $"; removed {removed} missing script(s)." : "."));
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static XRGame FindGame(GameObject[] roots) =>
            roots.Select(go => go.GetComponentInChildren<XRGame>(true)).FirstOrDefault(game => game != null);

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

        const string GhostMaterialPath = GraphicsFolder + "/XRPlacementGhost.mat";
        const string PlaneMaterialPath = GraphicsFolder + "/XRDetectedPlane.mat";
        const string PrefabFolder = "Assets/04.Prefabs/XR";
        const string PlanePrefabPath = PrefabFolder + "/XRPlane.prefab";
        const string BoardPlacementName = "Board Placement";
        const string TestCubeName = "Board Scale Cube (6 cm)";

        /// <summary>
        /// XR5: surface placement and the saved anchor (XR-PRD 5.2). Turns on the OpenXR Planes and Anchors features,
        /// makes the ghost and plane materials and the plane prefab, adds the plane and anchor managers to the rig with
        /// planes off until the spatial-data permission is granted, and hangs the game's board from a new
        /// <see cref="XRBoardPlacement"/>. It also switches off the rig's locomotion — the world never moves the player
        /// and the thumbstick is unused (4.5, 8) — and removes the XR2 test cube. Running it again only re-wires.
        /// </summary>
        [MenuItem("Window/TrainSudoku/XR/Add Board Placement to XR Scene")]
        static void AddBoardPlacement()
        {
            EnableOpenXRFeatures("ARPlaneFeature", "ARAnchorFeature");
            var ghost = EnsureTransparentMaterial(GhostMaterialPath, new Color(0.95f, 0.76f, 0.19f, 0.35f));
            var planeMaterial = EnsureTransparentMaterial(PlaneMaterialPath, new Color(1f, 1f, 1f, 0.12f));
            var planePrefab = EnsurePlanePrefab(planeMaterial);
            AssignShadowCatcher(EnsureShadowCatcherMaterial());

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var origin = roots.Select(go => go.GetComponentInChildren<XROrigin>(true)).FirstOrDefault(o => o != null);
                if (origin == null)
                {
                    Debug.LogError($"[XR] No XR Origin in {ScenePath}; run Build XR Scene first.");
                    return;
                }

                if (!origin.TryGetComponent<ARPlaneManager>(out var planeManager)) planeManager = origin.gameObject.AddComponent<ARPlaneManager>();
                planeManager.planePrefab = planePrefab;
                planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
                planeManager.enabled = false;
                if (!origin.TryGetComponent<ARAnchorManager>(out var anchorManager)) anchorManager = origin.gameObject.AddComponent<ARAnchorManager>();

                var root = roots.FirstOrDefault(go => go.name == BoardPlacementName);
                if (root == null)
                {
                    root = new GameObject(BoardPlacementName);
                    SceneManager.MoveGameObjectToScene(root, scene);
                }
                if (!root.TryGetComponent<XRBoardPlacement>(out var placement)) placement = root.AddComponent<XRBoardPlacement>();
                var serialized = new SerializedObject(placement);
                serialized.FindProperty("planes").objectReferenceValue = planeManager;
                serialized.FindProperty("anchors").objectReferenceValue = anchorManager;
                serialized.FindProperty("ghostMaterial").objectReferenceValue = ghost;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var game = FindGame(roots);
                if (game != null)
                {
                    var gameSerialized = new SerializedObject(game);
                    gameSerialized.FindProperty("placement").objectReferenceValue = placement;
                    gameSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                var locomotion = origin.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Locomotion");
                if (locomotion != null) locomotion.gameObject.SetActive(false);
                var cube = roots.FirstOrDefault(go => go.name == TestCubeName);
                if (cube != null) Object.DestroyImmediate(cube);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[XR] Board placement wired in {ScenePath}: planes, anchors, ghost; locomotion off.");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        const string OccludedLitPath = GraphicsFolder + "/XROccludedLit.mat";
        const string OccludedTextPath = GraphicsFolder + "/XROccludedText.mat";

        /// <summary>
        /// XR5: environment depth occlusion (XR-PRD 5.6). Turns on the OpenXR Occlusion feature, makes the two template
        /// materials the board's materials are made from, adds AR Foundation's occlusion manager and shader occlusion to
        /// the camera (hard occlusion from environment depth, off until the spatial-data permission is granted), and
        /// wires an <see cref="XRDepthOcclusion"/> to them. Running it again only re-wires.
        /// </summary>
        [MenuItem("Window/TrainSudoku/XR/Add Depth Occlusion to XR Scene")]
        static void AddDepthOcclusion()
        {
            EnableOpenXRFeatures("AROcclusionFeature");
            var lit = EnsureShaderMaterial(OccludedLitPath, "TrainSudoku/XR/Occluded Lit");
            var text = EnsureShaderMaterial(OccludedTextPath, "TrainSudoku/XR/Occluded Text");
            if (lit == null || text == null) return;

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var origin = roots.Select(go => go.GetComponentInChildren<XROrigin>(true)).FirstOrDefault(o => o != null);
                if (origin == null || origin.Camera == null)
                {
                    Debug.LogError($"[XR] No XR Origin camera in {ScenePath}; run Build XR Scene first.");
                    return;
                }

                var camera = origin.Camera.gameObject;
                if (!camera.TryGetComponent<AROcclusionManager>(out var occlusion)) occlusion = camera.AddComponent<AROcclusionManager>();
                occlusion.requestedEnvironmentDepthMode = EnvironmentDepthMode.Medium;
                occlusion.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
                occlusion.environmentDepthTemporalSmoothingRequested = true;
                occlusion.enabled = false;

                if (!camera.TryGetComponent<ARShaderOcclusion>(out var shaderOcclusion)) shaderOcclusion = camera.AddComponent<ARShaderOcclusion>();
                var shaderSettings = new SerializedObject(shaderOcclusion);
                shaderSettings.FindProperty("m_OcclusionManager").objectReferenceValue = occlusion;
                shaderSettings.FindProperty("m_OcclusionShaderMode").intValue = (int)AROcclusionShaderMode.HardOcclusion;
                shaderSettings.FindProperty("m_AROcclusionSources").intValue = (int)AROcclusionSources.EnvironmentDepth;
                shaderSettings.ApplyModifiedPropertiesWithoutUndo();
                shaderOcclusion.enabled = false;

                var root = roots.FirstOrDefault(go => go.name == BoardPlacementName);
                if (root == null)
                {
                    Debug.LogError($"[XR] No '{BoardPlacementName}' in {ScenePath}; run Add Board Placement first.");
                    return;
                }
                if (!root.TryGetComponent<XRDepthOcclusion>(out var depth)) depth = root.AddComponent<XRDepthOcclusion>();
                var depthSettings = new SerializedObject(depth);
                depthSettings.FindProperty("occlusion").objectReferenceValue = occlusion;
                depthSettings.FindProperty("shaderOcclusion").objectReferenceValue = shaderOcclusion;
                depthSettings.FindProperty("origin").objectReferenceValue = origin;
                depthSettings.FindProperty("occludedLit").objectReferenceValue = lit;
                depthSettings.FindProperty("occludedText").objectReferenceValue = text;
                depthSettings.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[XR] Depth occlusion wired in {ScenePath}: hard occlusion from environment depth.");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        const string OccludedFadePath = GraphicsFolder + "/XROccludedFade.mat";

        /// <summary>
        /// XR6: the tray and the hands (XR-PRD 4). Makes the occluded fade material the grab ghost and the steam are made
        /// from and hands it to the scene's <see cref="XRDepthOcclusion"/>, puts the XRI grab input on the game. Running it again
        /// only re-wires.
        /// </summary>
        [MenuItem("Window/TrainSudoku/XR/Add Tray and Grab to XR Scene")]
        static void AddTrayAndGrab()
        {
            var fade = EnsureShaderMaterial(OccludedFadePath, "TrainSudoku/XR/Occluded Fade");
            if (fade == null) return;

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var depth = roots.Select(go => go.GetComponentInChildren<XRDepthOcclusion>(true)).FirstOrDefault(d => d != null);
                var game = FindGame(roots);
                if (depth == null || game == null)
                {
                    Debug.LogError($"[XR] {ScenePath} needs its game and depth occlusion; run Add Game and Add Depth Occlusion first.");
                    return;
                }

                var depthSettings = new SerializedObject(depth);
                depthSettings.FindProperty("occludedFade").objectReferenceValue = fade;
                depthSettings.ApplyModifiedPropertiesWithoutUndo();

                if (!game.TryGetComponent<XRIGrabInput>(out var input)) input = game.gameObject.AddComponent<XRIGrabInput>();
                var gameSettings = new SerializedObject(game);
                gameSettings.FindProperty("grabInput").objectReferenceValue = input;
                gameSettings.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[XR] Tray and grab wired in {ScenePath}: the game waits for the player's hands.");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        const string UiFolder = DataFolder + "/Ui";
        const string ThemePath = UiFolder + "/XRRuntimeTheme.tss";
        const string SignboardPanelPath = UiFolder + "/XRSignboardPanel.asset";
        const string SignageAssetsPath = DataFolder + "/XRSignageAssets.asset";
        const string HeadingFontPath = "Assets/02.Graphics/Fonts/SDF/BarlowCondensed-SemiBold SDF.asset";
        const string BodyFontPath = "Assets/02.Graphics/Fonts/SDF/Barlow-Medium SDF.asset";
        const string MarkPath = "Assets/02.Graphics/Ui/Logo/tsugi-mark.png";
        const string UiInputName = "UI Input";

        /// <summary>
        /// XR7: the flow on the platform (XR-PRD 6). Makes the signboard's world-space panel settings and theme and the
        /// <see cref="XRSignageAssets"/> (the phone's Barlow faces and tsugi mark, used in place), makes sure the game is in
        /// the scene and hands it those, and adds what XRI's UI Toolkit support needs: a Panel Input Configuration that
        /// takes no input from an EventSystem, an XR UI Toolkit Manager, and UI interaction on every near-far and poke
        /// interactor in the rig. Running it again only re-wires.
        /// </summary>
        [MenuItem("Window/TrainSudoku/XR/Add Flow to XR Scene")]
        static void AddFlow()
        {
            var signage = EnsureSignageAssets();
            if (signage == null) return;
            AddGame();

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var game = FindGame(roots);
                if (game == null)
                {
                    Debug.LogError($"[XR] No game in {ScenePath}.");
                    return;
                }

                var serialized = new SerializedObject(game);
                serialized.FindProperty("signage").objectReferenceValue = signage;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var input = roots.FirstOrDefault(go => go.name == UiInputName);
                if (input == null)
                {
                    input = new GameObject(UiInputName);
                    SceneManager.MoveGameObjectToScene(input, scene);
                }

                if (!input.TryGetComponent<UnityEngine.UIElements.PanelInputConfiguration>(out var configuration))
                    configuration = input.AddComponent<UnityEngine.UIElements.PanelInputConfiguration>();
                // No EventSystem redirection: the interactors feed the world-space panels themselves (XRI's UI Toolkit notes).
                configuration.panelInputRedirection = UnityEngine.UIElements.PanelInputConfiguration.PanelInputRedirection.Never;
                configuration.processWorldSpaceInput = true;
                if (!input.TryGetComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIToolkitManager>(out _))
                    input.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIToolkitManager>();

                var switched = 0;
                foreach (var nearFar in roots.SelectMany(go => go.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true)))
                {
                    if (nearFar.enableUIInteraction) continue;
                    nearFar.enableUIInteraction = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(nearFar);
                    switched++;
                }

                foreach (var poke in roots.SelectMany(go => go.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRPokeInteractor>(true)))
                {
                    if (poke.enableUIInteraction) continue;
                    poke.enableUIInteraction = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(poke);
                    switched++;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[XR] Flow wired in {ScenePath}: signboard panel and UI Toolkit input; UI interaction switched on for {switched} interactor(s).");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// The signboard's hooks: a world-space panel settings asset whose colliders match the document's box with depth
        /// (poke needs something to travel through), at 100 UI pixels to a unit, on XR's own copy of the default runtime
        /// theme; and the <see cref="XRSignageAssets"/> pointing at it and at the phone's fonts and mark.
        /// </summary>
        static XRSignageAssets EnsureSignageAssets()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets/03.Data", "XR");
            if (!AssetDatabase.IsValidFolder(UiFolder)) AssetDatabase.CreateFolder(DataFolder, "Ui");

            var theme = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.ThemeStyleSheet>(ThemePath);
            if (theme == null)
            {
                File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath);
                theme = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.ThemeStyleSheet>(ThemePath);
            }

            var panel = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(SignboardPanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
                AssetDatabase.CreateAsset(panel, SignboardPanelPath);
            }

            panel.renderMode = UnityEngine.UIElements.PanelRenderMode.WorldSpace;
            panel.themeStyleSheet = theme;
            var panelSettings = new SerializedObject(panel);
            // ColliderUpdateMode is internal to UI Toolkit; 0 is MatchBoundingBox (then Keep, then MatchDocumentRect).
            panelSettings.FindProperty("m_ColliderUpdateMode").intValue = 0;
            panelSettings.FindProperty("m_ColliderIsTrigger").boolValue = false;
            panelSettings.FindProperty("m_PixelsPerUnit").floatValue = 100f;
            panelSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);

            var signage = AssetDatabase.LoadAssetAtPath<XRSignageAssets>(SignageAssetsPath);
            if (signage == null)
            {
                signage = ScriptableObject.CreateInstance<XRSignageAssets>();
                AssetDatabase.CreateAsset(signage, SignageAssetsPath);
            }

            var fields = new SerializedObject(signage);
            fields.FindProperty("panelSettings").objectReferenceValue = panel;
            fields.FindProperty("headingFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(HeadingFontPath);
            fields.FindProperty("bodyFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(BodyFontPath);
            fields.FindProperty("mark").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(MarkPath);
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(signage);
            AssetDatabase.SaveAssets();
            return signage;
        }

        /// <summary>A material on one of the XR shaders, saved as an asset so the shader and its variants ship.</summary>
        static Material EnsureShaderMaterial(string path, string shaderName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[XR] Shader '{shaderName}' not found.");
                return null;
            }

            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        const string ShadowCatcherMaterialPath = GraphicsFolder + "/XRShadowCatcher.mat";

        /// <summary>The board's shadow-catcher material (5.6), on the XR-owned shader, saved so the shader ships.</summary>
        static Material EnsureShadowCatcherMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(ShadowCatcherMaterialPath);
            if (existing != null) return existing;
            var shader = Shader.Find("TrainSudoku/XR/Shadow Catcher");
            if (shader == null)
            {
                Debug.LogError("[XR] Shader 'TrainSudoku/XR/Shadow Catcher' not found; no shadow catcher.");
                return null;
            }

            var material = new Material(shader) { name = "XRShadowCatcher" };
            material.SetColor("_ShadowColor", new Color(0f, 0f, 0f, 0.45f));
            AssetDatabase.CreateAsset(material, ShadowCatcherMaterialPath);
            return material;
        }

        static void AssignShadowCatcher(Material material)
        {
            var boardAssets = AssetDatabase.LoadAssetAtPath<XRBoardAssets>(BoardAssetsPath);
            if (boardAssets == null || material == null) return;
            var serialized = new SerializedObject(boardAssets);
            serialized.FindProperty("shadowCatcherMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(boardAssets);
            AssetDatabase.SaveAssets();
        }

        static void EnableOpenXRFeatures(params string[] featureTypeNames)
        {
            var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null) return;
            foreach (var feature in settings.GetFeatures())
            {
                if (!featureTypeNames.Contains(feature.GetType().Name) || feature.enabled) continue;
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
                Debug.Log($"[XR] Enabled OpenXR feature {feature.GetType().Name} on Android.");
            }
            EditorUtility.SetDirty(settings);
        }

        /// <summary>
        /// A transparent URP Unlit material saved as an asset, so its shader variant ships in the build; a material made
        /// at runtime would ask for a variant that stripping may already have removed.
        /// </summary>
        static Material EnsureTransparentMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder(GraphicsFolder)) AssetDatabase.CreateFolder("Assets/02.Graphics", "XR");

            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>A detected surface: its mesh and collider kept up to date by AR Foundation, drawn only while placing.</summary>
        static GameObject EnsurePlanePrefab(Material material)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlanePrefabPath);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/04.Prefabs", "XR");

            var go = new GameObject("XR Plane", typeof(ARPlane), typeof(MeshFilter), typeof(MeshCollider), typeof(MeshRenderer), typeof(ARPlaneMeshVisualizer));
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PlanePrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject FindPrefab(string name) =>
            AssetDatabase.FindAssets(name + " t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path) == name)
                .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
                .FirstOrDefault();
    }
}
