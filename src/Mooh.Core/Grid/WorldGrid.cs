using System;

namespace Mooh.Core.Grid
{
    public sealed class WorldGrid
    {
        public WorldGrid(int width, int height)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }

        public bool Contains(GridPosition position)
        {
            return position.X >= 0 && position.X < Width
                && position.Y >= 0 && position.Y < Height;
        }
    }
}
