using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Puts the board in the room and keeps it there (XR-PRD 5.2, 5.3). A saved anchor brings it back where it was
    /// left. Otherwise it asks for the spatial-data permission, slides a ghost platform over detected tables under the
    /// hand or controller ray — or floats it at waist height when there is no surface — and places it on a pinch or
    /// trigger, saving the spot as an anchor. The <see cref="XRBoardHandle"/> moves it afterwards, and letting go of
    /// the handle anchors and saves the new spot. Board placement is a precondition held by the shell, not a GameFlow
    /// state (6.1).
    /// </summary>
    /// <remarks>
    /// <see cref="BoardRoot"/> is what the board hangs from: scaled to the cell size, its origin is the middle of the
    /// board's near edge, on the surface, and its +Z points away from the player. <c>XRBoardDisplay</c> keeps its near
    /// edge on that origin, so a bigger board grows away from the player (X17).
    /// </remarks>
    public sealed class XRBoardPlacement : MonoBehaviour
    {
        private const string AnchorKey = "tsugi.xr.boardAnchor";
        private const string OnSurfaceKey = "tsugi.xr.boardOnSurface";
        private const float MaxRayDistance = 5f;
        private const string CellKey = "tsugi.xr.boardCell";

        /// <summary>
        /// The range the two-hand handle may scale a cell to, in metres (X6, revised 2026-09-14): below 4 cm a piece is
        /// too small to pinch reliably with hand tracking.
        /// </summary>
        public const float MinCellSize = 0.04f;
        public const float MaxCellSize = 0.09f;

        /// <summary>How close to a detected surface a board let go of by the handle must be to settle onto it, in metres.</summary>
        private const float SettleReach = 0.05f;

        /// <summary>How long locating looks for a surface before the sign suggests the headset's space setup.</summary>
        private const float NoSurfaceHintDelay = 6f;

        /// <summary>The placement sign: how far ahead of the eyes it floats, how far above the gaze, and its text height.</summary>
        private const float SignDistance = 0.8f;
        private const float SignRaise = 0.14f;
        private const float SignTextHeight = 0.028f;

        [SerializeField] private ARPlaneManager planes;
        [SerializeField] private ARAnchorManager anchors;
        [SerializeField] private Material ghostMaterial;

        [Tooltip("World size of one cell, in metres (XR-PRD X6).")]
        [SerializeField] private float cellSize = 0.06f;

        [Tooltip("The ghost's footprint in cells: a 6x6 board with its tunnel ring.")]
        [SerializeField] private Vector2 ghostCells = new Vector2(8f, 8f);

        [Tooltip("Where a floating board goes: this far ahead of the head and this far below it, in metres.")]
        [SerializeField] private float floatDistance = 0.6f;
        [SerializeField] private float floatDrop = 0.5f;

        private readonly List<NearFarInteractor> _interactors = new List<NearFarInteractor>();
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private Transform _ghost;
        private TextMesh _sign;
        private XRBoardHandle _handle;
        private ARAnchor _anchor;
        private bool _locating;
        private bool _surfacesAllowed;
        private bool _aimOnSurface;
        private float _locatingSince;
        private Pose _pose;

        public Transform BoardRoot { get; private set; }
        public bool IsPlaced { get; private set; }

        /// <summary>The size of one cell in the room, in metres: the board root's scale.</summary>
        public float CellSize => BoardRoot != null ? BoardRoot.lossyScale.x : cellSize;

        /// <summary>The board rests on a detected surface, rather than floating where no surface was found.</summary>
        public bool IsOnSurface { get; private set; }

        /// <summary>The handle is moving the board; the board is unanchored until it is let go.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>The bar that moves the board, once it is placed.</summary>
        public XRBoardHandle Handle => _handle;

        public event Action Placed;

        /// <summary><see cref="IsOnSurface"/> changed: the handle settled the board onto a surface, or lifted it off one.</summary>
        public event Action SurfaceChanged;

        private void Awake()
        {
            BoardRoot = new GameObject("Board Root").transform;
            BoardRoot.localScale = Vector3.one * cellSize;
            BoardRoot.gameObject.SetActive(false);
        }

        private async void Start()
        {
            // Let tracking and the AR session settle before reading poses or anchors.
            await Awaitable.WaitForSecondsAsync(1f);
            if (this == null) return;
            if (await TryRestoreAsync())
            {
                // Surfaces stay on after a restore, unseen, so a handle move can settle the board back onto one.
                if (XRScenePermission.IsGranted && planes != null) planes.enabled = true;
                return;
            }

            _surfacesAllowed = await RequestPermissionAsync();
            if (this == null) return;
            if (_surfacesAllowed && planes != null) planes.enabled = true;
            Debug.Log($"[XR placement] Spatial data {(_surfacesAllowed ? "granted" : "refused")}; locating.");
            BeginLocating();
        }

        /// <summary>Forgets the saved spot and runs first placement again ("Re-place board", 5.3).</summary>
        public void Replace()
        {
            ForgetSavedAnchor();
            BoardRoot.SetParent(null, true);
            if (_anchor != null && anchors != null) anchors.TryRemoveAnchor(_anchor);
            _anchor = null;
            BoardRoot.gameObject.SetActive(false);
            IsPlaced = false;
            IsMoving = false;
            BeginLocating();
        }

        // ------------------------------------------------------------------ moving (the handle)

        /// <summary>Lifts the board off its anchor so the handle can carry it.</summary>
        public void BeginMove()
        {
            if (!IsPlaced) return;
            IsMoving = true;
            BoardRoot.SetParent(null, true);
        }

        /// <summary>Carries the board to <paramref name="pose"/> at <paramref name="cell"/> metres to a cell, while the handle holds it.</summary>
        public void MoveTo(Pose pose, float cell)
        {
            if (!IsMoving) return;
            BoardRoot.SetPositionAndRotation(pose.position, pose.rotation);
            // Unparented while it moves, so its local scale is its size in the room.
            BoardRoot.localScale = Vector3.one * Mathf.Clamp(cell, MinCellSize, MaxCellSize);
        }

        /// <summary>
        /// Settles the board onto a detected surface when it was let go close to one, otherwise leaves it floating where
        /// it is, then anchors and saves it there (5.3).
        /// </summary>
        public async void EndMove()
        {
            if (!IsMoving) return;
            IsMoving = false;
            SettleOnSurface();
            await AnchorHereAsync();
        }

        private void SettleOnSurface()
        {
            var wasOnSurface = IsOnSurface;
            var position = BoardRoot.position;
            var onSurface = false;
            if (planes != null && planes.enabled
                && TryHitSurface(new Ray(position + Vector3.up * SettleReach, Vector3.down), out var surface)
                && position.y - surface.y <= SettleReach)
            {
                position.y = surface.y;
                BoardRoot.position = position;
                onSurface = true;
            }

            IsOnSurface = onSurface;
            if (IsOnSurface != wasOnSurface) SurfaceChanged?.Invoke();
        }

        private void Update()
        {
            ShowPlanes(_locating);
            if (!_locating) return;

            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;

            // Floating at waist height unless a detected surface is under the ray (5.2).
            _aimOnSurface = false;
            var centre = head.position + Flat(head.forward) * floatDistance + Vector3.down * floatDrop;
            if (_surfacesAllowed && TryHitSurface(head, out var surfacePoint))
            {
                centre = surfacePoint;
                _aimOnSurface = true;
            }

            // The platform faces the player: its near edge is the one nearest them, and +Z runs away from them.
            var away = Flat(centre - head.position);
            var rotation = Quaternion.LookRotation(away, Vector3.up);
            var nearEdge = centre - rotation * Vector3.forward * (ghostCells.y * cellSize / 2f);
            _pose = new Pose(nearEdge, rotation);
            _ghost.SetPositionAndRotation(nearEdge, rotation);
            ShowSign(head, _aimOnSurface);

            if (SelectPressed()) Place(_pose, _aimOnSurface);
        }

        // ------------------------------------------------------------------ restoring, placing and anchoring

        private async Awaitable<bool> TryRestoreAsync()
        {
            var saved = PlayerPrefs.GetString(AnchorKey, "");
            if (string.IsNullOrEmpty(saved) || !Guid.TryParse(saved, out var guid)) return false;
            if (anchors == null || anchors.subsystem == null || !anchors.descriptor.supportsLoadAnchor) return false;

            var result = await anchors.TryLoadAnchorAsync(new SerializableGuid(guid));
            if (this == null) return true;
            if (!result.status.IsSuccess() || result.value == null)
            {
                // A different room, or the anchor was lost: place it again.
                Debug.Log($"[XR placement] Saved anchor {saved} not found ({result.status}); placing again.");
                ForgetSavedAnchor();
                return false;
            }

            Attach(result.value);
            // The size the handle last scaled it to comes back with it (X6, revised 2026-09-14).
            BoardRoot.localScale = Vector3.one * Mathf.Clamp(PlayerPrefs.GetFloat(CellKey, cellSize), MinCellSize, MaxCellSize);
            IsOnSurface = PlayerPrefs.GetInt(OnSurfaceKey, 0) == 1;
            MarkPlaced();
            Debug.Log($"[XR placement] Board restored at saved anchor {saved} ({(IsOnSurface ? "on a surface" : "floating")}, {CellSize * 100f:F1} cm cells).");
            return true;
        }

        private async void Place(Pose pose, bool onSurface)
        {
            _locating = false;
            _ghost.gameObject.SetActive(false);
            _sign.gameObject.SetActive(false);

            BoardRoot.SetParent(null, false);
            BoardRoot.SetPositionAndRotation(pose.position, pose.rotation);
            IsOnSurface = onSurface;
            MarkPlaced();
            await AnchorHereAsync();
        }

        /// <summary>
        /// Anchors the board where it stands and saves the anchor, erasing the one saved before. Skipped when the handle
        /// picks the board up again before the anchor arrives: the next release anchors it instead.
        /// </summary>
        private async Awaitable AnchorHereAsync()
        {
            if (anchors == null || anchors.subsystem == null) return;
            var added = await anchors.TryAddAnchorAsync(new Pose(BoardRoot.position, BoardRoot.rotation));
            if (this == null) return;
            if (!added.status.IsSuccess() || added.value == null)
            {
                Debug.LogWarning($"[XR placement] Could not anchor the board ({added.status}); it will not come back next time.");
                return;
            }

            if (IsMoving)
            {
                anchors.TryRemoveAnchor(added.value);
                return;
            }

            Attach(added.value);
            ForgetSavedAnchor();
            if (!anchors.descriptor.supportsSaveAnchor) return;
            var saved = await anchors.TrySaveAnchorAsync(added.value);
            if (this == null) return;
            if (saved.status.IsSuccess())
            {
                PlayerPrefs.SetString(AnchorKey, saved.value.guid.ToString());
                PlayerPrefs.SetInt(OnSurfaceKey, IsOnSurface ? 1 : 0);
                PlayerPrefs.SetFloat(CellKey, CellSize);
                PlayerPrefs.Save();
                Debug.Log($"[XR placement] Board anchored and saved as {saved.value.guid} ({CellSize * 100f:F1} cm cells).");
            }
            else
            {
                Debug.LogWarning($"[XR placement] Could not save the board's anchor ({saved.status}).");
            }
        }

        /// <summary>
        /// Hangs the board from <paramref name="anchor"/>. The board moves onto the new anchor before the old one is
        /// removed: removing an anchor destroys its GameObject, and anything still parented to it.
        /// </summary>
        private void Attach(ARAnchor anchor)
        {
            var previous = _anchor;
            _anchor = anchor;
            BoardRoot.SetParent(anchor.transform, false);
            BoardRoot.localPosition = Vector3.zero;
            BoardRoot.localRotation = Quaternion.identity;
            if (previous != null && previous != anchor && anchors != null) anchors.TryRemoveAnchor(previous);
        }

        private void MarkPlaced()
        {
            BoardRoot.gameObject.SetActive(true);
            IsPlaced = true;
            if (_handle == null) _handle = XRBoardHandle.Create(BoardRoot, this);
            Placed?.Invoke();
        }

        private void ForgetSavedAnchor()
        {
            var saved = PlayerPrefs.GetString(AnchorKey, "");
            PlayerPrefs.DeleteKey(AnchorKey);
            PlayerPrefs.Save();
            if (Guid.TryParse(saved, out var guid) && anchors != null && anchors.subsystem != null && anchors.descriptor.supportsEraseAnchor)
                EraseAsync(new SerializableGuid(guid));
        }

        private async void EraseAsync(SerializableGuid guid) => await anchors.TryEraseAnchorAsync(guid);

        private static async Awaitable<bool> RequestPermissionAsync()
        {
            var answer = new AwaitableCompletionSource<bool>();
            XRScenePermission.Request(granted => answer.TrySetResult(granted));
            return await answer.Awaitable;
        }

        // ------------------------------------------------------------------ locating

        private void BeginLocating()
        {
            _locating = true;
            _locatingSince = Time.time;
            _interactors.Clear();
            _interactors.AddRange(FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            // The right hand leads; the left and then the gaze stand in when it is not tracked or misses.
            _interactors.Sort((a, b) => (b.handedness == InteractorHandedness.Right).CompareTo(a.handedness == InteractorHandedness.Right));
            if (_ghost == null) BuildGhost();
            if (_sign == null) BuildSign();
            _ghost.gameObject.SetActive(true);
            _sign.gameObject.SetActive(true);
        }

        /// <summary>The nearest upward-facing detected surface under a hand ray, or under the gaze.</summary>
        private bool TryHitSurface(Transform head, out Vector3 point)
        {
            foreach (var interactor in _interactors)
            {
                if (!interactor.isActiveAndEnabled || interactor.curveOrigin == null) continue;
                var origin = interactor.curveOrigin;
                if (TryHitSurface(new Ray(origin.position, origin.forward), out point)) return true;
            }

            return TryHitSurface(new Ray(head.position, head.forward), out point);
        }

        private bool TryHitSurface(Ray ray, out Vector3 point)
        {
            point = default;
            var nearest = float.MaxValue;
            var count = Physics.RaycastNonAlloc(ray, _hits, MaxRayDistance, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var plane = _hits[i].collider.GetComponentInParent<ARPlane>();
                if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp || _hits[i].distance >= nearest) continue;
                nearest = _hits[i].distance;
                point = _hits[i].point;
            }

            return nearest < float.MaxValue;
        }

        private bool SelectPressed()
        {
            foreach (var interactor in _interactors)
                if (interactor.isActiveAndEnabled && interactor.selectInput != null && interactor.selectInput.ReadWasPerformedThisFrame())
                    return true;
            return false;
        }

        /// <summary>Detected surfaces show only while the player is choosing one.</summary>
        private void ShowPlanes(bool visible)
        {
            if (planes == null || !planes.enabled) return;
            foreach (var plane in planes.trackables)
                if (plane.TryGetComponent<MeshRenderer>(out var renderer))
                    renderer.enabled = visible;
        }

        private static Vector3 Flat(Vector3 direction)
        {
            var flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            return flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
        }

        // ------------------------------------------------------------------ ghost and sign

        private void BuildGhost()
        {
            _ghost = new GameObject("Placement Ghost").transform;
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            Destroy(slab.GetComponent<Collider>());
            slab.transform.SetParent(_ghost, false);
            var width = ghostCells.x * cellSize;
            var depth = ghostCells.y * cellSize;
            slab.transform.localPosition = new Vector3(0f, 0.005f, depth / 2f);
            slab.transform.localScale = new Vector3(width, 0.01f, depth);
            var renderer = slab.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (ghostMaterial != null) renderer.sharedMaterial = ghostMaterial;
        }

        /// <summary>
        /// The one thing the XR edition ever attaches to the head, and only while placing (XR-PRD 8). The copy is
        /// English until XR10 gives XR its String Table.
        /// </summary>
        private void BuildSign()
        {
            var go = new GameObject("Placement Sign");
            _sign = go.AddComponent<TextMesh>();
            _sign.font = XRPalette.Font;
            _sign.fontSize = 64;
            _sign.characterSize = SignTextHeight / 6.4f;
            _sign.anchor = TextAnchor.MiddleCenter;
            _sign.alignment = TextAlignment.Center;
            _sign.color = XRPalette.Paper;
            go.GetComponent<MeshRenderer>().sharedMaterial = XRPalette.Font.material;
        }

        private void ShowSign(Transform head, bool onSurface)
        {
            var forward = Flat(head.forward);
            _sign.transform.SetPositionAndRotation(head.position + forward * SignDistance + Vector3.up * SignRaise,
                Quaternion.LookRotation(forward, Vector3.up));
            _sign.text = onSurface
                ? "Pinch to put the board here"
                : !_surfacesAllowed || Time.time - _locatingSince > NoSurfaceHintDelay
                    ? "No table found. Pinch to place the board here,\nor run Space Setup in the headset settings."
                    : "Look at a table";
        }
    }
}
