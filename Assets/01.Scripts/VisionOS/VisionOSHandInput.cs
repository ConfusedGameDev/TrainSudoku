using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.VisionOS
{
    /// <summary>
    /// Makes the Quest rig's hands work on the Vision Pro: pinch, pinch point and fingertip, computed from the
    /// hand joints and fed into the rig's own XRI interactors.
    /// </summary>
    /// <remarks>
    /// <b>Why this exists.</b> The rig's pose drivers and its Select action read XR Hands' <i>common gestures</i>:
    /// <c>&lt;XRHandDevice&gt;/pinchPosition</c>, <c>/pokePosition</c>, <c>/aimPosition</c> and
    /// <c>/pinchTouched</c>. A provider has to supply those, and <b>the visionOS provider supplies none of
    /// them</b> — it gives the joints and nothing else. So on the first headset run (2026-09-22) the hand meshes
    /// tracked perfectly, because they are drawn from joints, while every pinch did nothing: the controls
    /// existed and read zero. XR Hands fills them only through internal setters, so they cannot be fed from
    /// outside.
    ///
    /// Nothing in the game reads those controls directly, though. <c>XRTouchPoints</c>, <c>XRBoardPlacement</c>,
    /// <c>XRIGrabInput</c> and the panel presses all go through the rig's XRI interactors: the poke interactor's
    /// transform, the near-far interactor's near-cast origin, and its <c>selectInput</c>. So this feeds exactly
    /// those three, and the whole layer above works unchanged — no Quest file is edited (VisionOS-PRD 2).
    ///
    /// <list type="bullet">
    /// <item><b>Poses</b>: the <c>TrackedPoseDriver</c>s bound to <c>Pinch Position</c> and <c>Poke Position</c>
    /// are switched off, and their transforms are driven from the joints instead. Found by the action they
    /// read, never by object name, so a renamed rig object cannot silently break it.</item>
    /// <item><b>Select</b>: through <c>XRInputButtonReader.bypass</c>, XRI's own injection point — every read
    /// of the interactor's select input is answered here. The input action is left exactly as it was.</item>
    /// <item><b>Aim</b> is deliberately <b>not</b> supplied. The Quest removed every ray (X16), and without an aim
    /// pose <c>XRBoardPlacement</c> aims the placement ghost with the gaze — which on this headset is the
    /// platform's own look-and-pinch.</item>
    /// </list>
    ///
    /// Joint poses are in session space, the space the head's pose driver also writes, so they are brought into
    /// the world through the rig's camera-offset transform — the same step <c>XRWristMenu</c> takes for the
    /// wrist.
    /// </remarks>
    [DefaultExecutionOrder(-10000)] // before XRI's interaction manager and every script that reads a pinch
    [DisallowMultipleComponent]
    public sealed class VisionOSHandInput : MonoBehaviour
    {
        const string k_Tag = "[AVP hands]";

        /// <summary>Thumb and index tips closer than this start a pinch.</summary>
        const float k_PinchOnMetres = 0.015f;

        /// <summary>…and must part by this much to end it. The gap is the hysteresis that stops a pinch held at
        /// the threshold from chattering on and off, which a grab would read as drop-and-regrab.</summary>
        const float k_PinchOffMetres = 0.03f;

        /// <summary>The span over which the analogue pinch value runs from 1 to 0.</summary>
        const float k_ValueFullMetres = 0.01f, k_ValueNoneMetres = 0.04f;

#if UNITY_VISIONOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<VisionOSHandInput>() != null) return;
            var host = new GameObject(nameof(VisionOSHandInput));
            DontDestroyOnLoad(host);
            host.AddComponent<VisionOSHandInput>();
        }
#endif

        /// <summary>One hand's pinch, answering the interactor's select reads.</summary>
        sealed class Pinch : IXRInputButtonReader
        {
            public bool Held;
            public float Value;
            public int PressedFrame = -1, ReleasedFrame = -1;

            public void Set(bool tracked, float distance)
            {
                var held = tracked && (Held ? distance < k_PinchOffMetres : distance < k_PinchOnMetres);
                Value = tracked
                    ? Mathf.Clamp01(Mathf.InverseLerp(k_ValueNoneMetres, k_ValueFullMetres, distance))
                    : 0f;
                if (held && !Held) PressedFrame = Time.frameCount;
                if (!held && Held) ReleasedFrame = Time.frameCount;
                Held = held;
            }

            public bool ReadIsPerformed() => Held;
            public bool ReadWasPerformedThisFrame() => PressedFrame == Time.frameCount;
            public bool ReadWasCompletedThisFrame() => ReleasedFrame == Time.frameCount;
            public float ReadValue() => Value;

            public bool TryReadValue(out float value)
            {
                value = Value;
                return Held;
            }
        }

        sealed class Side
        {
            public readonly Handedness Hand;
            public readonly Pinch Pinch = new Pinch();
            public readonly List<Transform> PinchPoses = new List<Transform>();
            public readonly List<Transform> PokePoses = new List<Transform>();
            public bool WasTracked;

            public Side(Handedness hand) => Hand = hand;
        }

        readonly Side m_Left = new Side(Handedness.Left);
        readonly Side m_Right = new Side(Handedness.Right);
        static readonly List<XRHandSubsystem> k_Subsystems = new List<XRHandSubsystem>();

        XRHandSubsystem m_Hands;
        XROrigin m_Origin;
        bool m_Wired;

        void Update()
        {
            if (!m_Wired) m_Wired = TryWire();
            if (!m_Wired) return;

            if (m_Hands == null || !m_Hands.running) m_Hands = FindRunningHands();
            if (m_Hands == null) return;

            Feed(m_Left, m_Hands.leftHand);
            Feed(m_Right, m_Hands.rightHand);
        }

        static readonly List<Renderer> k_RigRenderers = new List<Renderer>();
        int m_HiddenCount = -1;

        void LateUpdate()
        {
            if (m_Origin != null) HideRigVisuals();
        }

        /// <summary>
        /// Hides every renderer under the rig: on this headset the player's real hands are the hands.
        /// </summary>
        /// <remarks>
        /// <c>VisionOSSettings.upperLimbVisibility</c> has the OS draw the player's own arms and hands over the
        /// content, so the rig's decoration only covers them. On the first full playthrough (2026-09-22) the
        /// player asked for all of it gone: the skinned hand meshes (the rig carries Quest and AndroidXR
        /// versions of each hand, plus the XR Hands <c>Hand Visualizer</c>) and the <c>PinchPointStabilized</c>
        /// visual, a <c>FresnelHighlight</c> blob at 33% alpha between thumb and index.
        ///
        /// It takes everything under the XR Origin <b>except the <c>TrackablesParent</c> subtree</b>, by position in
        /// the hierarchy rather than by name or type. <b>That exception is the whole game.</b> AR Foundation
        /// creates every anchor and every detected plane as a child of <c>XROrigin.TrackablesParent</c>, and
        /// <c>XRBoardPlacement</c> parents the board to its anchor — so once placed, the board, the signboard,
        /// the tray and the map all live under the XR Origin. The first version of this sweep took the whole
        /// origin, on the belief that nothing of the game lived there, and the second headset run (2026-09-22)
        /// showed the ghost and then nothing at all: placing the board moved it under the origin and hid it.
        /// The hands, controllers and pinch visuals are all under the camera offset instead.
        ///
        /// It runs every frame rather than once because the <c>Hand Visualizer</c> instantiates its meshes only
        /// when a hand is first tracked, and toggles them with tracking.
        /// </remarks>
        void HideRigVisuals()
        {
            var trackables = m_Origin.TrackablesParent;
            k_RigRenderers.Clear();
            m_Origin.GetComponentsInChildren(true, k_RigRenderers);

            var hidden = 0;
            foreach (var renderer in k_RigRenderers)
            {
                if (trackables != null && renderer.transform.IsChildOf(trackables)) continue;
                if (renderer.enabled) renderer.enabled = false;
                hidden++;
            }

            if (hidden == m_HiddenCount) return;
            m_HiddenCount = hidden;
            var names = new System.Text.StringBuilder();
            foreach (var renderer in k_RigRenderers)
                if (trackables == null || !renderer.transform.IsChildOf(trackables))
                    names.Append(names.Length == 0 ? "" : ", ").Append(renderer.name);
            Debug.Log($"{k_Tag} Hiding {hidden} rig renderer(s), sparing everything under " +
                $"{(trackables != null ? trackables.name : "(no trackables parent)")}: {names}");
        }

        /// <summary>Takes over the rig's pinch and poke pose drivers and hooks every near-far interactor's select.</summary>
        bool TryWire()
        {
            m_Origin = FindAnyObjectByType<XROrigin>();
            if (m_Origin == null) return false;

            var drivers = FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var driver in drivers)
            {
                var action = driver.positionInput.action;
                if (action == null || action.actionMap == null) continue;

                var side = SideFor(action.actionMap.name);
                if (side == null) continue;

                var list = action.name == "Pinch Position" ? side.PinchPoses
                    : action.name == "Poke Position" ? side.PokePoses
                    : null;
                if (list == null) continue;

                // Off, or it would write the empty common-gesture pose back over ours every frame.
                driver.enabled = false;
                list.Add(driver.transform);
            }

            var hooked = 0;
            foreach (var nearFar in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var side = nearFar.handedness == InteractorHandedness.Left ? m_Left
                    : nearFar.handedness == InteractorHandedness.Right ? m_Right
                    : null;
                if (side == null) continue;
                nearFar.selectInput.bypass = side.Pinch;
                hooked++;
            }

            Debug.Log($"{k_Tag} Wired: pinch poses L{m_Left.PinchPoses.Count}/R{m_Right.PinchPoses.Count}, " +
                $"poke poses L{m_Left.PokePoses.Count}/R{m_Right.PokePoses.Count}, {hooked} near-far select(s) hooked.");
            return true;
        }

        /// <summary>The rig names its pose maps "XRI Left" and "XRI Right".</summary>
        Side SideFor(string mapName) =>
            mapName.EndsWith("Left") ? m_Left
            : mapName.EndsWith("Right") ? m_Right
            : null;

        void Feed(Side side, XRHand hand)
        {
            Pose thumb = default, index = default;
            var tracked = hand.isTracked
                && hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out thumb)
                && hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out index);

            if (tracked != side.WasTracked)
            {
                side.WasTracked = tracked;
                Debug.Log($"{k_Tag} {side.Hand} hand {(tracked ? "tracked" : "lost")}.");
            }

            if (!tracked)
            {
                side.Pinch.Set(false, float.MaxValue);
                return;
            }

            var wasHeld = side.Pinch.Held;
            side.Pinch.Set(true, Vector3.Distance(thumb.position, index.position));
            if (side.Pinch.Held != wasHeld)
                Debug.Log($"{k_Tag} {side.Hand} pinch {(side.Pinch.Held ? "on" : "off")}.");

            // The pinch point sits between the two tips and takes the knuckle's orientation, which stays steady
            // through a pinch where the tips themselves roll.
            var knuckle = hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out var proximal)
                ? proximal.rotation
                : index.rotation;
            Place(side.PinchPoses, (thumb.position + index.position) * 0.5f, knuckle);
            Place(side.PokePoses, index.position, index.rotation);
        }

        /// <summary>Session-space pose into the world, through the rig's camera offset.</summary>
        void Place(List<Transform> targets, Vector3 position, Quaternion rotation)
        {
            if (targets.Count == 0) return;
            var offset = m_Origin.CameraFloorOffsetObject != null
                ? m_Origin.CameraFloorOffsetObject.transform
                : m_Origin.transform;
            var worldPosition = offset.TransformPoint(position);
            var worldRotation = offset.rotation * rotation;
            foreach (var target in targets)
                if (target != null) target.SetPositionAndRotation(worldPosition, worldRotation);
        }

        static XRHandSubsystem FindRunningHands()
        {
            k_Subsystems.Clear();
            SubsystemManager.GetSubsystems(k_Subsystems);
            foreach (var subsystem in k_Subsystems)
                if (subsystem.running) return subsystem;
            return null;
        }
    }
}
