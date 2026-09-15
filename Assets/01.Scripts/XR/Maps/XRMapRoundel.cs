using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    /// A raised roundel on the platform map (XR-PRD 6.2): a line's on the network, a station's on the line map. A
    /// fingertip coming down onto its face presses it. A closed one answers with a shake and does nothing.
    /// </summary>
    /// <remarks>
    /// The fingertip is read straight off the hand (<see cref="XRTouchPoints"/>) rather than through XRI's poke, which
    /// never pressed a roundel on the headset (the XR7 headset check). The XR7 ray, pinch and trigger path went with the
    /// rays after the first XR8 check (<see cref="XRTouchOnly"/>), and with it the interactable and its hit volume.
    /// </remarks>
    public sealed class XRMapRoundel : MonoBehaviour
    {
        /// <summary>How far the roundel stands proud of the card, in cells: 7 mm at the 6 cm cell.</summary>
        public const float Height = 0.12f;

        private const float RimShare = 0.14f;
        private const float LabelShare = 0.36f;
        private const float PadlockShare = 0.5f;

        /// <summary>
        /// A fingertip press, in cells above the face. A fingertip anywhere higher than <see cref="ArmHeight"/> (1.5 cm)
        /// is ready, from whatever angle it then comes in; reaching <see cref="PressHeight"/> (6 mm) over the roundel
        /// presses it, once, until it rises again. One that comes down to that height off the roundel must lift before it
        /// can press, so a finger sliding across the card does not. Within <see cref="NearHeight"/> (5 cm) it lifts the
        /// roundel a little, to show which one it is over.
        /// </summary>
        private const float ArmHeight = 0.25f;
        private const float PressHeight = 0.1f;
        private const float NearHeight = 0.8f;

        /// <summary>A fingertip presses within this share of the disc's width: hand tracking is good to about a centimetre.</summary>
        private const float TouchWidthShare = 1.4f;

        private const float HoverScale = 1.12f;
        private const float HoverSeconds = 0.12f;
        private const float PopScaleTo = 1.25f;
        private const float PopSeconds = 0.18f;
        private const float ShakeDistance = 0.08f;
        private const float ShakeSeconds = 0.3f;

        /// <summary>A fingertip seen coming down twice in quick succession presses once.</summary>
        private const float PressCooldown = 0.35f;

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Transform _pop;
        private Transform _body;
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
            roundel._reach = diameter * TouchWidthShare / 2f;
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

        private void Update()
        {
            var near = Touch();
            _hover = Mathf.MoveTowards(_hover, near ? 1f : 0f, Time.unscaledDeltaTime / HoverSeconds);
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
