using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>A tappable arrow at the midpoint of a selected cell's side.</summary>
    public sealed class BoardMarker : MonoBehaviour
    {
        public Direction Side { get; private set; }

        public void Set(Direction side) => Side = side;
    }
}
