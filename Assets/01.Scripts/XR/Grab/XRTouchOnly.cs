using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.XR
{
    /// <summary>
    /// No rays (XR-PRD X16, revised 2026-09-15 after the first XR8 headset check). Pieces and the handle are taken by a
    /// close pinch or grip, and every menu — the platform maps, the signboard, the wrist menu — is pressed with a
    /// fingertip. A hand or controller ray reaching for one thing kept catching another: it pressed RESUME from anywhere,
    /// and got in the way of the wrist button and the handle.
    /// </summary>
    /// <remarks>
    /// The rig's own parts switch rays back on: the hands' <c>PokeGestureDetector</c> turns far casting on whenever a hand
    /// stops pointing, and the controllers' <c>ControllerInputActionManager</c> brings up a teleport ray on the thumbstick.
    /// Both are switched off, and far casting, XRI's UI interaction and the ray visuals are held off every frame, so
    /// nothing brings them back. The panels read the fingertips themselves (<see cref="XRPanelTouch"/>). The hand's ray
    /// pose is still there to read: first placement aims the ghost with it.
    /// </remarks>
    public static class XRTouchOnly
    {
        private static readonly List<NearFarInteractor> NearFars = new List<NearFarInteractor>();
        private static readonly List<XRPokeInteractor> Pokes = new List<XRPokeInteractor>();
        private static readonly List<GameObject> Rays = new List<GameObject>();
        private static int _frame = -1;

        /// <summary>Holds the rig to touch only. Cheap after the first call: call it every frame.</summary>
        public static void Enforce()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            NearFars.RemoveAll(interactor => interactor == null);
            if (NearFars.Count == 0) Find();

            foreach (var nearFar in NearFars)
            {
                if (nearFar.enableFarCasting) nearFar.enableFarCasting = false;
                if (nearFar.enableUIInteraction) nearFar.enableUIInteraction = false;
            }

            foreach (var poke in Pokes)
                if (poke != null && poke.enableUIInteraction)
                    poke.enableUIInteraction = false;

            foreach (var ray in Rays)
                if (ray != null && ray.activeSelf)
                    ray.SetActive(false);
        }

        /// <summary>The rig's interactors, its ray visuals and teleport rays, and the parts that would switch rays back on.</summary>
        private static void Find()
        {
            NearFars.AddRange(Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include));
            Pokes.Clear();
            Pokes.AddRange(Object.FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include));

            Rays.Clear();
            foreach (var nearFar in NearFars)
            foreach (var line in nearFar.GetComponentsInChildren<LineRenderer>(true))
                if (line.gameObject != nearFar.gameObject)
                    Rays.Add(line.gameObject);
            foreach (var teleport in Object.FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include))
                Rays.Add(teleport.gameObject);

            // Sample scripts, so matched by name: XR code never references the samples' assemblies.
            var switchedOff = 0;
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                var type = behaviour.GetType().Name;
                if (type != "PokeGestureDetector" && type != "ControllerInputActionManager") continue;
                behaviour.enabled = false;
                switchedOff++;
            }

            if (NearFars.Count > 0)
                Debug.Log($"[XR touch only] {NearFars.Count} near-far interactors without rays; {Rays.Count} ray visuals and {switchedOff} ray switches off.");
        }
    }
}
