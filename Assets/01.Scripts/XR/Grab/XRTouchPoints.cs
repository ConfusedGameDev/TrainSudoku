using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.XR
{
    /// <summary>A point a hand touches with, and which hand or controller it is, the same from frame to frame.</summary>
    public readonly struct TouchPoint
    {
        public readonly int Id;
        public readonly Vector3 Position;

        public TouchPoint(int id, Vector3 position)
        {
            Id = id;
            Position = position;
        }
    }

    /// <summary>
    /// Where the player's hands can touch things this frame, read off the rig's own interactors so hands and controllers
    /// come out alike: each poke interactor's point (a hand's index fingertip, from the OpenXR poke pose; a controller's
    /// tip) and each near-far interactor's near-cast origin (a hand's pinch point, from the pinch pose; a controller).
    /// </summary>
    /// <remarks>
    /// XRI decides hovers by its own casts, filters and group priorities, and on the headset neither a fingertip on a map
    /// roundel or a signboard button nor a hand resting on the board handle came through as one (the XR7 headset
    /// check). What must answer to a plain touch reads these points instead. The rig's <c>PokeGestureDetector</c> only
    /// switches the near-far interactors' far casting, so the poke points are live whatever shape the hand makes.
    /// </remarks>
    public static class XRTouchPoints
    {
        private static readonly List<XRPokeInteractor> Pokes = new List<XRPokeInteractor>();
        private static readonly List<NearFarInteractor> NearFars = new List<NearFarInteractor>();
        private static readonly List<TouchPoint> TipPoints = new List<TouchPoint>();
        private static readonly List<Vector3> PinchPointList = new List<Vector3>();
        private static int _frame = -1;

        /// <summary>Fingertips, and controller tips, in world space; each keeps its id while its hand or controller is tracked.</summary>
        public static IReadOnlyList<TouchPoint> Fingertips
        {
            get
            {
                Refresh();
                return TipPoints;
            }
        }

        /// <summary>Pinch points, and controllers, in world space: where a close pinch or grip takes hold.</summary>
        public static IReadOnlyList<Vector3> PinchPoints
        {
            get
            {
                Refresh();
                return PinchPointList;
            }
        }

        private static void Refresh()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            TipPoints.Clear();
            PinchPointList.Clear();

            // The rig's interactors are all there from the start; an untracked hand or a set-down controller is switched
            // off by the rig, which is what isActiveAndEnabled reads.
            Pokes.RemoveAll(poke => poke == null);
            NearFars.RemoveAll(nearFar => nearFar == null);
            if (Pokes.Count == 0) Pokes.AddRange(Object.FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (NearFars.Count == 0) NearFars.AddRange(Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            foreach (var poke in Pokes)
                if (poke.isActiveAndEnabled)
                    // The interactor's identity hash: stable for its lifetime (GetInstanceID is obsolete on this editor).
                    TipPoints.Add(new TouchPoint(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(poke),
                        (poke.attachTransform != null ? poke.attachTransform : poke.transform).position));

            foreach (var nearFar in NearFars)
            {
                if (!nearFar.isActiveAndEnabled) continue;
                var origin = nearFar.nearInteractionCaster != null ? nearFar.nearInteractionCaster.castOrigin : null;
                PinchPointList.Add((origin != null ? origin : nearFar.transform).position);
            }
        }
    }
}
