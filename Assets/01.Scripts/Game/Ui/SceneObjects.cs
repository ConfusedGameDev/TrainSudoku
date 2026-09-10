using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Scene housekeeping that has nothing to do with widgets: tearing down generated GameObjects in either edit
    /// mode or play mode. The board, the train and the game manager all rebuild their children this way, so it
    /// lives apart from <see cref="UiBuilder"/> and survives the move to UI Toolkit.
    /// </summary>
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
    }
}
