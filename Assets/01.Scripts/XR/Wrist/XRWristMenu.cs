using System;
using System.Collections.Generic;
using TrainSudoku.XR.Rules;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using static TrainSudoku.XR.XRSignageUi;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The wrist menu (XR-PRD 6.4): a small roundel worn on the back of the non-dominant wrist, where a watch would be,
    /// and the compact panel it opens above the wrist, carrying pause and settings. The left controller's menu button
    /// opens the same panel (4.5).
    /// </summary>
    /// <remarks>
    /// The roundel shows only while the back of the wrist is turned to the eyes (<see cref="WatchCheck"/>), so a hand at
    /// work on the board never shows it, and it stays off the palm, which both headsets keep for system gestures. It is
    /// pressed with the other hand's fingertip, like the signboard's buttons; there are no rays (<see cref="XRTouchOnly"/>).
    /// A roundel just turned into view takes no press for <see cref="RoundelSettleSeconds"/>, and a fingertip must be
    /// ready over it first: turning the wrist must never press it against the other hand.
    ///
    /// The wrist's own fingertips press nothing on the roundel, and the panel ignores the hand it opened over for as long
    /// as it is open, even if the settings move the roundel to the other wrist meanwhile.
    ///
    /// The panel stands where it opened, world-locked and turned to the eyes, rather than riding the wrist: a panel that
    /// follows one hand moves under the other hand's finger. It closes when its choice takes the flow elsewhere, when the
    /// roundel or the menu button is pressed again, or when the player walks away from it.
    ///
    /// Both documents stay switched on and are hidden through their root's visibility (<see cref="SetVisible"/>).
    ///
    /// It shows and asks; the shell (<see cref="XRGame"/>) decides. Pressing the roundel raises <see cref="Toggled"/>, an
    /// entry raises <see cref="Chosen"/>, and the settings that act on the board raise their own events. Dominant hand,
    /// volumes and language are written straight to <see cref="XRPreferences"/>. Copy is English until XR10.
    /// </remarks>
    public sealed class XRWristMenu : MonoBehaviour
    {
        private const float PixelsPerUnit = 100f;

        /// <summary>The roundel: its size in UI pixels and in the room, in metres.</summary>
        private const float RoundelPixels = 160f;
        private const float RoundelMetres = 0.03f;

        /// <summary>
        /// Where the roundel sits: this far out of the back of the wrist, and this far back from the wrist joint towards
        /// the elbow, in metres. XR Hands follows OpenXR: a joint's up is out of the back of the hand, its forward is
        /// towards the fingertips.
        /// </summary>
        private const float RoundelLift = 0.03f;
        private const float RoundelTowardElbow = 0.015f;

        /// <summary>A fingertip still presses the roundel this far outside it: it rides a moving wrist.</summary>
        private const float RoundelSlop = 0.008f;

        /// <summary>A fingertip is ready to press the roundel only this close outside its edge, in metres: over it, near enough.</summary>
        private const float RoundelArmMargin = 0.015f;

        /// <summary>How long the roundel shows before it takes a press, in seconds: the wrist turning it into view is not a press.</summary>
        private const float RoundelSettleSeconds = 0.3f;

        /// <summary>The panel: its width in UI pixels and in the room, and how far above the roundel its bottom edge opens.</summary>
        private const float PanelWidth = 640f;
        private const float PanelMetres = 0.2f;
        private const float PanelAbove = 0.06f;

        /// <summary>Where the panel opens with neither a wrist nor a controller under it: ahead of and below the eyes.</summary>
        private const float HeadDistance = 0.45f;
        private const float HeadDrop = 0.2f;

        /// <summary>The panel closes once the head is this far from it, in metres.</summary>
        private const float WalkAway = 1.2f;

        /// <summary>
        /// One touch of the roundel, or of the menu button, counts once: a second press within this many seconds is the
        /// first seen again. A fingertip on a moving wrist can draw back past the arming gap and come down again, which
        /// the first XR8 headset check saw as a double press at the old 0.35 s.
        /// </summary>
        private const float PressCooldown = 1f;

        /// <summary>The panel's layout, in UI pixels: a row is 84 px, 2.6 cm, for a fingertip.</summary>
        private const float Padding = 28f;
        private const float HeaderHeight = 76f;
        private const float RowHeight = 84f;
        private const float RowGap = 12f;
        private const float CaptionWidth = 190f;

        /// <summary>One press of the height nudge, in metres.</summary>
        public const float NudgeStep = 0.01f;

        private static readonly List<XRHandSubsystem> Subsystems = new List<XRHandSubsystem>();

        private XRSignageAssets _assets;
        private XRPreferences _preferences;
        private UIDocument _roundel;
        private UIDocument _panel;
        private readonly XRPanelTouch _roundelTouch = new XRPanelTouch("wrist roundel", PixelsPerUnit, RoundelSlop, RoundelArmMargin);
        private readonly XRPanelTouch _panelTouch = new XRPanelTouch("wrist menu", PixelsPerUnit);
        private Hand _wristHand = Hand.Left;
        private readonly WatchCheck _watch = new WatchCheck();
        private InputAction _menuButton;
        private float _lastPress = float.NegativeInfinity;
        private IReadOnlyList<WristItem> _items = Array.Empty<WristItem>();
        private string _title = "";
        private bool _offered;

        /// <summary>Whether the roundel is worn and the panel open, and since when the roundel has shown.</summary>
        private bool _roundelShown;
        private bool _open;
        private float _roundelShownAt;

        /// <summary>What each document's root was last set to; null until it has been set.</summary>
        private bool? _roundelVisible;
        private bool? _panelVisible;

        private XRHandSubsystem _hands;
        private XROrigin _origin;
        private XRInputModalityManager _modality;

        /// <summary>The roundel or the menu button was pressed.</summary>
        public event Action Toggled;

        /// <summary>An entry other than Settings was chosen; Settings is the menu's own page.</summary>
        public event Action<WristItem> Chosen;

        public event Action ReplaceBoard;

        /// <summary>The height nudge, in metres: up is positive.</summary>
        public event Action<float> NudgeBoard;

        /// <summary>The wrist the roundel is worn on. Its fingertips press nothing on the roundel.</summary>
        public Hand WristHand
        {
            get => _wristHand;
            set
            {
                // The other wrist has to be turned into view from scratch.
                if (value != _wristHand) _watch.Reset();
                _wristHand = value;
                // The open panel keeps the hand it opened over (Open). Choosing LEFT at the second XR8 check swapped it
                // under the finger that chose, and that finger pressed nothing more.
                _roundelTouch.IgnoredHand = Handed(value);
            }
        }

        public bool IsOpen => _open;

        /// <summary>Whether the flow has a menu to offer: the roundel hides, and an open panel closes, while it does not.</summary>
        public bool Offered
        {
            get => _offered;
            set
            {
                if (value == _offered) return;
                _offered = value;
                if (!value) Close();
            }
        }

        public static XRWristMenu Create(XRSignageAssets assets, XRPreferences preferences)
        {
            var go = new GameObject("Wrist Menu");
            var menu = go.AddComponent<XRWristMenu>();
            menu._assets = assets;
            menu._preferences = preferences;
            menu._roundel = menu.Document("Roundel", RoundelPixels, RoundelMetres);
            menu._panel = menu.Document("Panel", PanelWidth, PanelMetres);
            SetVisible(menu._roundel, ref menu._roundelVisible, false);
            SetVisible(menu._panel, ref menu._panelVisible, false);
            return menu;
        }

        /// <summary>A world-space document <paramref name="metres"/> wide, pivoted at its bottom centre.</summary>
        private UIDocument Document(string name, float widthPixels, float metres)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (metres / (widthPixels / PixelsPerUnit));
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = _assets != null ? _assets.PanelSettings : null;
            document.worldSpaceSizeMode = WorldSpaceSizeMode.Fixed;
            document.worldSpaceSize = new Vector2(widthPixels, widthPixels);
            document.pivot = Pivot.BottomCenter;
            return document;
        }

        /// <summary>
        /// Shows or hides a document through its root's visibility. The GameObject stays on: the roundel switched back
        /// on showed for a frame or two at a giant scale in the room behind the wrist (the second XR8 headset check), and
        /// a document switched off loses what was built into it.
        /// </summary>
        private static void SetVisible(UIDocument document, ref bool? visible, bool show)
        {
            if (visible == show || document == null) return;
            var root = document.rootVisualElement;
            // Not attached to its panel yet: the next frame tries again.
            if (root == null) return;
            root.style.visibility = show ? Visibility.Visible : Visibility.Hidden;
            visible = show;
        }

        private static InteractorHandedness Handed(Hand hand) => hand == Hand.Left ? InteractorHandedness.Left : InteractorHandedness.Right;

        private void OnEnable()
        {
            _menuButton = new InputAction("Wrist Menu", InputActionType.Button);
            // The left controller's menu button (4.5); the right one's belongs to the system.
            _menuButton.AddBinding("<XRController>{LeftHand}/{MenuButton}");
#if UNITY_EDITOR
            // Testing without a headset.
            _menuButton.AddBinding("<Keyboard>/escape");
#endif
            _menuButton.performed += OnMenuButton;
            _menuButton.Enable();
        }

        private void OnDisable()
        {
            if (_menuButton == null) return;
            _menuButton.performed -= OnMenuButton;
            _menuButton.Dispose();
            _menuButton = null;
        }

        private void OnMenuButton(InputAction.CallbackContext context) =>
            Press($"the menu button ({(context.control != null ? context.control.path : "?")})");

        /// <summary>The roundel or the menu button, pressed by <paramref name="source"/>, which the log names.</summary>
        private void Press(string source)
        {
            if (Time.unscaledTime - _lastPress < PressCooldown)
            {
                Debug.Log($"[XR wrist] Pressed by {source}, within the cooldown: ignored");
                return;
            }

            _lastPress = Time.unscaledTime;
            Debug.Log($"[XR wrist] Pressed by {source}");
            Toggled?.Invoke();
        }

        // ------------------------------------------------------------------ opening and closing

        /// <summary>Opens the panel over the wrist, or the controller, or ahead of the eyes, with <paramref name="items"/> under <paramref name="title"/>.</summary>
        public void Open(IReadOnlyList<WristItem> items, string title)
        {
            _items = items ?? Array.Empty<WristItem>();
            _title = title ?? "";
            _panelTouch.IgnoredHand = PlacePanel();
            // The fingertip that pressed the roundel starts afresh here too.
            _panelTouch.Disarm();
            ShowMain();
        }

        public void Close()
        {
            if (_panel == null) return;
            _panelTouch.Clear();
            _open = false;
            SetVisible(_panel, ref _panelVisible, false);
        }

        /// <summary>
        /// Stands the panel's bottom edge above the roundel if it shows, else above the left controller, else ahead of the
        /// eyes, turned to face them. It grows upward from there, so the taller settings page keeps its footing. Returns
        /// the hand it opened over, whose fingertips it ignores while it is open.
        /// </summary>
        private InteractorHandedness PlacePanel()
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            var eyes = head != null ? head.position : Vector3.zero;
            Vector3 bottom;
            InteractorHandedness over;
            if (_roundelShown)
            {
                bottom = _roundel.transform.TransformPoint(0f, RoundelPixels / PixelsPerUnit / 2f, 0f) + Vector3.up * PanelAbove;
                over = Handed(WristHand);
            }
            else if (TryController(out var controller))
            {
                bottom = controller + Vector3.up * PanelAbove;
                over = InteractorHandedness.Left;
            }
            else
            {
                var forward = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up) : Vector3.forward;
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
                bottom = eyes + forward.normalized * HeadDistance + Vector3.down * HeadDrop;
                over = InteractorHandedness.None;
            }

            var away = bottom + Vector3.up * (PanelMetres / 2f) - eyes;
            _panel.transform.SetPositionAndRotation(bottom, Quaternion.LookRotation(away.sqrMagnitude > 1e-6f ? away : Vector3.forward, Vector3.up));
            return over;
        }

        // ------------------------------------------------------------------ every frame

        private void LateUpdate()
        {
            UpdateRoundel();
            // Also retries a document that was not attached to its panel when it was first hidden.
            SetVisible(_roundel, ref _roundelVisible, _roundelShown);
            SetVisible(_panel, ref _panelVisible, _open);
            if (_roundelShown) _roundelTouch.Update(_roundel.transform, _roundel, Time.unscaledTime - _roundelShownAt >= RoundelSettleSeconds);
            if (!_open) return;

            var head = Camera.main;
            if (head != null && Vector3.Distance(head.transform.position, _panel.transform.position) > WalkAway)
            {
                Close();
                return;
            }

            _panelTouch.Update(_panel.transform, _panel);
        }

        /// <summary>Wears the roundel on the wrist while the menu is offered and the back of the wrist is turned to the eyes.</summary>
        private void UpdateRoundel()
        {
            var head = Camera.main;
            var show = false;
            if (_offered && head != null && TryWrist(WristHand, out var wrist))
            {
                var back = wrist.up;
                var centre = wrist.position + back * RoundelLift - wrist.forward * RoundelTowardElbow;
                var toEyes = head.transform.position - centre;
                show = _watch.Update(true, (back.x, back.y, back.z), (toEyes.x, toEyes.y, toEyes.z));
                if (show) PlaceRoundel(centre, back, head.transform);
            }
            else
            {
                _watch.Reset();
            }

            if (show == _roundelShown) return;
            _roundelShown = show;
            if (!show) return;

            // Built once: the document stays on, so it keeps it.
            var root = _roundel.rootVisualElement;
            if (root != null && root.childCount == 0) BuildRoundel();
            _roundelShownAt = Time.unscaledTime;
            _roundelTouch.Disarm();
        }

        /// <summary>Its face looks out of the back of the wrist, upright to the viewer; the document's pivot is its bottom centre.</summary>
        private void PlaceRoundel(Vector3 centre, Vector3 back, Transform head)
        {
            var up = Vector3.ProjectOnPlane(head.up, back);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, back);
            if (up.sqrMagnitude < 1e-6f) return;
            // The player's side of a document is -z, so +z points into the wrist.
            var rotation = Quaternion.LookRotation(-back, up.normalized);
            _roundel.transform.SetPositionAndRotation(centre - rotation * Vector3.up * (RoundelMetres / 2f), rotation);
        }

        private void BuildRoundel()
        {
            _roundelTouch.Clear();
            var root = _roundel.rootVisualElement;
            root.Clear();
            // Its click is only the Editor's way in: on the headset a fingertip presses it (_roundelTouch).
            var button = new Button(() => Press("a UI click on the roundel")) { text = "" };
            button.style.flexGrow = 1;
            button.style.marginLeft = button.style.marginRight = button.style.marginTop = button.style.marginBottom = 0f;
            button.style.paddingLeft = button.style.paddingRight = button.style.paddingTop = button.style.paddingBottom = 0f;
            button.style.backgroundColor = XRPalette.Paper;
            button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 10f;
            button.style.borderTopColor = button.style.borderBottomColor = button.style.borderLeftColor = button.style.borderRightColor = XRPalette.Ink;
            Round(button, RoundelPixels / 2f);
            if (_assets != null && _assets.Mark != null) button.style.backgroundImage = _assets.Mark;
            root.Add(button);
            _roundelTouch.Add(button, () => Press("a fingertip on the roundel"));
        }

        // ------------------------------------------------------------------ where the hands are

        /// <summary>The wrist joint of <paramref name="hand"/> in world space, from XR Hands, while it is tracked.</summary>
        private bool TryWrist(Hand hand, out Pose pose)
        {
            pose = default;
            if (_hands == null || !_hands.running)
            {
                _hands = null;
                SubsystemManager.GetSubsystems(Subsystems);
                foreach (var subsystem in Subsystems)
                    if (subsystem.running)
                    {
                        _hands = subsystem;
                        break;
                    }

                if (_hands == null) return false;
            }

            var tracked = hand == Hand.Left ? _hands.leftHand : _hands.rightHand;
            if (!tracked.isTracked || !tracked.GetJoint(XRHandJointID.Wrist).TryGetPose(out var local)) return false;

            // Joint poses are in the session's space, which is the rig's camera offset: the head's pose driver writes there too.
            if (_origin == null) _origin = FindFirstObjectByType<XROrigin>();
            var space = _origin != null && _origin.CameraFloorOffsetObject != null ? _origin.CameraFloorOffsetObject.transform : null;
            pose = space != null ? local.GetTransformedBy(space) : local;
            return true;
        }

        /// <summary>The left controller, while controllers are what the player holds.</summary>
        private bool TryController(out Vector3 position)
        {
            position = default;
            if (XRInputModalityManager.currentInputMode.Value != XRInputModalityManager.InputMode.MotionController) return false;
            if (_modality == null) _modality = FindFirstObjectByType<XRInputModalityManager>();
            if (_modality == null || _modality.leftController == null) return false;
            position = _modality.leftController.transform.position;
            return true;
        }

        // ------------------------------------------------------------------ the pages

        private void ShowMain()
        {
            var card = Begin(_items.Count);
            Header(card, _title);
            foreach (var item in _items)
            {
                var chosen = item;
                Key(Line(card), Wording(item), item == WristItem.Settings ? (Action)ShowSettings : () => Chosen?.Invoke(chosen), item == WristItem.Resume);
            }
        }

        /// <summary>Settings (6.4): dominant hand, the board's height, re-placing it, language, and music and effects volume.</summary>
        private void ShowSettings()
        {
            var card = Begin(7);
            Header(card, "SETTINGS");

            var hand = Line(card);
            Caption(hand, "HAND");
            Key(hand, "LEFT", () => SetHand(Hand.Left), _preferences.DominantHand == Hand.Left);
            Key(hand, "RIGHT", () => SetHand(Hand.Right), _preferences.DominantHand == Hand.Right);

            var height = Line(card);
            Caption(height, "BOARD");
            Key(height, "LOWER", () => NudgeBoard?.Invoke(-NudgeStep), false);
            Key(height, "RAISE", () => NudgeBoard?.Invoke(NudgeStep), false);

            Key(Line(card), "RE-PLACE BOARD", () => ReplaceBoard?.Invoke(), false);

            var language = Line(card);
            Caption(language, "LANGUAGE");
            var code = XRLocale.CurrentCode;
            Key(language, string.IsNullOrEmpty(code) ? "SYSTEM" : code.ToUpperInvariant(), () =>
            {
                XRLocale.Next(_preferences);
                ShowSettings();
            }, false);

            Volume(Line(card), "MUSIC", () => _preferences.MusicVolume, delta => _preferences.StepMusic(delta));
            Volume(Line(card), "EFFECTS", () => _preferences.EffectsVolume, delta => _preferences.StepEffects(delta));

            Key(Line(card), "‹  BACK", ShowMain, false);
        }

        private void SetHand(Hand hand)
        {
            _preferences.DominantHand = hand;
            ShowSettings();
        }

        private void Volume(VisualElement line, string caption, Func<int> value, Func<int, int> step)
        {
            Caption(line, caption);
            Key(line, "-", () =>
            {
                step(-1);
                ShowSettings();
            }, false);
            var level = XRSignageUi.Text(_assets, value().ToString(), 50f, true, XRPalette.Ink, 2f);
            level.style.width = 90f;
            level.style.flexShrink = 0;
            level.style.marginLeft = RowGap;
            level.style.unityTextAlign = TextAnchor.MiddleCenter;
            line.Add(level);
            Key(line, "+", () =>
            {
                step(1);
                ShowSettings();
            }, false);
        }

        private static string Wording(WristItem item)
        {
            switch (item)
            {
                case WristItem.Resume: return "RESUME";
                case WristItem.Retry: return "RETRY";
                case WristItem.BackToMap: return "LINE MAP";
                case WristItem.BackToNetwork: return "NETWORK";
                case WristItem.Settings: return "SETTINGS";
                case WristItem.Close: return "CLOSE";
                default: return item.ToString().ToUpperInvariant();
            }
        }

        // ------------------------------------------------------------------ building blocks

        /// <summary>Shows the panel at the height of <paramref name="lines"/> rows under the header, and clears it for a new page.</summary>
        private VisualElement Begin(int lines)
        {
            _panelTouch.Clear();
            _panel.worldSpaceSize = new Vector2(PanelWidth, Padding * 2f + HeaderHeight + lines * (RowHeight + RowGap));
            _open = true;
            SetVisible(_panel, ref _panelVisible, true);
            var root = _panel.rootVisualElement;
            root.Clear();
            return Card(root, 8f, 28f, Padding, Padding);
        }

        private void Header(VisualElement card, string title)
        {
            var strip = Led(card);
            strip.style.height = HeaderHeight;
            strip.style.flexShrink = 0;
            strip.style.paddingTop = strip.style.paddingBottom = 0f;
            strip.Add(XRSignageUi.Text(_assets, title, 46f, true, XRPalette.Led, 6f));
        }

        private static VisualElement Line(VisualElement card)
        {
            var line = Row(card);
            line.style.height = RowHeight;
            line.style.flexShrink = 0;
            line.style.marginTop = RowGap;
            line.style.alignItems = Align.Stretch;
            return line;
        }

        /// <summary>A button filling its share of a row, registered for a fingertip.</summary>
        private void Key(VisualElement line, string text, Action clicked, bool primary)
        {
            var button = XRSignageUi.Button(_assets, text, clicked, primary);
            button.style.flexGrow = 1;
            button.style.flexBasis = 0f;
            button.style.marginLeft = line.childCount > 0 ? RowGap : 0f;
            button.style.marginTop = button.style.marginBottom = button.style.marginRight = 0f;
            button.style.paddingTop = button.style.paddingBottom = 0f;
            button.style.paddingLeft = button.style.paddingRight = 12f;
            button.style.fontSize = 42f;
            line.Add(button);
            _panelTouch.Add(button, clicked);
        }

        private void Caption(VisualElement line, string text)
        {
            var label = XRSignageUi.Text(_assets, text, 38f, true, XRPalette.InkDim, 4f);
            label.style.width = CaptionWidth;
            label.style.flexShrink = 0;
            line.Add(label);
        }
    }
}
