using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Fingertip presses on the buttons of a world-space UI Toolkit panel: the signboard's card (XR-PRD 6.2) and the wrist
    /// menu (6.4). A fingertip over the panel, between <see cref="ArmGap"/> and <see cref="ArmDepth"/> in front of it, is
    /// ready; coming to within <see cref="PressGap"/> of its face over a button presses it, once, until it draws back. One
    /// that comes to the face anywhere else, or without having been ready, has touched down, and must draw back before it
    /// can press. Within <see cref="HoverGap"/> the button under it swells a little.
    /// </summary>
    /// <remarks>
    /// XRI's own poke never pressed a UI Toolkit button on the headset (the XR7 headset check), so the panels read the
    /// fingertips themselves (<see cref="XRTouchPoints"/>). The document's pivot must be its bottom centre, on the panel
    /// transform's origin, at <c>pixelsPerUnit</c> UI pixels to a unit, with the player's side of the panel at -z.
    ///
    /// Everything is compared in the root's own space, in UI pixels: a button's rectangle is its world bound brought back
    /// into the root. The first version compared the fingertip with the world bounds as they came and measured its slop
    /// in pixels, and on the headset every press went to the first button listed on the page, and the wrist roundel
    /// answered a fingertip anywhere in its plane (the second XR8 headset check): what a slop far larger than the card
    /// does. So the two spaces are never mixed, and of the buttons in reach the nearest wins, never the first.
    /// </remarks>
    public sealed class XRPanelTouch
    {
        public const float ArmGap = 0.02f;
        public const float ArmDepth = 0.15f;
        public const float PressGap = 0.006f;
        public const float HoverGap = 0.04f;
        private const float HoverScale = 1.06f;

        /// <summary>A second press on the panel within this many seconds is the first one seen again, jittering at the face.</summary>
        public const float PressCooldown = 0.35f;

        private readonly string _name;
        private readonly float _pixelsPerUnit;
        private readonly float _slop;
        private readonly float _armMargin;
        private readonly List<(Button Button, Action Clicked)> _buttons = new List<(Button Button, Action Clicked)>();
        private readonly Dictionary<int, bool> _armedTips = new Dictionary<int, bool>();
        private readonly List<int> _goneTips = new List<int>();
        private Button _hovered;
        private float _lastPress = float.NegativeInfinity;

        /// <param name="name">What the log calls the panel.</param>
        /// <param name="pixelsPerUnit">The panel settings' UI pixels to a unit.</param>
        /// <param name="slop">How far outside a button a fingertip still presses it, in metres: tracking is good to about a centimetre.</param>
        /// <param name="armMargin">How far outside the panel's edge a fingertip still counts as over it, and can be ready, in metres.</param>
        public XRPanelTouch(string name, float pixelsPerUnit, float slop = 0.003f, float armMargin = 0.02f)
        {
            _name = name;
            _pixelsPerUnit = pixelsPerUnit;
            _slop = slop;
            _armMargin = armMargin;
        }

        /// <summary>Fingertips of this hand press nothing here: the hand a panel is worn on must not press it by itself.</summary>
        public InteractorHandedness IgnoredHand { get; set; } = InteractorHandedness.None;

        /// <summary>Forgets the buttons: the panel is being rebuilt.</summary>
        public void Clear()
        {
            if (_hovered != null) _hovered.style.scale = StyleKeyword.Null;
            _hovered = null;
            _buttons.Clear();
        }

        /// <summary>Forgets which fingertips were ready: the panel has just come into view, and must be come at afresh.</summary>
        public void Disarm() => _armedTips.Clear();

        public void Add(Button button, Action clicked) => _buttons.Add((button, clicked));

        /// <summary>Reads the fingertips against the panel. True when one pressed a button, which may have rebuilt it.</summary>
        /// <param name="canPress">False while the panel is settling: fingertips get ready and hover, but reaching the face only touches down.</param>
        public bool Update(Transform panel, UIDocument document, bool canPress = true)
        {
            Button hovered = null;
            var tips = XRTouchPoints.Fingertips;
            var root = document != null ? document.rootVisualElement : null;
            if (panel != null && root != null && _buttons.Count > 0)
            {
                var metres = Mathf.Max(1e-6f, panel.lossyScale.z);
                var size = RootSize(root, document);
                // Metres in the room to UI pixels, through the panel's scale.
                var toPixels = _pixelsPerUnit / metres;
                var slop = _slop * toPixels;
                var margin = _armMargin * toPixels;
                foreach (var tip in tips)
                {
                    if (IgnoredHand != InteractorHandedness.None && tip.Handedness == IgnoredHand) continue;

                    var local = panel.InverseTransformPoint(tip.Position);
                    var gap = -local.z * metres;
                    // The root's own space, in UI pixels: x from its left edge, y down from its top. The pivot is its bottom centre.
                    var point = new Vector2(local.x * _pixelsPerUnit + size.x / 2f, size.y - local.y * _pixelsPerUnit);
                    var over = point.x >= -margin && point.x <= size.x + margin && point.y >= -margin && point.y <= size.y + margin;
                    var button = ButtonAt(root, point, slop);
                    if (button != null && Mathf.Abs(gap) < HoverGap) hovered = button;

                    if (gap > ArmGap)
                    {
                        // Ready only over the panel and near it: not a hand off to one side, or across the room.
                        _armedTips[tip.Id] = over && gap < ArmDepth;
                    }
                    else if (gap <= PressGap && _armedTips.TryGetValue(tip.Id, out var armed) && armed)
                    {
                        _armedTips[tip.Id] = false;
                        if (button == null || !canPress) continue;
                        if (Time.unscaledTime - _lastPress < PressCooldown)
                        {
                            Debug.Log($"[XR touch] {_name}: {button.text} by the {tip.Handedness} fingertip, within the cooldown: ignored");
                            continue;
                        }

                        _lastPress = Time.unscaledTime;
                        Debug.Log($"[XR touch] {_name}: {button.text} by the {tip.Handedness} fingertip");
                        // Pressing may rebuild the panel, and with it the button list: nothing more this frame.
                        Click(button);
                        return true;
                    }
                }
            }

            _goneTips.Clear();
            foreach (var id in _armedTips.Keys)
            {
                var present = false;
                foreach (var tip in tips)
                    if (tip.Id == id)
                    {
                        present = true;
                        break;
                    }

                if (!present) _goneTips.Add(id);
            }

            foreach (var id in _goneTips) _armedTips.Remove(id);

            if (hovered == _hovered) return false;
            if (_hovered != null) _hovered.style.scale = StyleKeyword.Null;
            _hovered = hovered;
            if (_hovered != null) _hovered.style.scale = new Scale(Vector3.one * HoverScale);
            return false;
        }

        /// <summary>The root's laid-out size in UI pixels, or the document's size until it has been laid out.</summary>
        private static Vector2 RootSize(VisualElement root, UIDocument document)
        {
            var layout = root.layout;
            return layout.width > 0f && layout.height > 0f ? layout.size : document.worldSpaceSize;
        }

        /// <summary>The button nearest <paramref name="point"/> of those within <paramref name="slop"/> of it, all in the root's space.</summary>
        private Button ButtonAt(VisualElement root, Vector2 point, float slop)
        {
            Button best = null;
            var bestDistance = float.MaxValue;
            foreach (var (button, _) in _buttons)
            {
                if (button.panel == null || !button.enabledInHierarchy) continue;
                var rect = root.WorldToLocal(button.worldBound);
                // Not laid out yet: a button built this frame has no size, or a NaN one.
                if (!(rect.width > 0f) || !(rect.height > 0f)) continue;

                var dx = Mathf.Max(0f, Mathf.Max(rect.xMin - point.x, point.x - rect.xMax));
                var dy = Mathf.Max(0f, Mathf.Max(rect.yMin - point.y, point.y - rect.yMax));
                if (dx > slop || dy > slop) continue;
                var distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                best = button;
                bestDistance = distance;
            }

            return best;
        }

        private void Click(Button button)
        {
            Action clicked = null;
            foreach (var (candidate, action) in _buttons)
                if (candidate == button)
                {
                    clicked = action;
                    break;
                }

            clicked?.Invoke();
        }
    }
}
