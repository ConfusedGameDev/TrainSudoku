using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Things only a hand's own pinch or grip may take, never a ray (XR-PRD 4.2 and 5.3, revised after the XR6 headset
    /// check): the pieces on the board, packed a cell apart, where a ray aimed at one kept landing on a neighbour; and the
    /// board handle, which rays swept across the near edge kept catching. Their colliders go on Unity's built-in
    /// Ignore Raycast layer, which the rig's far casters already leave out, and every near-far interactor's near caster
    /// is widened to take that layer in — on the controllers it looks at Default only.
    /// </summary>
    public static class XRDirectReach
    {
        /// <summary>Unity's built-in Ignore Raycast layer.</summary>
        public const int Layer = 2;

        private static readonly HashSet<NearFarInteractor> Configured = new HashSet<NearFarInteractor>();
        private static int _configuredFrame = -1;

        /// <summary>Makes <paramref name="volume"/>'s collider reachable by a close pinch or grip only.</summary>
        public static void Apply(GameObject volume)
        {
            if (volume == null) return;
            volume.layer = Layer;
            ConfigureInteractors();
        }

        /// <summary>Near casters take the layer in, far casters leave it out. Once per interactor; the hands and controllers are all in the rig from the start.</summary>
        public static void ConfigureInteractors()
        {
            if (_configuredFrame == Time.frameCount) return;
            _configuredFrame = Time.frameCount;
            var bit = 1 << Layer;
            Configured.RemoveWhere(interactor => interactor == null);
            foreach (var nearFar in Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!Configured.Add(nearFar)) continue;
                if (nearFar.nearInteractionCaster is SphereInteractionCaster near) near.physicsLayerMask |= bit;
                if (nearFar.farInteractionCaster is CurveInteractionCaster far) far.raycastMask &= ~bit;
            }
        }
    }
}
