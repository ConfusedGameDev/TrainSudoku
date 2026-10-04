using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.VisionOS;

// The platform API is a *class* called VisionOS, inside a namespace that also ends in VisionOS, and our own
// namespace is TrainSudoku.VisionOS. So a bare `VisionOS.Foo` binds to our namespace and fails to compile
// with "the type or namespace name 'Foo' does not exist in the namespace 'TrainSudoku.VisionOS'", which reads
// like a missing assembly reference rather than the name collision it is. The alias settles it in one place;
// the same shadowing is why CLAUDE.md insists on fully qualifying CompilationPipeline and Tools in a bridge
// snippet.
using VisionOSApi = UnityEngine.XR.VisionOS.VisionOS;

namespace TrainSudoku.VisionOS
{
    /// <summary>
    /// Switches plane detection on for the Vision Pro, and reports what visionOS authorised, under
    /// <c>[AVP]</c>. The one runtime file V1 adds.
    /// </summary>
    /// <remarks>
    /// <b>Why this exists.</b> The Quest edition gates plane detection on
    /// <c>XRScenePermission.IsGranted</c>, which asks Android for
    /// <c>com.oculus.permission.USE_SCENE</c> and <b>hard-returns refused on every other platform</b>.
    /// <c>XRBoardPlacement</c> only ever does <c>if (XRScenePermission.IsGranted &amp;&amp; planes != null)
    /// planes.enabled = true</c>, so on visionOS the plane manager would stay off for ever, no surface
    /// would ever be detected, and the board would float at waist height on every launch.
    ///
    /// <c>XRScenePermission</c> is a <c>static class</c> with no interface, and it lives under
    /// <c>Assets/01.Scripts/XR/</c>, which this branch does not edit (VisionOS-PRD 2). Giving it an
    /// <c>IScenePermission</c> seam is a change to shared code and therefore a seam commit on
    /// <c>feat/MetaXR</c> (V-h). V1 deliberately avoids that: it enables the plane manager from the
    /// outside instead, and finds out whether that alone is enough. If the board still refuses to sit
    /// on a table, some other branch reads the flag too, and V2 lands the seam — see VisionOS-PRD 4.1,
    /// finding 3.
    ///
    /// <b>It does not request authorization itself.</b> <c>VisionOSSessionSubsystem</c> collects the
    /// authorizations its enabled providers need and asks for them once, in
    /// <c>RequestAuthorizationIfNeeded</c>. Enabling the plane manager is what puts
    /// <see cref="VisionOSAuthorizationType.WorldSensing"/> on that list, so the prompt follows from
    /// the line below rather than from a call of our own. Asking twice would only risk a second prompt.
    ///
    /// <b>The usage descriptions are what make it possible at all.</b> An empty
    /// <c>worldSensingUsageDescription</c> or <c>handsTrackingUsageDescription</c> in the visionOS
    /// player settings means no <c>Info.plist</c> key, and visionOS refuses the request outright
    /// rather than prompting. <c>VisionOSFoundationSetup</c> writes both.
    ///
    /// This assembly is <c>includePlatforms: ["Editor", "VisionOS"]</c>, because
    /// <c>Unity.XR.VisionOS</c> is. So none of this compiles into a Quest or phone build and it cannot
    /// break one.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class VisionOSWorldSensing : MonoBehaviour
    {
        const string k_Tag = "[AVP]";

        /// <summary>How long to keep reporting authorization before giving up on a silent refusal.</summary>
        const float k_ReportWindowSeconds = 20f;

        static readonly List<ARPlaneManager> k_Planes = new List<ARPlaneManager>();

        /// <summary>
        /// Puts this component into the running scene on the headset, so **no scene asset changes**.
        /// </summary>
        /// <remarks>
        /// V1 runs the Quest's own <c>Assets/Scenes/XR.unity</c>, which is a Quest-owned file this branch
        /// keeps byte-identical (VisionOS-PRD 2). Adding a component to it in the Editor would dirty the
        /// scene and be one careless ⌘S away from breaking that rule, so the component installs itself
        /// instead and the scene never learns it exists.
        ///
        /// <c>UNITY_VISIONOS &amp;&amp; !UNITY_EDITOR</c> is the visionOS package's own idiom, and it is what keeps
        /// this out of the way of Quest work: playing <c>XR.unity</c> in the Editor must behave exactly as
        /// it does on <c>feat/MetaXR</c>. For a deliberate Editor run, use
        /// <c>Window > TrainSudoku > VisionOS > Add World Sensing to Open Scene</c>, which adds it without
        /// saving.
        /// </remarks>
#if UNITY_VISIONOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<VisionOSWorldSensing>() != null) return;
            var host = new GameObject(nameof(VisionOSWorldSensing));
            Object.DontDestroyOnLoad(host);
            host.AddComponent<VisionOSWorldSensing>();
        }
#endif

        ARPlaneManager m_Planes;
        float m_Elapsed;
        VisionOSAuthorizationStatus m_LastWorldSensing = (VisionOSAuthorizationStatus)(-1);
        VisionOSAuthorizationStatus m_LastHandTracking = (VisionOSAuthorizationStatus)(-1);
        bool m_ReportedPlaneCount;

