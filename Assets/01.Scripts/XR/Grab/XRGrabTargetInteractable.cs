using System;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.XR
{
    /// <summary>
    /// One grab target as XRI sees it (<see cref="XRIGrabInput"/>): a tray slot or a board cell. It can be hovered and
    /// selected only while it has something to give — an empty cell is no target, so a hand reaching into the board
    /// finds the nearest piece rather than the nearest slab — and a hand already holding it keeps it, whatever becomes
    /// of the piece it lifted. Both hands may hold one slot at once: supply is unlimited (4.1).
    /// </summary>
    public sealed class XRGrabTargetInteractable : XRSimpleInteractable
    {
        private XRIGrabInput _input;
        private Func<bool> _grabbable;

        public GrabTarget Target { get; private set; }

        internal void Setup(XRIGrabInput input, GrabTarget target, Func<bool> grabbable)
        {
            var first = _input == null;
            _input = input;
            Target = target;
            _grabbable = grabbable;
            if (!first) return;

            selectMode = InteractableSelectMode.Multiple;
            selectEntered.AddListener(OnSelectEntered);
            selectExited.AddListener(OnSelectExited);
            hoverEntered.AddListener(_ => _input.OnHoverChanged(this));
            hoverExited.AddListener(_ => _input.OnHoverChanged(this));
        }

        private void OnSelectEntered(SelectEnterEventArgs args) => _input.OnSelectEntered(this, args.interactorObject);
        private void OnSelectExited(SelectExitEventArgs args) => _input.OnSelectExited(this, args.interactorObject);

        // XRI asks this every frame of a selection too, and ends the selection when it turns false; so a hand holding
        // this target keeps it even once the cell it lifted from is empty.
        public override bool IsSelectableBy(IXRSelectInteractor interactor) =>
            base.IsSelectableBy(interactor) && (IsSelected(interactor) || (_input != null && _input.CanGrab(interactor, Target, _grabbable)));

        public override bool IsHoverableBy(IXRHoverInteractor interactor) =>
            base.IsHoverableBy(interactor) && _input != null && _input.CanHover(interactor, Target, _grabbable);
    }
}
