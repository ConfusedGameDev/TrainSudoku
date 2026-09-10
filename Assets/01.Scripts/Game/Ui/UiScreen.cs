using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// One UI Toolkit screen. The UI Toolkit counterpart of <see cref="PanelBase"/>, carrying the same public
    /// surface the uGUI panels had — <c>Build</c>, <c>Bind</c>, <c>IsVisibleIn</c>, <c>Refresh</c>,
    /// <c>SetVisible</c>, <c>IsBuilt</c> — so <see cref="GameManager"/>'s state loop did not have to be rewritten.
    /// </summary>
    /// <remarks>
    /// It landed alongside the old uGUI <c>PanelBase</c> at M12 rather than replacing it, so the game stayed playable
    /// through M13 and M14 — M14 needed real play to read star times out of <c>save.json</c>. At M16 the seven
    /// screens, the six old panels, <c>PanelBase</c> and the uGUI widget factory all went together, which is what 4.2
    /// asks for: "delete the old panels only after the replacements compile".
    ///
    /// Two deliberate differences from <see cref="PanelBase"/>:
    /// <list type="bullet">
    /// <item><b>Nothing is baked into the scene.</b> uGUI needed a Generate Scene Objects step because widgets are
    /// GameObjects with serialized references. A UI Toolkit tree is built in code at runtime, so the
    /// <c>BuildTree</c>/<c>Wire</c> split exists only to keep the shape familiar — both run at runtime and lambdas
    /// are fine.</item>
    /// <item><b><see cref="SetVisible"/> toggles display, not the GameObject</b>, so a hidden screen still receives
    /// <see cref="Refresh"/>, and since M19 it does so through the 260 ms screen change of work order 9: the
    /// incoming screen slides up from beneath the LED strip while the outgoing one drops 40 px and fades.</item>
    /// </list>
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public abstract class UiScreen : MonoBehaviour
    {
        [Tooltip("Left empty, the screen finds the one shell in the scene.")]
        [SerializeField] private UiShell shell;

        /// <summary>How far the incoming screen starts below its resting place: the height of the LED strip.</summary>
        private const float EnterFrom = PlayScreen.BottomBarHeight;

        /// <summary>How far a dismissed screen drops as it fades (work order 9).</summary>
        private const float ExitDrop = 40f;

        /// <summary>On the root while it fades out, so the disabled dimming in components.uss stays out of the fade.</summary>
        private const string LeavingClass = "screen--leaving";

        private UIDocument _document;
        private IVisualElementScheduledItem _transition;
        private bool? _shown;

        /// <summary>
        /// The root's own picking mode, remembered so the exit transition can put it back.
        /// </summary>
        /// <remarks>
        /// <b>Never assume this is <see cref="PickingMode.Position"/>.</b> A <see cref="UIDocument"/> hands out a
        /// root that ignores picking, and that is exactly what lets a tap fall through the transparent Play screen
        /// to the 3D board: <c>BoardView.BeginPress</c> asks <c>EventSystem.IsPointerOverGameObject</c> first, and a
        /// full-screen root that picks would answer yes everywhere and swallow every placement.
        /// </remarks>
        private PickingMode _rootPicking = PickingMode.Ignore;

        /// <summary>The shell this screen registers with. There is exactly one in the scene.</summary>
        protected UiShell Shell
        {
            get
            {
                if (shell == null) shell = FindAnyObjectByType<UiShell>(FindObjectsInactive.Include);
                return shell;
            }
        }

        protected GameManager Game { get; private set; }
        protected GameFlow Flow => Game.Flow;

        /// <summary>The screen's root. Null until the <see cref="UIDocument"/> has been enabled.</summary>
        protected VisualElement Root => Document != null ? Document.rootVisualElement : null;

        protected UIDocument Document
        {
            get
            {
                if (_document == null) _document = GetComponent<UIDocument>();
                return _document;
            }
        }

        public bool IsBuilt { get; private set; }

        /// <summary>
        /// Whether this screen slides in and out. True everywhere except the Play frame, which is not a screen the
        /// player moves between so much as the surround the board lives in: it is up across Play, Pause, the train
        /// run and Arrival, and sliding it would both move the board's frame while the board sat still and hand the
        /// camera a set of insets measured mid-transform.
        /// </summary>
        protected virtual bool Animates => true;

        /// <summary>Creates the visual tree, replacing any earlier one, and leaves the screen hidden.</summary>
        public void Build()
        {
            var root = Root;
            if (root == null) return; // the document is not enabled yet; Bind builds again once it is

            root.Clear();
            root.style.flexGrow = 1f;
            // The token sheet must be in place before the tree is built so USS classes resolve during layout, and
            // the tint pass must run after it, because it can only find elements that already exist.
            if (Shell != null) Shell.Register(root);
            BuildTree(root);
            if (Shell != null) Shell.ApplyLineColour(root);
            IsBuilt = true;
            SetVisible(false);
        }

        /// <summary>Runtime setup: registers with the shell, builds if needed, then wires the handlers.</summary>
        public void Bind(GameManager game)
        {
            Game = game;
            if (Document != null && Shell != null && Document.panelSettings == null)
                Document.panelSettings = Shell.PanelSettings;

            if (!IsBuilt) Build();
            if (IsBuilt) Wire();
        }

        /// <summary>Create the elements. Runs at runtime, so closures over fields are safe.</summary>
        protected abstract void BuildTree(VisualElement root);

        /// <summary>Attach handlers and anything that depends on the runtime platform.</summary>
        protected abstract void Wire();

        public abstract bool IsVisibleIn(GameState state);

        /// <summary>Called on every state change while the screen is visible, right after it is shown.</summary>
        public virtual void Refresh(GameState state)
        {
        }

        public void SetVisible(bool visible)
        {
            var root = Root;
            if (root == null) return;
            if (_shown.HasValue && _shown.Value == visible) return;

            // The first call is the one Build makes to hide a freshly built screen: there is nothing to animate away
            // from, and animating it would show a screen the flow never asked for.
            var abrupt = !_shown.HasValue || !Animates;
            if (!_shown.HasValue) _rootPicking = root.pickingMode;
            _shown = visible;
            _transition?.Pause();
            _transition = null;

            if (abrupt)
            {
                Settle(root, visible, _rootPicking);
                return;
            }

            if (visible)
            {
                root.style.display = DisplayStyle.Flex;
                _transition = Motion.Play(root, Motion.ScreenChange, t =>
                {
                    var e = Motion.EaseOut(t);
                    root.style.opacity = e;
                    root.style.translate = new Translate(0f, Mathf.Lerp(EnterFrom, 0f, e));
                }, () => Settle(root, true, _rootPicking));
                return;
            }

            // A screen on its way out must stop taking taps at once, or a button can still be hit while it fades and
            // GameFlow throws on a transition it did not expect. Its children are what take the taps, so disabling
            // the subtree is what stops them; the class keeps that from dimming anything on the way past.
            root.AddToClassList(LeavingClass);
            root.SetEnabled(false);
            _transition = Motion.Play(root, Motion.ScreenChange, t =>
            {
                var e = Motion.EaseIn(t);
                root.style.opacity = 1f - e;
                root.style.translate = new Translate(0f, ExitDrop * e);
            }, () => Settle(root, false, _rootPicking));
        }

        /// <summary>Puts the root in its resting state, shown or hidden, with no transform or fade left over.</summary>
        private static void Settle(VisualElement root, bool visible, PickingMode picking)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            root.style.opacity = 1f;
            root.style.translate = new Translate(0f, 0f);
            root.pickingMode = picking;
            root.SetEnabled(true);
            root.RemoveFromClassList(LeavingClass);
        }

        protected virtual void OnDisable()
        {
            if (shell != null && Root != null) shell.Unregister(Root);
        }
    }
}
