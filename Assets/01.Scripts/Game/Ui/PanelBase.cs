using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// One uGUI screen. <see cref="Build"/> creates the widgets (in the Editor, saved with the scene, or at runtime as
    /// a fallback) and stores them in serialized fields; <see cref="Bind"/> attaches the runtime handlers. The
    /// <see cref="GameManager"/> shows the panels whose <see cref="IsVisibleIn"/> matches the flow state.
    /// </summary>
    public abstract class PanelBase : MonoBehaviour
    {
        [SerializeField, HideInInspector] private bool built;

        protected GameManager Game { get; private set; }
        protected GameFlow Flow => Game.Flow;
        protected RectTransform Root => (RectTransform)transform;

        public bool IsBuilt => built;

        /// <summary>Creates the widgets, replacing any earlier ones, and leaves the panel hidden.</summary>
        public void Build()
        {
            UiBuilder.Clear(transform);
            BuildWidgets();
            built = true;
            gameObject.SetActive(false);
        }

        /// <summary>Runtime setup: builds if the scene was never generated, then wires the handlers.</summary>
        public void Bind(GameManager game)
        {
            Game = game;
            if (!built) Build();
            Wire();
        }

        protected abstract void BuildWidgets();

        /// <summary>Attach click handlers and apply anything that depends on the runtime platform.</summary>
        protected abstract void Wire();

        public abstract bool IsVisibleIn(GameState state);

        /// <summary>Called on every state change while the panel is visible, right after it is shown.</summary>
        public virtual void Refresh(GameState state)
        {
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
