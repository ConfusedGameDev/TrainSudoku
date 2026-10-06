using System;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The board lesson's ghost hands (XR-PRD 7): a translucent pair at the two sides of the opened rail, showing where
    /// the player's own hands go. They wait open above it, and once the player comes near they come down and pinch it,
    /// over and over, which is the call to action.
    /// </summary>
    /// <remarks>
    /// They are the XR Hands sample's hand models, posed in code: their joints follow OpenXR, forward to the fingertips
    /// and up out of the back of the hand, so a finger curls by turning each joint about its own x. Only the thumb and
    /// the index move; the angles were found by bringing the two tips to about a centimetre apart, a pinch of pads.
    ///
    /// They are life-size and stay so, so they hang from nothing under the board root, which scales with the board;
    /// they are placed in the room each frame from the rail's grip points. With no models assigned there is nothing to
    /// show, and the lesson runs on its callouts.
    /// </remarks>
    public sealed class XRGhostHands : MonoBehaviour
    {
        public enum Mode
        {
            Hidden,

            /// <summary>Open, hovering over the rail.</summary>
            Waiting,

            /// <summary>Coming down onto the rail and pinching it, in a loop.</summary>
            Pinching,

            /// <summary>
            /// One hand taking a piece: down onto it, pinch, carry it to its cell, let go, in a loop (<see cref="ShowCarry"/>).
            /// The lesson for a player who has never pinched anything in a headset: watching a hand do it, at the real
            /// size and in the real place, says it better than any line of text.
            /// </summary>
            Carrying,
        }

        /// <summary>How far above the rail a waiting hand's pinch point hovers, in metres, and how far it bobs.</summary>
        private const float Hover = 0.05f;
        private const float Bob = 0.008f;
        private const float BobHertz = 0.5f;

        /// <summary>The fingers point across the rail and down onto it, and each hand turns in a little towards the board.</summary>
        private const float PitchDegrees = 40f;
        private const float YawInDegrees = 20f;

        /// <summary>One pinch, in seconds: down, close, hold, open, up.</summary>
        private const float CycleSeconds = 2.4f;
        private const float DownUntil = 0.25f;
        private const float ClosedFrom = 0.4f;
        private const float ClosedUntil = 0.72f;
        private const float OpenFrom = 0.82f;

        /// <summary>The pinch pose, as a turn of each joint from its rest, in degrees. The thumb's yaw mirrors with the hand.</summary>
        private static readonly (string Bone, Vector3 Turn)[] PinchPose =
        {
            ("IndexProximal", new Vector3(40f, 0f, 0f)),
            ("IndexIntermediate", new Vector3(45f, 0f, 0f)),
            ("IndexDistal", new Vector3(24f, 0f, 0f)),
            ("ThumbMetacarpal", new Vector3(0f, 20f, 0f)),
            ("ThumbProximal", new Vector3(30f, 0f, 0f)),
        };

        private sealed class GhostHand
        {
            public Transform Root;
            public Transform[] Bones;
            public Quaternion[] Rest;
            public Quaternion[] Pinched;
            public Transform IndexTip;
            public Transform ThumbTip;

            /// <summary>-1 for the left hand, +1 for the right: the side of the platform it stands at.</summary>
            public float Side;
        }

        private GhostHand _left;
        private GhostHand _right;
        private Mode _mode = Mode.Hidden;
        private float _since;

        /// <summary>The rail the hands show how to take.</summary>
        public XRBoardHandle Handle { get; set; }

        /// <summary>One carry, in seconds, and its beats as fractions of it: slow on purpose, for a first-time player.</summary>
        private const float CarrySeconds = 4.2f;
        private const float CarryDownUntil = 0.12f;
        private const float CarryClosedAt = 0.24f;
        private const float CarryArrivedAt = 0.62f;
        private const float CarryOpenAt = 0.72f;
        private const float CarryGoneAt = 0.9f;

        /// <summary>How high the carried piece arcs between the tray and its cell, in metres.</summary>
        private const float CarryArc = 0.06f;

        private Func<Vector3> _from;
        private Func<Vector3> _to;
        private Func<Quaternion> _facing;
        private Func<bool> _resting;
        private float _carrySide = 1f;

        /// <summary>
        /// Shows one hand, <paramref name="side"/> −1 the left and +1 the right, taking a piece from <paramref name="from"/>
        /// to <paramref name="to"/> on a loop, turned by <paramref name="facing"/> (the board's rotation). The points are
        /// read every frame, so the tray and the board can move under it. While <paramref name="resting"/> answers true —
        /// the player is holding a piece already — the hand steps aside and the loop starts over when it is put down.
        /// </summary>
        public void ShowCarry(Func<Vector3> from, Func<Vector3> to, Func<Quaternion> facing, float side, Func<bool> resting)
        {
            _from = from;
            _to = to;
            _facing = facing;
            _resting = resting;
            var changed = _mode != Mode.Carrying || side != _carrySide;
            _carrySide = side;
            if (changed)
            {
                _mode = Mode.Carrying;
                _since = Time.unscaledTime;
            }

            gameObject.SetActive(true);
        }

        public static XRGhostHands Create(XRBoardAssets assets)
        {
            var go = new GameObject("Ghost Hands");
            var hands = go.AddComponent<XRGhostHands>();
            if (assets != null)
            {
                hands._left = Build(go.transform, assets.LeftHandModel, -1f);
                hands._right = Build(go.transform, assets.RightHandModel, 1f);
            }

            go.SetActive(false);
            return hands;
        }

        public void Show(Mode mode)
        {
            if (mode == _mode) return;
            _mode = mode;
            _since = Time.unscaledTime;
            gameObject.SetActive(mode != Mode.Hidden);
            // Both hands again after a carry, which shows only one.
            Visible(_left, true);
            Visible(_right, true);
        }

        private static void Visible(GhostHand hand, bool visible)
        {
            if (hand != null && hand.Root.gameObject.activeSelf != visible) hand.Root.gameObject.SetActive(visible);
        }

        private static GhostHand Build(Transform parent, GameObject model, float side)
        {
            if (model == null) return null;
            var root = Instantiate(model, parent, false).transform;
            root.name = side < 0f ? "Left" : "Right";
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = XRBoardMaterials.GhostHand;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                // The hand moves every frame and its bounds are the rest pose's: never cull it by them.
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }

            var hand = new GhostHand
            {
                Root = root,
                Side = side,
                Bones = new Transform[PinchPose.Length],
                Rest = new Quaternion[PinchPose.Length],
                Pinched = new Quaternion[PinchPose.Length],
                IndexTip = Find(root, "IndexTip"),
                ThumbTip = Find(root, "ThumbTip"),
            };

            for (var i = 0; i < PinchPose.Length; i++)
            {
                var bone = Find(root, PinchPose[i].Bone);
                if (bone == null) continue;
                var turn = PinchPose[i].Turn;
                // The models are mirror images: a yaw towards the palm on the left hand is the opposite turn on the right.
                turn.y *= -side;
                turn.z *= -side;
                hand.Bones[i] = bone;
                hand.Rest[i] = bone.localRotation;
                hand.Pinched[i] = bone.localRotation * Quaternion.Euler(turn);
            }

            return hand;
        }

        /// <summary>The joint whose name ends in <paramref name="suffix"/>: the models prefix each with L_ or R_.</summary>
        private static Transform Find(Transform from, string suffix)
        {
            if (from.name.EndsWith(suffix, System.StringComparison.Ordinal)) return from;
            foreach (Transform child in from)
            {
                var found = Find(child, suffix);
                if (found != null) return found;
            }

            return null;
        }

        private void LateUpdate()
        {
            if (_mode == Mode.Carrying)
            {
                Carry();
                return;
            }

            if (_mode == Mode.Hidden || Handle == null) return;
            var elapsed = Time.unscaledTime - _since;
            float pinch, lift;
            if (_mode == Mode.Pinching)
            {
                var t = Mathf.Repeat(elapsed / CycleSeconds, 1f);
                var down = t < OpenFrom ? Mathf.SmoothStep(0f, 1f, t / DownUntil) : 1f - Mathf.SmoothStep(0f, 1f, (t - OpenFrom) / (1f - OpenFrom));
                pinch = t < ClosedUntil ? Mathf.SmoothStep(0f, 1f, (t - DownUntil) / (ClosedFrom - DownUntil)) : 1f - Mathf.SmoothStep(0f, 1f, (t - ClosedUntil) / (OpenFrom - ClosedUntil));
                lift = Hover * (1f - down);
            }
            else
            {
                pinch = 0f;
                lift = Hover + Bob * Mathf.Sin(elapsed * BobHertz * 2f * Mathf.PI);
            }

            Stand(pinch, lift);
        }

        /// <summary>
        /// Stands both hands at the rail's grip points, <paramref name="lift"/> metres above it and pinched by
        /// <paramref name="pinch"/>, 0 open to 1 closed. Public so a tool can look at a pose without running the loop.
        /// </summary>
        public void Stand(float pinch, float lift)
        {
            if (Handle == null) return;
            Handle.GripPoints(out var left, out var right);
            var board = Handle.transform;
            Place(_left, left + board.up * lift, board.rotation, pinch);
            Place(_right, right + board.up * lift, board.rotation, pinch);
        }

        /// <summary>One beat of the carry loop: hover, down, pinch, arc over to the cell, open, lift away, and round again.</summary>
        private void Carry()
        {
            var hand = _carrySide < 0f ? _left : _right;
            Visible(_carrySide < 0f ? _right : _left, false);
            if (hand == null || _from == null || _to == null) return;

            if (_resting != null && _resting())
            {
                // The player has a piece in hand: out of the way, and from the top once they let go.
                Visible(hand, false);
                _since = Time.unscaledTime;
                return;
            }

            var t = Mathf.Repeat((Time.unscaledTime - _since) / CarrySeconds, 1f);
            var from = _from();
            var to = _to();
            var facing = _facing != null ? _facing() : Quaternion.identity;
            var up = facing * Vector3.up;

            Vector3 at;
            float pinch;
            if (t < CarryDownUntil)
            {
                at = from + up * (Hover * (1f - Mathf.SmoothStep(0f, 1f, t / CarryDownUntil)));
                pinch = 0f;
            }
            else if (t < CarryClosedAt)
            {
                at = from;
                pinch = Mathf.SmoothStep(0f, 1f, (t - CarryDownUntil) / (CarryClosedAt - CarryDownUntil));
            }
            else if (t < CarryArrivedAt)
            {
                var s = Mathf.SmoothStep(0f, 1f, (t - CarryClosedAt) / (CarryArrivedAt - CarryClosedAt));
                at = Vector3.Lerp(from, to, s) + up * (CarryArc * Mathf.Sin(s * Mathf.PI));
                pinch = 1f;
            }
            else if (t < CarryOpenAt)
            {
                at = to;
                pinch = 1f - Mathf.SmoothStep(0f, 1f, (t - CarryArrivedAt) / (CarryOpenAt - CarryArrivedAt));
            }
            else
            {
                at = to + up * (Hover * Mathf.SmoothStep(0f, 1f, (t - CarryOpenAt) / (CarryGoneAt - CarryOpenAt)));
                pinch = 0f;
            }

            // Gone for the last beat, so the jump back to the tray is never seen.
            Visible(hand, t < CarryGoneAt);
            Place(hand, at, facing, pinch);
        }

        /// <summary>Poses the hand, then stands it so the point between its thumb and index tips is on <paramref name="target"/>.</summary>
        private static void Place(GhostHand hand, Vector3 target, Quaternion board, float pinch)
        {
            if (hand == null) return;
            for (var i = 0; i < hand.Bones.Length; i++)
                if (hand.Bones[i] != null)
                    hand.Bones[i].localRotation = Quaternion.Slerp(hand.Rest[i], hand.Pinched[i], pinch);

            // Fingers away from the player and down onto the rail, turned in a little towards the platform.
            hand.Root.rotation = board * Quaternion.Euler(PitchDegrees, -hand.Side * YawInDegrees, 0f);
            var grip = hand.IndexTip != null && hand.ThumbTip != null
                ? (hand.IndexTip.position + hand.ThumbTip.position) / 2f
                : hand.Root.position;
            hand.Root.position += target - grip;
        }
    }
}
