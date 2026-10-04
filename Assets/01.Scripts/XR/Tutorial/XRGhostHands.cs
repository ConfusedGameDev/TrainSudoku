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
