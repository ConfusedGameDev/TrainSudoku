using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>Marks a cell's collider so a raycast hit can be mapped back to board coordinates.</summary>
    public sealed class BoardCell : MonoBehaviour
    {
        public int X { get; private set; }
        public int Y { get; private set; }

        public void Set(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
