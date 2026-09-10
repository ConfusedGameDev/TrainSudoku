using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Scene housekeeping that has nothing to do with widgets: tearing down generated GameObjects in either edit
    /// mode or play mode, and making sure the scene has an EventSystem. The board, the train and the game manager
    /// all rebuild their children this way.
    /// </summary>
    /// <remarks>
    /// This is what is left of the old <c>UiBuilder</c>. Its widget half went with the uGUI panels; these two jobs
    /// were never about widgets and outlived it.
    /// </remarks>
    public static class SceneObjects
    {
        /// <summary>Destroys a GameObject in play mode or edit mode.</summary>
        public static void Destroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        /// <summary>Destroys every child of <paramref name="parent"/>, leaving the parent itself.</summary>
        public static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        /// <summary>
        /// The scene's EventSystem driven by the Input System, created under <paramref name="parent"/> if none
        /// exists. <b>UI Toolkit still needs it:</b> the runtime panel registers a PanelRaycaster with the
        /// EventSystem, which is what lets <c>BoardView.IsOverUi</c> tell a tap on a button from a tap on the board.
        /// </summary>
        public static EventSystem EnsureEventSystem(InputActionAsset actions, Transform parent)
        {
            var existing = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(parent, false);
            var module = go.AddComponent<InputSystemUIInputModule>();
            if (actions != null) module.actionsAsset = actions;
            return go.GetComponent<EventSystem>();
        }
    }
}
