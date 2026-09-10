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
    /// <see cref="Refresh"/>.</item>
    /// </list>
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public abstract class UiScreen : MonoBehaviour
    {
        [Tooltip("Left empty, the screen finds the one shell in the scene.")]
        [SerializeField] private UiShell shell;

        private UIDocument _document;

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
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        protected virtual void OnDisable()
        {
            if (shell != null && Root != null) shell.Unregister(Root);
        }
    }
}
