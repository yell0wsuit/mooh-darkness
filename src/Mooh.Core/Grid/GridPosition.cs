using System;

namespace Mooh.Core.Grid
{
    public readonly record struct GridPosition(int X, int Y)
    {
        public GridPosition Offset(Direction direction)
        {
            return direction switch
            {
                Direction.Left => this with { X = X - 1 },
                Direction.Up => this with { Y = Y - 1 },
                Direction.Right => this with { X = X + 1 },
                Direction.Down => this with { Y = Y + 1 },
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
            };
        }
    }
}
