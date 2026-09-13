using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.XR
{
    /// <summary>
    /// An XRI simple interactable that only a hand's or controller's grab can hover or select: a poke or the gaze never
    /// does. The board handle is one, so a fingertip brushing it while laying track cannot move the board.
    /// </summary>
    public sealed class XRGrabOnlyInteractable : XRSimpleInteractable
    {
        public override bool IsHoverableBy(IXRHoverInteractor interactor) =>
            base.IsHoverableBy(interactor) && !(interactor is XRPokeInteractor) && !(interactor is XRGazeInteractor);

        public override bool IsSelectableBy(IXRSelectInteractor interactor) =>
            base.IsSelectableBy(interactor) && !(interactor is XRPokeInteractor) && !(interactor is XRGazeInteractor);
    }
}
