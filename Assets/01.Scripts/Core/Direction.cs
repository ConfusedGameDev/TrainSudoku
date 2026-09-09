namespace TrainSudoku.Core
{
    /// <summary>A side of a cell. North is y-1 (towards the top row), South is y+1, East is x+1, West is x-1.</summary>
    public enum Direction
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    public static class DirectionExtensions
    {
        public static readonly Direction[] All = { Direction.North, Direction.East, Direction.South, Direction.West };

        public static Direction Opposite(this Direction direction) => (Direction)(((int)direction + 2) % 4);

        public static int Dx(this Direction direction)
        {
            switch (direction)
            {
                case Direction.East: return 1;
                case Direction.West: return -1;
                default: return 0;
            }
        }

        public static int Dy(this Direction direction)
        {
            switch (direction)
            {
                case Direction.South: return 1;
                case Direction.North: return -1;
                default: return 0;
            }
        }

        public static bool IsVertical(this Direction direction) => direction == Direction.North || direction == Direction.South;
    }
}
