using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace TrainSudoku.VisionOS
{
    /// <summary>
    /// Puts the world-space UI Toolkit panels onto real meshes, because on Vision Pro they do not render in world
    /// space at all.
    /// </summary>
    /// <remarks>
    /// <b>The finding this exists for</b> (2026-09-23, on the headset). The signboard's <b>posts stood correctly on
    /// the platform while its card followed the head</b> — a flat overlay pinned to the view, at the edge of vision.
    /// The posts are ordinary meshes; the card is a world-space <c>UIDocument</c>. So world-space UI Toolkit is not
    /// rendered in world space here, which is the risk `Docs/XR-PRD.md` section 13 left open ("World-space UI Toolkit
    /// on Vision Pro Metal mode is unverified") answered with a no.
    ///
    /// It also explains, in order, everything that came before it: the card "shaking and vanishing" was a head-locked
    /// overlay inside a reprojected frame; the card going "grey" was an opaque plate put at the sign's true position
    /// while the real card stayed stuck to the view; and "no UI at all" was that plate made invisible. Two fixes were
    /// built on a wrong diagnosis before the screenshot showed the posts and the card in different places.
    ///
    /// <b>What this does instead.</b> Each world-space document is given its own copy of its
    /// <see cref="PanelSettings"/>, pointed at a <see cref="RenderTexture"/>, and a quad is hung at the document's
    /// own transform showing that texture. The panel is then an ordinary mesh: stereo-correct, depth-writing, and
    /// reprojected like everything else — so the shimmer question is settled by the same change, with no depth proxy.
    ///
    /// <b>Nothing above it changes.</b> The document still lays out at the same pixel size, so
    /// <c>XRPanelTouch</c>'s fingertip mapping (bottom-centre pivot, 100 px to a unit) is untouched, and
    /// <c>XRSignboard</c> and <c>XRWristMenu</c> keep building their views exactly as they do on the Quest. No Quest
    /// file is edited (VisionOS-PRD 2).
    ///
    /// <b>The quad carries both faces, with mirrored UVs on one of them</b>, so the card reads correctly whichever
    /// side the player stands on — the sign turns to face them from any edge of the platform, and guessing the facing
    /// from a local axis is what went wrong twice already.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class VisionOSPanelSurface : MonoBehaviour
    {
        const string k_Tag = "[AVP panel]";
        const string k_SurfaceName = "VisionOS Panel Surface";

        /// <summary>Loaded by name from a <c>Resources</c> folder; <c>VisionOSFoundationSetup</c> creates it.</summary>
        public const string MaterialResource = "VisionOSPanelSurface";

        const float k_ScanInterval = 0.5f;

        /// <summary>
        /// UI pixels to a world unit. The project's own convention, not a readable property: <c>XRSignboard</c>
        /// declares <c>PixelsPerUnit = 100</c> and hands the same number to <c>XRPanelTouch</c>, which is what maps
        /// fingertips into card space. <c>PanelSettings</c> exposes no runtime accessor for it.
        /// </summary>
        const float k_PixelsPerUnit = 100f;

        /// <summary>
        /// The panel texture is bound <b>by name</b>, never through <see cref="Material.mainTexture"/>. That property
        /// writes whichever property carries the <c>[MainTexture]</c> attribute and falls back to <c>_MainTex</c> when
        /// none does — a name this shader does not declare. The write then lands nowhere, <c>_BaseMap</c> keeps its
        /// <c>"white"</c> default, and because that default is opaque the alpha clip discards nothing: the card comes
        /// out as a <b>solid white rectangle</b>, which is what the headset showed on 2026-09-23. The shader now
        /// carries the attribute as well, so both routes work; this one does not depend on it.
        /// </summary>
        static readonly int k_BaseMap = Shader.PropertyToID("_BaseMap");

        static Material s_Template;
        static bool s_TemplateMissing;
        readonly HashSet<UIDocument> m_Converted = new HashSet<UIDocument>();
        readonly List<Surface> m_Surfaces = new List<Surface>();
        float m_NextScan;

        /// <summary>One converted document: the quad standing in for it, and the root whose visibility it follows.</summary>
        sealed class Surface
        {
            public UIDocument Document;
            public MeshRenderer Renderer;
            public RenderTexture Texture;
            public string Name;
            public float ProbeAt;
            public bool Probed;
        }

#if UNITY_VISIONOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<VisionOSPanelSurface>() != null) return;
            var host = new GameObject(nameof(VisionOSPanelSurface));
            DontDestroyOnLoad(host);
            host.AddComponent<VisionOSPanelSurface>();
        }
#endif

        /// <summary>
        /// Panels appear as the game builds them — the sign once the board is placed, the wrist menu on demand — so
        /// they are looked for on a slow timer rather than once.
        /// </summary>
        void LateUpdate()
        {
            FollowVisibility();

            if (Time.unscaledTime < m_NextScan || s_TemplateMissing) return;
            m_NextScan = Time.unscaledTime + k_ScanInterval;

            foreach (var document in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (document == null || m_Converted.Contains(document)) continue;
                if (document.panelSettings == null) continue;
                Convert(document);
            }
        }

        /// <summary>
        /// Switches each surface off while its document is hiding, and cleans up after documents that have gone.
        /// </summary>
        /// <remarks>
        /// <b>The two panels hide themselves in different ways, and only one of them takes the quad with it.</b>
        /// <c>XRSignboard.Hide</c> deactivates its GameObject, so the quad — a child of the document's transform —
        /// goes dark by itself. <c>XRWristMenu.SetVisible</c> instead sets <c>root.style.visibility</c> and
        /// deliberately leaves the GameObject on (a document switched off loses what was built into it). The quad is
        /// an ordinary <see cref="MeshRenderer"/> and knows nothing about that, so the hidden wrist roundel kept
        /// drawing: the <b>"flying white quad"</b> of the 2026-09-23 headset check, which was the pause roundel the
        /// game believed it had hidden. Clearing the panel to transparent would probably cover it — every cleared
        /// pixel fails the alpha clip — but that leans on UI Toolkit still clearing a panel it is drawing nothing
        /// into, and a stale texture would freeze a card in the room. Reading the visibility is the cheaper promise.
        /// </remarks>
        void FollowVisibility()
        {
            for (var i = m_Surfaces.Count - 1; i >= 0; i--)
            {
                var surface = m_Surfaces[i];
                if (surface.Document == null || surface.Renderer == null)
                {
                    if (surface.Texture != null) surface.Texture.Release();
                    m_Surfaces.RemoveAt(i);
                    continue;
                }

                var root = surface.Document.rootVisualElement;
                var shown = surface.Document.isActiveAndEnabled && root != null &&
                    root.resolvedStyle.visibility == Visibility.Visible &&
                    root.resolvedStyle.display != DisplayStyle.None;
                if (surface.Renderer.enabled != shown) surface.Renderer.enabled = shown;
                if (shown && !surface.Probed && Time.unscaledTime >= surface.ProbeAt) Probe(surface);
            }
        }

        /// <summary>
        /// Reads the panel's texture back once, a second after it was built, and says whether anything was drawn into
        /// it.
        /// </summary>
        /// <remarks>
        /// <b>Why this is worth a stall.</b> The two ways this component fails look identical from inside the
        /// headset — an unbound texture and an empty one both end in "the card is not there" — and each wrong guess
        /// costs a full export, Xcode compile, install and play-through. The readback separates them outright: alpha
        /// in the texture means the panel is rendering and any remaining fault is the quad, the material or the
        /// placement; no alpha means the panel never drew and nothing about the mesh side matters yet. It runs once
        /// per panel, on a texture that is already resident, so the cost is a single sync point at startup.
        /// </remarks>
        static void Probe(Surface surface)
        {
            surface.Probed = true;
            var texture = surface.Texture;
            if (texture == null) return;

            var previous = RenderTexture.active;
            var sample = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture;
                sample.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0, false);
                sample.Apply(false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            // Every sixteenth pixel: enough to tell drawn from empty without walking half a million of them.
            const int step = 16;
            var pixels = sample.GetPixels32();
            var drawn = 0;
            var sampled = 0;
            var peak = 0;
            for (var i = 0; i < pixels.Length; i += step)
            {
                sampled++;
                int alpha = pixels[i].a;
                if (alpha > 0) drawn++;
                if (alpha > peak) peak = alpha;
            }

            Destroy(sample);
            Debug.Log($"{k_Tag} {surface.Name}: texture {texture.width}x{texture.height}, {drawn}/{sampled} sampled " +
                $"pixels drawn, peak alpha {peak}. " + (drawn > 0
                    ? "The panel is rendering into it; anything still wrong is on the mesh side."
                    : "EMPTY — the panel is not rendering into its texture, so the mesh side is not the fault."));
        }

        void Convert(UIDocument document)
        {
            if (s_Template == null) s_Template = Resources.Load<Material>(MaterialResource);
            if (s_Template == null)
            {
                s_TemplateMissing = true;
                Debug.LogError($"{k_Tag} Resources/{MaterialResource} is missing; run VisionOSFoundationSetup. " +
                    "Panels will stay stuck to the head.");
                return;
            }

            // Captured before the panel is switched off world space, which is what defines these.
            var pixels = document.worldSpaceSize;
            const float perUnit = k_PixelsPerUnit;
            if (pixels.x < 1f || pixels.y < 1f)
            {
                m_Converted.Add(document);   // not a world-space panel; leave it alone
                return;
            }

            // 24 bits of depth, not 0: that is what carries the stencil, and UI Toolkit clips with the stencil buffer.
            // Without one, anything the card masks — a rounded corner, an `overflow: hidden` group — stops being cut.
            var texture = new RenderTexture((int)pixels.x, (int)pixels.y, 24, RenderTextureFormat.ARGB32)
            {
                name = $"{document.name} Panel",
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
            };
            texture.Create();

            // Its own copy: the PanelSettings asset is shared between the signboard and the wrist menu, and one
            // target texture between them would put both panels on the same surface. The asset on disk is untouched.
            var settings = Instantiate(document.panelSettings);
            settings.name = $"{document.panelSettings.name} (VisionOS)";
            // Out of world space, and this is the line the whole fix turns on. The signage asset is authored
            // PanelRenderMode.WorldSpace (XRFoundationSetup), and a panel left in that mode does not honour
            // targetTexture: it draws through UI Toolkit's world-space path instead. Assigning the texture without
            // this stopped the world-space draw and rendered nothing into the texture either — the panel went
            // nowhere at all, which on the headset read as the card simply being absent.
            settings.renderMode = PanelRenderMode.ScreenSpaceOverlay;
            // One UI pixel to one texture pixel. The texture is already worldSpaceSize pixels, so the card lays out
            // at exactly the size XRSignboard built it for and XRPanelTouch's 100-px-to-a-unit mapping is untouched.
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.targetTexture = texture;
            settings.clearColor = true;
            settings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            document.panelSettings = settings;

            var surface = new GameObject(k_SurfaceName);
            surface.transform.SetParent(document.transform, false);
            // The document's pivot is its bottom centre, so the quad's centre sits half its height above the origin.
            var size = pixels / perUnit;
            surface.transform.localPosition = new Vector3(0f, size.y / 2f, 0f);

            surface.AddComponent<MeshFilter>().sharedMesh = BuildQuad(size);
            var renderer = surface.AddComponent<MeshRenderer>();
            var material = new Material(s_Template) { name = $"{document.name} Panel Surface" };
            material.SetTexture(k_BaseMap, texture);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            m_Converted.Add(document);
            m_Surfaces.Add(new Surface
            {
                Document = document,
                Renderer = renderer,
                Texture = texture,
                Name = document.name,
                ProbeAt = Time.unscaledTime + 1f,
            });
            Debug.Log($"{k_Tag} {document.name}: panel moved onto a {pixels.x:F0}x{pixels.y:F0} surface, " +
                $"{size.x:F2}x{size.y:F2} units.");
        }

        /// <summary>
        /// A quad of <paramref name="size"/> centred on its origin, with a face either way: the front (−z) mirrored
        /// in u so the card reads from the player's side, the back (+z) plain so it reads from the other side too.
        /// </summary>
        static Mesh BuildQuad(Vector2 size)
        {
            var x = size.x / 2f;
            var y = size.y / 2f;

            var vertices = new[]
            {
                // front, normal −z
                new Vector3(-x, -y, 0f), new Vector3(x, -y, 0f), new Vector3(-x, y, 0f), new Vector3(x, y, 0f),
                // back, normal +z
                new Vector3(-x, -y, 0f), new Vector3(x, -y, 0f), new Vector3(-x, y, 0f), new Vector3(x, y, 0f),
            };

            var uvs = new[]
            {
                new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f),
            };

            var normals = new[]
            {
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
            };

            // cross(p1 - p0, p2 - p0) points out of the front face (CLAUDE.md).
            var triangles = new[]
            {
                0, 2, 1, 1, 2, 3,   // −z
                4, 5, 6, 5, 7, 6,   // +z
            };

            var mesh = new Mesh { name = "VisionOS Panel Quad" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
