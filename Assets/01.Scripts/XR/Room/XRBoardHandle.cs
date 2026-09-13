using System.Collections.Generic;
using TrainSudoku.XR.Rules;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.XR
{
    /// <summary>
    /// What moves the board (XR-PRD 5.3, revised after the XR6 headset check). At rest it is a short knob at the near
    /// corner opposite the tray, taken by a close pinch only — never a ray, a poke or the gaze — so laying track does not
    /// catch it. Pinched, it grows into a bar along the near edge that the other hand can take as well; the board only
    /// follows once the hand has carried it 3 cm or twisted it 12 degrees, so a stray pinch does nothing. Then drag to
    /// move the board in any direction, height included; twist the wrist to turn it, or hold the bar with both hands and
    /// steer. Let go and <see cref="XRBoardPlacement"/> settles the board onto a surface it is close to and re-anchors it.
    /// </summary>
    /// <remarks>
    /// It is a simple interactable: XRI reports the grab and moves nothing. A grab interactable would detach the handle
    /// from the board for the length of the grab, which left it behind in the air while the board moved. The board turns
    /// about the hands, so the bar stays in them.
    /// </remarks>
    public sealed class XRBoardHandle : MonoBehaviour
    {
        /// <summary>The knob at rest, in cells: its length and thickness, how far in front of the near edge and how high.</summary>
        private const float KnobLengthCells = 1.2f;
        private const float ThicknessCells = 0.35f;
        private const float OffsetCells = 0.45f;
        private const float HeightCells = 0.25f;

        /// <summary>
        /// How far from the middle of the near edge the knob sits, in cells: just inside the near corner of an 8x8
        /// platform and a little outside a 6x6 one's, so it keeps its place from level to level (X17).
        /// </summary>
        private const float CornerCells = 4.4f;

        /// <summary>While held the bar spans the near edge from corner to corner.</summary>
        private const float BarLengthCells = 2f * CornerCells + KnobLengthCells;

        /// <summary>How much larger than the handle its grab volume is on every side, so a pinch need not be exact.</summary>
        private const float GrabPaddingCells = 0.25f;

        private const float ExpandSeconds = 0.15f;

        /// <summary>How far a pinched handle must be carried, in metres, or turned, in degrees, before the board follows.</summary>
        private const float DeadZoneMetres = 0.03f;
        private const float DeadZoneDegrees = 12f;

        /// <summary>
        /// Degrees the board turns for each degree the wrist twists. A wrist only twists so far comfortably, so the board
        /// turns a little further than the hand does.
        /// </summary>
        private const float TwistGain = 1.5f;

        private readonly List<IXRSelectInteractor> _holders = new List<IXRSelectInteractor>(2);
        private XRBoardPlacement _placement;
        private XRSimpleInteractable _interactable;
        private MeshRenderer _bar;
        private BoxCollider _volume;
        private Pose _startBoard;
        private Vector3 _startGrab;
        private Quaternion _startHand;
        private Vector3 _startSpan;
        private bool _available = true;
        private bool _moving;
        private float _expand;

        /// <summary>-1 for the left-hand corner, +1 for the right.</summary>
        private float _side = -1f;

        public bool IsHeld => _holders.Count > 0;

        /// <summary>The player's dominant hand: the knob rests at the near corner on the other side, away from the tray.</summary>
        public Hand DominantHand
        {
            set
            {
                var side = value == Hand.Right ? -1f : 1f;
                if (Mathf.Approximately(side, _side)) return;
                _side = side;
                ApplyShape();
            }
        }

        public static XRBoardHandle Create(Transform boardRoot, XRBoardPlacement placement)
        {
            var go = new GameObject("Board Handle");
            go.transform.SetParent(boardRoot, false);
            go.transform.localPosition = new Vector3(0f, HeightCells, -OffsetCells);

            // A cylinder primitive is two units tall along Y: lay it along X and scale it to the handle's length.
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = "Bar";
            // Immediately, so the interactable added below does not collect this collider as one of its own.
            DestroyImmediate(bar.GetComponent<Collider>());
            bar.transform.SetParent(go.transform, false);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var handle = go.AddComponent<XRBoardHandle>();
            handle._placement = placement;
            handle._bar = bar.GetComponent<MeshRenderer>();
            handle._bar.sharedMaterial = XRBoardMaterials.SignPole;
            handle._volume = go.AddComponent<BoxCollider>();
            XRDirectReach.Apply(go);

            var interactable = go.AddComponent<XRGrabOnlyInteractable>();
            interactable.selectMode = InteractableSelectMode.Multiple;
            interactable.selectEntered.AddListener(handle.OnGrabbed);
            interactable.selectExited.AddListener(handle.OnReleased);
            interactable.hoverEntered.AddListener(_ => handle.Highlight(true));
            interactable.hoverExited.AddListener(_ => handle.Highlight(false));
            handle._interactable = interactable;
            handle.ApplyShape();
            return handle;
        }

        /// <summary>
        /// Shows or hides the handle. It hides while any piece is held, so a hand cannot catch it by accident mid-drop
        /// (5.3); a handle already in a hand stays until it is let go.
        /// </summary>
        public void SetAvailable(bool available)
        {
            if (available == _available || (!available && IsHeld)) return;
            _available = available;
            _interactable.enabled = available;
            _bar.enabled = available;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (_holders.Count >= 2 || _holders.Contains(args.interactorObject)) return;
            _holders.Add(args.interactorObject);
            Rebase();
            Highlight(true);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (!_holders.Remove(args.interactorObject)) return;
            if (_holders.Count > 0)
            {
                // Down to one hand: carry on from where the board is now, with that hand.
                Rebase();
                return;
            }

            // A pinch that never left the dead zone moved nothing, so there is nothing to re-anchor.
            if (_moving) _placement.EndMove();
            _moving = false;
            Highlight(false);
        }

        /// <summary>Takes the board's pose and the hands' as they are now as the start of the move.</summary>
        private void Rebase()
        {
            var root = _placement.BoardRoot;
            _startBoard = new Pose(root.position, root.rotation);
            if (_holders.Count == 1)
            {
                _startGrab = GrabPoint(_holders[0]);
                _startHand = _holders[0].transform.rotation;
            }
            else
            {
                var a = GrabPoint(_holders[0]);
                var b = GrabPoint(_holders[1]);
                _startGrab = (a + b) / 2f;
                _startSpan = Vector3.ProjectOnPlane(b - a, Vector3.up);
            }
        }

        private void Update()
        {
            var expanded = IsHeld ? 1f : 0f;
            if (!Mathf.Approximately(_expand, expanded))
            {
                _expand = Mathf.MoveTowards(_expand, expanded, Time.deltaTime / ExpandSeconds);
                ApplyShape();
            }

            if (_holders.Count == 0) return;

            Vector3 grab;
            float yaw;
            if (_holders.Count == 1)
            {
                // One hand: it carries the board, and twisting the wrist turns it.
                grab = GrabPoint(_holders[0]);
                yaw = -Twist(_startHand, _holders[0].transform.rotation) * TwistGain;
            }
            else
            {
                // Two hands: their midpoint carries the board, and the line between them steers it.
                var a = GrabPoint(_holders[0]);
                var b = GrabPoint(_holders[1]);
                grab = (a + b) / 2f;
                yaw = Vector3.SignedAngle(_startSpan, Vector3.ProjectOnPlane(b - a, Vector3.up), Vector3.up);
            }

            if (!_moving)
            {
                if ((grab - _startGrab).magnitude < DeadZoneMetres && Mathf.Abs(yaw) < DeadZoneDegrees) return;
                // Out of the dead zone: the board catches up with the hand and follows it from here.
                _moving = true;
                _placement.BeginMove();
            }

            // Turn about the grab, not the board's origin, so the bar stays where the hands are.
            var turn = Quaternion.Euler(0f, yaw, 0f);
            _placement.MoveTo(new Pose(grab + turn * (_startBoard.position - _startGrab), turn * _startBoard.rotation));
        }

        /// <summary>Between the knob at its corner (at rest) and the bar across the near edge (held).</summary>
        private void ApplyShape()
        {
            if (_bar == null || _volume == null) return;
            var eased = _expand * _expand * (3f - 2f * _expand);
            var centre = Mathf.Lerp(_side * CornerCells, 0f, eased);
            var length = Mathf.Lerp(KnobLengthCells, BarLengthCells, eased);
            _bar.transform.localPosition = new Vector3(centre, 0f, 0f);
            _bar.transform.localScale = new Vector3(ThicknessCells, length / 2f, ThicknessCells);

            var girth = ThicknessCells + GrabPaddingCells * 2f;
            _volume.center = new Vector3(centre, 0f, 0f);
            _volume.size = new Vector3(length + GrabPaddingCells * 2f, girth, girth);
        }

        private Vector3 GrabPoint(IXRSelectInteractor holder) => holder.GetAttachTransform(_interactable).position;

        /// <summary>How far the hand has rolled about its own pointing axis since <paramref name="from"/>, in degrees.</summary>
        private static float Twist(Quaternion from, Quaternion to) =>
            Mathf.DeltaAngle(0f, (Quaternion.Inverse(from) * to).eulerAngles.z);

        private void Highlight(bool on)
        {
            if (_bar != null) _bar.sharedMaterial = on || IsHeld ? XRBoardMaterials.PlatformEdge : XRBoardMaterials.SignPole;
        }
    }
}