        void OnEnable() => VisionOSApi.AuthorizationChanged += OnAuthorizationChanged;

        void OnDisable() => VisionOSApi.AuthorizationChanged -= OnAuthorizationChanged;

        /// <summary>The event half of the reporting; <see cref="Update"/> polls for what the event can miss.</summary>
        void OnAuthorizationChanged(VisionOSAuthorizationEventArgs args) =>
            Debug.Log($"{k_Tag} Authorization changed: {args.type} is {args.status}.");

        void Start()
        {
            Debug.Log($"{k_Tag} World sensing starting. simulator={VisionOSApi.IsSimulator()}, " +
                $"immersiveSpaceReady={VisionOSApi.IsImmersiveSpaceReady()}");

            // FindObjectsInactive.Include: XRBoardPlacement's own plane manager starts disabled, which is
            // precisely the state this component exists to change, so an active-only search would miss it.
            k_Planes.Clear();
            k_Planes.AddRange(FindObjectsByType<ARPlaneManager>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (k_Planes.Count == 0)
            {
                Debug.LogError($"{k_Tag} No ARPlaneManager in the scene. The board cannot find a surface.");
                return;
            }

            if (k_Planes.Count > 1)
                Debug.LogWarning($"{k_Tag} {k_Planes.Count} ARPlaneManagers found; enabling all of them.");

            m_Planes = k_Planes[0];
            foreach (var planes in k_Planes) planes.enabled = true;
            Debug.Log($"{k_Tag} Plane detection enabled on {k_Planes.Count} manager(s), " +
                "which is what asks visionOS for WorldSensing.");
        }

        void Update()
        {
            if (m_Planes == null) return;

            // Both statuses are polled as well as subscribed to. QueryAuthorizationStatus answers from the
            // session's own cache, and a status settled before this component's OnEnable would raise no
            // event at all, so the event alone can miss the answer entirely.
            Report(VisionOSAuthorizationType.WorldSensing, ref m_LastWorldSensing);
            Report(VisionOSAuthorizationType.HandTracking, ref m_LastHandTracking);

            if (m_ReportedPlaneCount || m_Planes.trackables.count == 0) return;
            m_ReportedPlaneCount = true;
            Debug.Log($"{k_Tag} First surfaces detected: {m_Planes.trackables.count}. " +
                "The placement ghost can settle on a real table.");
        }

        void Report(VisionOSAuthorizationType type, ref VisionOSAuthorizationStatus last)
        {
            var status = VisionOSApi.QueryAuthorizationStatus(type);
            if (status == last) return;
            last = status;
            Debug.Log($"{k_Tag} {type} is {status}.");

            if (status != VisionOSAuthorizationStatus.Denied) return;
            Debug.LogWarning($"{k_Tag} {type} denied. " + (type == VisionOSAuthorizationType.WorldSensing
                ? "No surfaces will be detected, so the board floats at waist height."
                : "Hands will not track, so nothing can be grabbed or pressed."));
        }

        void LateUpdate()
        {
            HidePlanes();

            // A refusal that never prompts looks exactly like a slow prompt, so say so once rather than
            // leaving a silent log to be read as "still waiting".
            if (m_Planes == null || m_Elapsed > k_ReportWindowSeconds) return;
            m_Elapsed += Time.unscaledDeltaTime;
            if (m_Elapsed <= k_ReportWindowSeconds) return;

            if (m_LastWorldSensing == VisionOSAuthorizationStatus.NotDetermined)
                Debug.LogWarning($"{k_Tag} WorldSensing still NotDetermined after " +
                    $"{k_ReportWindowSeconds:F0}s. Check that worldSensingUsageDescription is set: with no " +
                    "Info.plist key visionOS refuses without ever prompting.");
        }

        static readonly List<Renderer> k_PlaneRenderers = new List<Renderer>();

        /// <summary>Never draws a detected surface on this headset.</summary>
        /// <remarks>
        /// <c>XRBoardPlacement.ShowPlanes</c> shows every detected plane while the player is choosing a
        /// surface — the Quest's design, where it tells you which table the ghost can land on. On the first full
        /// playthrough here (2026-09-22) the player saw them as <b>transparent white sheets</b> over the floor,
        /// the table and the walls (<c>XRDetectedPlane.mat</c>, white at 12% alpha) and asked for them gone.
        /// On visionOS the platform never shows its scanned surfaces, and the ghost settling onto a table is
        /// feedback enough, so here they are never drawn at all.
        ///
        /// It runs in <c>LateUpdate</c> because <c>ShowPlanes</c> re-enables them in <c>XRBoardPlacement</c>'s
        /// own <c>Update</c> every frame; the renderer sees whichever of the two wrote last. The plane
        /// <i>colliders</i> are untouched — placement raycasts against them, and falling pieces bounce off them.
        /// </remarks>
        void HidePlanes()
        {
            if (m_Planes == null) return;
            foreach (var plane in m_Planes.trackables)
            {
                k_PlaneRenderers.Clear();
                plane.GetComponentsInChildren(true, k_PlaneRenderers);
                foreach (var renderer in k_PlaneRenderers)
                    if (renderer.enabled) renderer.enabled = false;
            }
        }
    }
}
