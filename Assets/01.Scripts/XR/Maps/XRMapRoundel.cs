using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.XR
{
    /// <summary>What a roundel shows: its disc and rim, and on top either a label or a padlock.</summary>
    internal readonly struct RoundelLook
    {
        public readonly Material Fill;
        public readonly Material Rim;
        public readonly string Label;
        public readonly Color LabelColour;

        /// <summary>A padlock on top instead of the label, in this material. Null for none.</summary>
        public readonly Material Padlock;

        public RoundelLook(Material fill, Material rim, string label, Color labelColour, Material padlock = null)
        {
            Fill = fill;
            Rim = rim;
            Label = label;
            LabelColour = labelColour;
            Padlock = padlock;
        }
    }

    /// <summary>
    /// A raised roundel on the platform map (XR-PRD 6.2): a line's on the network, a station's on the line map. It is
    /// chosen the two ways a piece is grabbed (X16): a fingertip pressing it from above, or a hand or controller ray with
    /// a pinch, grip or trigger. A closed one answers with a shake and does nothing.
    /// </summary>
    /// <remarks>
    /// The rays hit a volume of its own sitting on the roundel's top face, a little wider than the disc. The fingertip is
    /// read straight off the hand (<see cref="XRTouchPoints"/>) rather than through XRI's poke, which never pressed a
    /// roundel on the headset (the XR7 headset check): coming down from above onto the face presses it.
    /// </remarks>
    public sealed class XRMapRoundel : MonoBehaviour
    {
        /// <summary>How far the roundel stands proud of the card, in cells: 7 mm at the 6 cm cell.</summary>
        public const float Height = 0.12f;

        private const float RimShare = 0.14f;
        private const float LabelShare = 0.36f;
        private const float PadlockShare = 0.5f;

        /// <summary>How far above the top the rays' hit volume reaches, and how far it dips below it, in cells.</summary>
        private const float HitReach = 0.3f;
        private const float HitDip = 0.05f;

        /// <summary>
        /// A fingertip press, in cells above the face. A fingertip anywhere higher than <see cref="ArmHeight"/> (1.5 cm)
        /// is ready, from whatever angle it then comes in; reaching <see cref="PressHeight"/> (6 mm) over the roundel
        /// presses it, once, until it rises again. One that comes down to that height off the roundel must lift before it
        /// can press, so a finger sliding across the card does not. Within <see cref="NearHeight"/> (5 cm) it lifts the
        /// roundel the way a ray's hover does.
        /// </summary>
        private const float ArmHeight = 0.25f;
        private const float PressHeight = 0.1f;
        private const float NearHeight = 0.8f;

        /// <summary>A fingertip presses within this share of the disc's width: hand tracking is good to about a centimetre.</summary>
        private const float TouchWidthShare = 1.4f;

        /// <summary>The volume is wider than the disc, so a ray at its rim still counts.</summary>
        private const float HitWidthShare = 1.2f;

        private const float HoverScale = 1.12f;
        private const float HoverSeconds = 0.12f;
        private const float PopScaleTo = 1.25f;
        private const float PopSeconds = 0.18f;
        private const float ShakeDistance = 0.08f;
        private const float ShakeSeconds = 0.3f;

        /// <summary>A hand's pinch is both a select and a UI press; this keeps the pair to one press.</summary>
        private const float PressCooldown = 0.35f;

        private readonly List<NearFarInteractor> _hoveringRays = new List<NearFarInteractor>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Transform _pop;
        private Transform _body;
        private XRSimpleInteractable _interactable;
        private bool _selectable;
        private Action _pressed;
        private float _lastPress = -10f;
        private float _hover;

        /// <summary>A fingertip presses this close to the roundel's centre, in cells.</summary>
        private float _reach;

        /// <summary>Which fingertips are ready to press: seen above <see cref="ArmHeight"/> since they last came down.</summary>
        private readonly Dictionary<int, bool> _armed = new Dictionary<int, bool>();
        private readonly List<int> _gone = new List<int>();
        private Coroutine _shake;

        /// <param name="at">Where on the card, as (x, z) in cells.</param>
        /// <param name="baseHeight">The card's printed surface the roundel stands on.</param>
        /// <param name="selectable">False for a closed line or station: a press shakes it and nothing else.</param>
        internal static XRMapRoundel Create(Transform parent, string name, Vector2 at, float baseHeight, float diameter,
            RoundelLook look, bool selectable, Action pressed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(at.x, baseHeight, at.y);

            var roundel = go.AddComponent<XRMapRoundel>();
            roundel._selectable = selectable;
            roundel._pressed = pressed;
            roundel._pop = Child("Pop", go.transform);
            roundel._body = Child("Body", roundel._pop);
            roundel.BuildLook(diameter, look);
            roundel.BuildHitVolume(diameter);
            return roundel;
        }

        private static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private void BuildLook(float diameter, RoundelLook look)
        {
            // A cylinder primitive is two units tall and one across.
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            Kill(disc.GetComponent<Collider>());
            disc.transform.SetParent(_body, false);
            disc.transform.localPosition = new Vector3(0f, Height / 2f, 0f);
            disc.transform.localScale = new Vector3(diameter, Height / 2f, diameter);
            disc.GetComponent<MeshRenderer>().sharedMaterial = look.Fill;

            var radius = diameter / 2f;
            Print("Rim", new XRMapMesh(Height + 0.002f).Ring(Vector2.zero, radius * (1f - RimShare * 2f), radius), look.Rim);

            if (look.Padlock != null)
            {
                Print("Padlock", new XRMapMesh(Height + 0.004f).Padlock(Vector2.zero, diameter * PadlockShare), look.Padlock);
            }
            else if (!string.IsNullOrEmpty(look.Label))
            {
                var label = XRPlatformMap.FlatText(_body, look.Label, diameter * LabelShare, look.LabelColour, TextAnchor.MiddleCenter);
                label.transform.localPosition = new Vector3(0f, Height + 0.004f, 0f);
            }
        }

        private void Print(string name, XRMapMesh shape, Material material)
        {
            var mesh = shape.ToMesh(name);
            _meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(_body, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void BuildHitVolume(float diameter)
        {
            var hit = new GameObject("Hit");
            hit.transform.SetParent(transform, false);
            hit.transform.localPosition = new Vector3(0f, Height, 0f);

            // Collider first: the interactable collects its colliders when it wakes.
            var volume = hit.AddComponent<BoxCollider>();
            volume.size = new Vector3(diameter * HitWidthShare, HitReach + HitDip, diameter * HitWidthShare);
            volume.center = new Vector3(0f, (HitReach - HitDip) / 2f, 0f);
            _reach = diameter * TouchWidthShare / 2f;

            // No poke filter: the fingertip is read in Touch, and a poke interactor left hovering a roundel as the map
            // tore it down was what threw inside XRI.
            _interactable = hit.AddComponent<XRSimpleInteractable>();
            _interactable.selectEntered.AddListener(_ => Press());
            _interactable.hoverEntered.AddListener(OnHoverEntered);
            _interactable.hoverExited.AddListener(OnHoverExited);
        }

        /// <summary>
        /// Ends every hover and selection on the roundel while its collider still exists. The map calls it before tearing
        /// the roundel down: XRI otherwise ends them a frame late, on a destroyed collider, and throws.
        /// </summary>
        internal void Release()
        {
            if (_interactable == null) return;
            var manager = _interactable.interactionManager;
            if (manager == null) return;
            manager.CancelInteractableSelection((IXRSelectInteractable)_interactable);
            manager.CancelInteractableHover((IXRHoverInteractable)_interactable);
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (args.interactorObject is NearFarInteractor ray && !_hoveringRays.Contains(ray)) _hoveringRays.Add(ray);
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (args.interactorObject is NearFarInteractor ray) _hoveringRays.Remove(ray);
        }

        private void Update()
        {
            // A controller's trigger selects UI (4.5), and a roundel is platform UI: a trigger pulled while its ray is on
            // the roundel presses it, as the grip or a hand's pinch does through the selection.
            foreach (var ray in _hoveringRays)
                if (ray != null && ray.isActiveAndEnabled && ray.uiPressInput != null && ray.uiPressInput.ReadWasPerformedThisFrame())
                {
                    Press();
                    break;
                }

            var near = Touch();
            var hovered = near || (_interactable != null && _interactable.isHovered);
            _hover = Mathf.MoveTowards(_hover, hovered ? 1f : 0f, Time.unscaledDeltaTime / HoverSeconds);
            var eased = _hover * _hover * (3f - 2f * _hover);
            var across = Mathf.Lerp(1f, HoverScale, eased);
            _body.localScale = new Vector3(across, 1f, across);
        }

        /// <summary>Reads the fingertips over the roundel and presses on one coming down onto its face. True while one is near.</summary>
        private bool Touch()
        {
            var near = false;
            var tips = XRTouchPoints.Fingertips;
            foreach (var tip in tips)
            {
                var local = transform.InverseTransformPoint(tip.Position);
                var height = local.y - Height;
                var inside = local.x * local.x + local.z * local.z <= _reach * _reach;
                if (inside && height < NearHeight) near = true;

                if (height > ArmHeight)
                {
                    _armed[tip.Id] = true;
                }
                else if (height <= PressHeight && _armed.TryGetValue(tip.Id, out var armed) && armed)
                {
                    // Down at the face: over this roundel it presses; anywhere else it has touched down, and must lift again.
                    _armed[tip.Id] = false;
                    if (inside) Press();
                }
            }

            // Forget fingertips that went away (a hand out of view), so one coming back starts unready.
            _gone.Clear();
            foreach (var id in _armed.Keys)
            {
                var present = false;
                foreach (var tip in tips)
                    if (tip.Id == id)
                    {
                        present = true;
                        break;
                    }

                if (!present) _gone.Add(id);
            }

            foreach (var id in _gone) _armed.Remove(id);
            return near;
        }

        private void Press()
        {
            if (Time.unscaledTime - _lastPress < PressCooldown) return;
            _lastPress = Time.unscaledTime;

            if (!_selectable)
            {
                if (_shake != null) StopCoroutine(_shake);
                _shake = StartCoroutine(Shake());
                return;
            }

            PopScale.Play(_pop.gameObject, PopScaleTo, PopSeconds);
            _pressed?.Invoke();
        }

        private IEnumerator Shake()
        {
            for (var t = 0f; t < ShakeSeconds; t += Time.unscaledDeltaTime)
            {
                var fade = 1f - t / ShakeSeconds;
                _pop.localPosition = new Vector3(Mathf.Sin(t * 60f) * ShakeDistance * fade, 0f, 0f);
                yield return null;
            }

            _pop.localPosition = Vector3.zero;
            _shake = null;
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes)
                Kill(mesh);
            _meshes.Clear();
        }

        /// <summary>Destroy in play mode, DestroyImmediate in edit mode, so this can be built from an editor probe.</summary>
        private static void Kill(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
