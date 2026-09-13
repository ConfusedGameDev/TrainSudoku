using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The bar along the board's near edge that moves it (XR-PRD 5.3). Grab it and drag to move the board in any
    /// direction, height included; twist the wrist to turn it about the vertical, or hold the bar with both hands and
    /// steer. Let go and <see cref="XRBoardPlacement"/> settles the board onto a surface it is close to and re-anchors it.
    /// </summary>
    /// <remarks>
    /// It is an <see cref="XRSimpleInteractable"/>: XRI reports the grab and moves nothing. A grab interactable would
    /// detach the bar from the board for the length of the grab, which left the bar behind in the air while the board
    /// moved. The board turns about the hands, so the bar stays in them.
    /// </remarks>
    public sealed class XRBoardHandle : MonoBehaviour
    {
        /// <summary>The bar, in cells: its length, thickness, how far in front of the near edge and how high it sits.</summary>
        private const float LengthCells = 6f;
        private const float ThicknessCells = 0.3f;
        private const float OffsetCells = 0.45f;
        private const float HeightCells = 0.25f;

        /// <summary>How much larger than the bar its grab volume is on every side, so a pinch need not be exact.</summary>
        private const float GrabPaddingCells = 0.25f;

        /// <summary>
        /// Degrees the board turns for each degree the wrist twists. A wrist only twists so far comfortably, so the board
        /// turns a little further than the hand does.
        /// </summary>
        private const float TwistGain = 1.5f;

        private readonly List<IXRSelectInteractor> _holders = new List<IXRSelectInteractor>(2);
        private XRBoardPlacement _placement;
        private XRSimpleInteractable _interactable;
        private MeshRenderer _bar;
        private Pose _startBoard;
        private Vector3 _startGrab;
        private Quaternion _startHand;
        private Vector3 _startSpan;

        public bool IsHeld => _holders.Count > 0;

        public static XRBoardHandle Create(Transform boardRoot, XRBoardPlacement placement)
        {
            var go = new GameObject("Board Handle");
            go.transform.SetParent(boardRoot, false);
            go.transform.localPosition = new Vector3(0f, HeightCells, -OffsetCells);

            // A cylinder primitive is two units tall along Y: lay it along X and scale it to the bar's length.
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = "Bar";
            // Immediately, so the interactable added below does not collect this collider as one of its own.
            DestroyImmediate(bar.GetComponent<Collider>());
            bar.transform.SetParent(go.transform, false);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            bar.transform.localScale = new Vector3(ThicknessCells, LengthCells / 2f, ThicknessCells);

            var handle = go.AddComponent<XRBoardHandle>();
            handle._placement = placement;
            handle._bar = bar.GetComponent<MeshRenderer>();
            handle._bar.sharedMaterial = XRBoardMaterials.SignPole;

            var volume = go.AddComponent<BoxCollider>();
            var girth = ThicknessCells + GrabPaddingCells * 2f;
            volume.size = new Vector3(LengthCells + GrabPaddingCells * 2f, girth, girth);

            var interactable = go.AddComponent<XRSimpleInteractable>();
            interactable.selectMode = InteractableSelectMode.Multiple;
            interactable.selectEntered.AddListener(handle.OnGrabbed);
            interactable.selectExited.AddListener(handle.OnReleased);
            interactable.hoverEntered.AddListener(_ => handle.Highlight(true));
            interactable.hoverExited.AddListener(_ => handle.Highlight(false));
            handle._interactable = interactable;
            return handle;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (_holders.Count >= 2 || _holders.Contains(args.interactorObject)) return;
            _holders.Add(args.interactorObject);
            if (_holders.Count == 1) _placement.BeginMove();
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

            _placement.EndMove();
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

            // Turn about the grab, not the board's origin, so the bar stays where the hands are.
            var turn = Quaternion.Euler(0f, yaw, 0f);
            _placement.MoveTo(new Pose(grab + turn * (_startBoard.position - _startGrab), turn * _startBoard.rotation));
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
