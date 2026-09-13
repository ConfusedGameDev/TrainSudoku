using TrainSudoku.Core;
using TrainSudoku.XR.Rules;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The tray (XR-PRD 4.1): six slots, one per key, each the real track piece on a small concrete tile at board scale.
    /// Supply is unlimited — taking a piece leaves its slot as it was. The tray hangs off the board, so it moves with it;
    /// <see cref="TrayDock"/> decides which edge it docks at, and a move slides it round. Its pieces keep the board's yaw
    /// wherever it docks: keys belong to the board, not to the viewer.
    /// </summary>
    public sealed class XRTray : MonoBehaviour
    {
        private const float SlideSeconds = 0.35f;

        /// <summary>The volume a hand aims at to take a slot's piece, in cells: a little short of the pitch, so neighbours never overlap.</summary>
        private static readonly Vector3 GrabVolume = new Vector3(1.2f, 0.6f, 1.2f);
        private const float GrabVolumeCentre = 0.2f;

        private readonly TrayDock _dock = new TrayDock();
        private readonly Transform[] _slots = new Transform[TrayDock.SlotCount];
        private readonly MeshRenderer[] _tiles = new MeshRenderer[TrayDock.SlotCount];
        private readonly Vector3[] _slideFrom = new Vector3[TrayDock.SlotCount];
        private XRBoardDisplay _display;
        private IGrabInput _input;
        private float _slide = 1f;

        /// <summary>A piece is in a hand: the tray stays at its edge until it is let go (4.1).</summary>
        public bool Holding { get; set; }

        public Direction Edge => _dock.Edge;

        /// <summary>The side of the player's edge the tray docks on. Changing it slides the tray across.</summary>
        public Hand DominantHand
        {
            get => _dock.DominantHand;
            set
            {
                if (value == _dock.DominantHand) return;
                _dock.DominantHand = value;
                SlideToDock();
            }
        }

        public static XRTray Create(XRBoardDisplay display, IGrabInput input, Hand dominantHand)
        {
            var go = new GameObject("Tray");
            go.transform.SetParent(display.transform, false);
            var tray = go.AddComponent<XRTray>();
            tray._display = display;
            tray._input = input;
            tray._dock.DominantHand = dominantHand;
            if (input != null) input.HoverChanged += tray.OnHoverChanged;
            return tray;
        }

        private void OnDestroy()
        {
            if (_input != null) _input.HoverChanged -= OnHoverChanged;
        }

        /// <summary>
        /// Builds the slots for the level on show, at the edge the tray is docked at. Run after every
        /// <see cref="XRBoardDisplay.Load"/>: the track meshes belong to the level.
        /// </summary>
        public void Build()
        {
            for (var i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            if (_display == null || _display.Level == null) return;
            for (var slot = 0; slot < TrayDock.SlotCount; slot++) BuildSlot(slot);
            _slide = 1f;
            PlaceSlots(1f);
        }

        /// <summary>Where a slot's piece sits, in world space: where a piece sent back to the tray flies to.</summary>
        public Vector3 SlotWorldPosition(PieceKey key)
        {
            var slot = _slots[TrayDock.SlotOf(key)];
            return slot != null ? slot.TransformPoint(0f, _display.PieceLift, 0f) : transform.position;
        }

        private void BuildSlot(int slot)
        {
            var key = TrayDock.KeyAt(slot);
            var root = new GameObject($"Slot {key}");
            root.transform.SetParent(transform, false);
            var volume = root.AddComponent<BoxCollider>();
            volume.center = new Vector3(0f, GrabVolumeCentre, 0f);
            volume.size = GrabVolume;

            var size = (float)BoardLayout.CellSize - XRBoardDisplay.TileInset;
            var tile = new GameObject("Tile");
            tile.transform.SetParent(root.transform, false);
            tile.transform.localPosition = new Vector3(0f, -XRBoardDisplay.TileHeight / 2f, 0f);
            tile.transform.localScale = new Vector3(size, XRBoardDisplay.TileHeight, size);
            tile.AddComponent<MeshFilter>().sharedMesh = _display.TileMesh;
            _tiles[slot] = tile.AddComponent<MeshRenderer>();
            _tiles[slot].sharedMaterial = XRBoardMaterials.Tile;

            var piece = new GameObject($"Piece {key}");
            piece.transform.SetParent(root.transform, false);
            piece.transform.localPosition = new Vector3(0f, _display.PieceLift, 0f);
            piece.AddComponent<MeshFilter>().sharedMesh = _display.TrackMeshFor(key);
            piece.AddComponent<MeshRenderer>().sharedMaterial = _display.PlayerTrackMaterial;

            _slots[slot] = root.transform;
            // Tray pieces stand apart, so a ray can still take them from across the table (X16).
            _input?.AddTarget(volume, GrabTarget.Tray(key), null, directOnly: false);
        }

        private void Update()
        {
            if (_display == null || _display.Level == null || _slots[0] == null) return;

            var head = Camera.main;
            if (head != null)
            {
                // Where the player stands, in the board's frame: the tray's parent is the board.
                var viewer = transform.parent.InverseTransformPoint(head.transform.position);
                if (_dock.Update(viewer.x, viewer.z, _display.Level.Width, _display.Level.Height, Time.deltaTime, Holding))
                    SlideToDock();
            }

            if (_slide >= 1f) return;
            _slide = Mathf.Min(1f, _slide + Time.deltaTime / SlideSeconds);
            PlaceSlots(_slide);
        }

        private void SlideToDock()
        {
            for (var slot = 0; slot < TrayDock.SlotCount; slot++)
                if (_slots[slot] != null) _slideFrom[slot] = _slots[slot].localPosition;
            _slide = 0f;
        }

        private void PlaceSlots(float t)
        {
            var eased = t * t * (3f - 2f * t);
            for (var slot = 0; slot < TrayDock.SlotCount; slot++)
            {
                if (_slots[slot] == null) continue;
                var (x, z) = _dock.SlotCentre(slot, _display.Level.Width, _display.Level.Height);
                var docked = new Vector3((float)x, 0f, (float)z);
                _slots[slot].localPosition = t >= 1f ? docked : Vector3.Lerp(_slideFrom[slot], docked, eased);
            }
        }

        private void OnHoverChanged(GrabTarget target, bool hovered)
        {
            if (!target.IsTray) return;
            var tile = _tiles[TrayDock.SlotOf(target.Key)];
            if (tile != null) tile.sharedMaterial = hovered ? XRBoardMaterials.PlatformEdge : XRBoardMaterials.Tile;
        }
    }
}
