using System;
using TrainSudoku.XR.Rules;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The grab interface on the XR Interaction Toolkit (XR-PRD 10.4). Every grab target becomes an XRI interactable, so
    /// the rig's near-far interactors — a pinch or a grip, directly or along the hand or controller ray (X16) — decide
    /// what each hand is aiming at, and arbitrate between the pieces, the board handle and, later, the UI. XRI only
    /// reports: nothing it selects moves. Pokes and gaze never grab.
    /// </summary>
    public sealed class XRIGrabInput : MonoBehaviour, IGrabInput
    {
        private readonly IXRSelectInteractor[] _interactors = new IXRSelectInteractor[2];
        private readonly XRGrabTargetInteractable[] _targets = new XRGrabTargetInteractable[2];

        public event Action<Hand, GrabTarget> Grabbed;
        public event Action<Hand> Released;
        public event Action<GrabTarget, bool> HoverChanged;

        public bool AcceptsGrabs { get; set; } = true;

        public void AddTarget(Collider volume, GrabTarget target, Func<bool> grabbable, bool directOnly)
        {
            if (volume == null) return;
            if (directOnly) XRDirectReach.Apply(volume.gameObject);
            if (!volume.TryGetComponent<XRGrabTargetInteractable>(out var interactable))
                interactable = volume.gameObject.AddComponent<XRGrabTargetInteractable>();
            interactable.Setup(this, target, grabbable);
        }

        public bool TryGetHold(Hand hand, out GrabHold hold)
        {
            hold = default;
            var interactor = _interactors[(int)hand];
            var target = _targets[(int)hand];
            if (interactor == null || target == null) return false;

            var point = interactor.GetAttachTransform(target).position;
            // Far while the selection came down the ray; XRI turns it Near if the player pulls the piece into the hand.
            if (interactor is NearFarInteractor nearFar && nearFar.selectionRegion.Value == NearFarInteractor.Region.Far && nearFar.curveOrigin != null)
            {
                var origin = nearFar.curveOrigin;
                hold = new GrabHold(origin.position, point, new Ray(origin.position, origin.forward), true);
            }
            else
            {
                hold = new GrabHold(point, point, default, false);
            }

            return true;
        }

        /// <summary>
        /// Where something else has first claim on a pinch: a hand pinching there takes no board piece. The board handle
        /// claims the space round its rail, which wraps the corner cell; a pinch on the rail kept lifting that cell's piece
        /// (the second XR8 headset check).
        /// </summary>
        public Func<Vector3, bool> Reserved { get; set; }

        /// <summary>A hand may start a grab: grabs are open, it is a hand or controller rather than a poke or the gaze, it holds nothing, and the target has something to give.</summary>
        internal bool CanGrab(IXRInteractor interactor, GrabTarget target, Func<bool> grabbable) =>
            CanHover(interactor, target, grabbable) && _interactors[(int)HandOf(interactor)] == null;

        internal bool CanHover(IXRInteractor interactor, GrabTarget target, Func<bool> grabbable) =>
            AcceptsGrabs && !(interactor is XRPokeInteractor) && !(interactor is XRGazeInteractor) &&
            (target.IsTray || Reserved == null || !Reserved(PinchPoint(interactor))) && (grabbable == null || grabbable());

        /// <summary>Where the hand takes hold: a near-far interactor's near-cast origin (a hand's pinch point), else the interactor itself.</summary>
        private static Vector3 PinchPoint(IXRInteractor interactor)
        {
            var origin = interactor is NearFarInteractor nearFar && nearFar.nearInteractionCaster != null ? nearFar.nearInteractionCaster.castOrigin : null;
            return (origin != null ? origin : interactor.transform).position;
        }

        internal void OnSelectEntered(XRGrabTargetInteractable target, IXRSelectInteractor interactor)
        {
            var hand = HandOf(interactor);
            if (_interactors[(int)hand] != null) return;
            _interactors[(int)hand] = interactor;
            _targets[(int)hand] = target;
            Grabbed?.Invoke(hand, target.Target);
        }

        internal void OnSelectExited(XRGrabTargetInteractable target, IXRSelectInteractor interactor)
        {
            var hand = HandOf(interactor);
            if (_interactors[(int)hand] != interactor) return;
            _interactors[(int)hand] = null;
            _targets[(int)hand] = null;
            Released?.Invoke(hand);
        }

        internal void OnHoverChanged(XRGrabTargetInteractable target) => HoverChanged?.Invoke(target.Target, target.isHovered);

        private static Hand HandOf(IXRInteractor interactor) =>
            interactor.handedness == InteractorHandedness.Left ? Hand.Left : Hand.Right;
    }
}
