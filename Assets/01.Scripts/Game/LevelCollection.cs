using System.Collections.Generic;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>The ordered list of levels the game offers (PRD section 9.1). Level Select and unlocking follow this order.</summary>
    [CreateAssetMenu(menuName = "TrainSudoku/Level Collection", fileName = "LevelCollection")]
    public sealed class LevelCollection : ScriptableObject
    {
        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();

        public IReadOnlyList<LevelDefinition> Levels => levels;
        public int Count => levels.Count;
        public LevelDefinition this[int index] => levels[index];

        public int IndexOf(LevelDefinition level) => levels.IndexOf(level);
        public bool Contains(LevelDefinition level) => level != null && levels.Contains(level);

        /// <summary>Appends a level. Returns false when it is null or already listed. Callers in the editor record undo and mark the asset dirty.</summary>
        public bool Add(LevelDefinition level)
        {
            if (level == null || levels.Contains(level)) return false;
            levels.Add(level);
            return true;
        }

        public bool Remove(LevelDefinition level) => levels.Remove(level);
    }
}
