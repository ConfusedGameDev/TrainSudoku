using TrainSudoku.Core;
using TrainSudoku.XR.Rules;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The tutorial's rings (XR-PRD 7): one round the cell the coach is pointing at, and, for the first three rails, one
    /// round the tray slot to take the piece from. They are their own objects, never a tile material: the grab hover
    /// swaps the slab materials under a hand, and would wipe a highlight kept there.
    /// </summary>
    public sealed class XRTutorialMarks : MonoBehaviour
    {
        private const float RingInner = 0.37f;
        private const float RingOuter = 0.47f;

        /// <summary>Clear of the slab's top, so the ring never fights it for depth.</summary>
        private const float RingLift = 0.006f;

        private const float PulseScale = 0.08f;
        private const float PulseHertz = 1.2f;

        private XRBoardDisplay _display;
        private XRTray _tray;
        private Mesh _mesh;
        private Transform _cellRing;
        private Transform _slotRing;
        private MeshRenderer _cellRenderer;
        private XRTutorialGuide _guide;

        public static XRTutorialMarks Create(XRBoardDisplay display, XRTray tray)
        {
            var go = new GameObject("Tutorial Marks");
            go.transform.SetParent(display.transform, false);
            var marks = go.AddComponent<XRTutorialMarks>();
            marks._display = display;
            marks._tray = tray;
            marks._mesh = new Mesh { name = "Tutorial Ring" };
            ProceduralBoardMesh.FillRing(marks._mesh, RingInner, RingOuter, 1f);
            marks._cellRing = marks.Ring("Cell Ring", go.transform, out marks._cellRenderer);
            marks.Show(XRTutorialGuide.None);
            return marks;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_slotRing != null) Destroy(_slotRing.gameObject);
        }

        /// <summary>Points the rings at <paramref name="guide"/>; <see cref="XRTutorialGuide.None"/> hides both.</summary>
        public void Show(XRTutorialGuide guide)
        {
            _guide = guide;
            var onBoard = guide.Active && _display != null && _display.Level != null
                          && guide.X >= 0 && guide.Y >= 0 && guide.X < _display.Level.Width && guide.Y < _display.Level.Height;
            _cellRing.gameObject.SetActive(onBoard);
            if (onBoard)
            {
                var (x, z) = BoardLayout.CellCenter(guide.X, guide.Y, _display.Level.Width, _display.Level.Height);
                _cellRing.localPosition = new Vector3((float)x, RingLift, (float)z);
                // The two lessons in getting it wrong ring in red; laying the route rings in ink.
                var wrong = guide.Kind == XRGuideKind.Mistake || guide.Kind == XRGuideKind.Erase || guide.Kind == XRGuideKind.Discard;
                _cellRenderer.sharedMaterial = XRBoardMaterials.Solid(wrong ? XRPalette.Stop : XRPalette.Ink);
            }

            ShowSlot(onBoard ? guide.Slot : null);
        }

        /// <summary>The tray rebuilds its slots on every level: hang the slot ring on the new ones.</summary>
        public void Refresh() => Show(_guide);

        private void ShowSlot(PieceKey? key)
        {
            var slot = key.HasValue && _tray != null && _tray.isActiveAndEnabled ? _tray.SlotTransform(key.Value) : null;
            if (slot == null)
            {
                if (_slotRing != null) _slotRing.gameObject.SetActive(false);
                return;
            }

            if (_slotRing == null) _slotRing = Ring("Slot Ring", slot, out _);
            _slotRing.GetComponent<MeshRenderer>().sharedMaterial = XRBoardMaterials.Solid(XRPalette.Ink);
            // Parented on the slot, so it rides the tray when it slides to another edge.
            _slotRing.SetParent(slot, false);
            _slotRing.localPosition = new Vector3(0f, RingLift, 0f);
            _slotRing.gameObject.SetActive(true);
        }

        private Transform Ring(string name, Transform parent, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        private void Update()
        {
            // A slow breath, on unscaled time like every other motion, so it reads as "here" rather than as decoration.
            var scale = 1f + PulseScale * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseHertz * 2f * Mathf.PI));
            if (_cellRing != null) _cellRing.localScale = Vector3.one * scale;
            if (_slotRing != null) _slotRing.localScale = Vector3.one * scale;
        }
    }
}
