using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Scene
{
    /// <summary>Generates visible terrain outside the puzzle, without adding world entities.</summary>
    public sealed class ExteriorScenery
    {
        private readonly int _originalWidth;
        private readonly int _originalHeight;
        private readonly uint _seed;
        private readonly List<AnimationTable> _terrain = [];
        private readonly HashSet<AnimationTable> _roundSupports = [];
        private readonly Dictionary<GridPosition, AnimationTable> _edges = [];
        private readonly AnimationTable _rocks;
        private readonly AnimationTable _dirt;
        private readonly AnimationTable[] _accentSolids;
        private readonly AnimationTable _pipeHorizontal;
        private readonly AnimationTable _pipeVertical;
        private int _turns;
        private int _width;
        private int _height;

        /// <summary>Samples the authored terrain once, using an independent scenery seed.</summary>
        public ExteriorScenery(WorldGrid world, AnimationLibrary library, int seed)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(library);
            _originalWidth = _width = world.Width;
            _originalHeight = _height = world.Height;
            _seed = unchecked((uint)seed);
            _rocks = library.Table("ORock01");
            BackdropStyle style = Backdrop.StyleOf(world.Entities.Select(entity => entity.ArchivalIdentity?.ArchivalType ?? string.Empty), library);
            _dirt = library.Table(style switch
            {
                BackdropStyle.Normal => "CellsEat01",
                BackdropStyle.Desert => "DCellsEat01",
                BackdropStyle.Tropical => "JCellsEat01",
                _ => "CellsEat01",
            });
            _accentSolids =
            [
                SolidTable("CellsSolidWd", library),
                SolidTable("CellsSolidPl", library),
                SolidTable(style switch
                {
                    BackdropStyle.Normal => "CellsSolid01",
                    BackdropStyle.Desert => "DCellsSolid01",
                    BackdropStyle.Tropical => "JCellsSolid01",
                    _ => "CellsSolid01",
                }, library),
                SolidTable("CellsMetal01", library),
            ];
            _pipeHorizontal = SolidTable("PipeHor", library);
            _pipeVertical = SolidTable("PipeVert", library);
            foreach (Entity entity in world.Entities)
            {
                string? type = entity.ArchivalIdentity?.ArchivalType;
                if (type is not ("CellsEat01" or "CellsSolid01" or "CellsMetal01" or "CellsSolidPl"
                    or "CellsSolidWd" or "DCellsEat01" or "DCellsSolid01" or "JCellsEat01" or "JCellsSolid01"))
                {
                    continue;
                }

                AnimationTable table = library.Table(type);
                _terrain.Add(table);
                if (entity.Interactions.HasFlag(InteractionMask.RoundSupport))
                {
                    _ = _roundSupports.Add(table);
                }
                GridPosition cell = entity.Position;
                if (cell.X == 0 || cell.Y == 0 || cell.X == _width - 1 || cell.Y == _height - 1)
                {
                    _edges[cell] = table;
                }
            }

            if (_terrain.Count == 0)
            {
                _terrain.Add(library.Table("CellsSolid01"));
            }
        }

        /// <summary>Keeps the generated cells attached to the world after a completed turn.</summary>
        public void Rotate(WorldRotationDirection direction)
        {
            _turns = (_turns + (direction == WorldRotationDirection.Left ? 1 : 3)) % 4;
            (_width, _height) = (_height, _width);
        }

        /// <summary>Lists only exterior sprites near the visible camera, before any in-flight spin.</summary>
        public IReadOnlyList<SceneSprite> Layout(SceneCamera camera, WorldSpin? spin = null)
        {
            ArgumentNullException.ThrowIfNull(camera);
            (double baseX, double baseY) = camera.Base;
            double left = (-camera.HorizontalMargin - baseX) / SceneCamera.CellWidth;
            double right = (800 + camera.HorizontalMargin - baseX) / SceneCamera.CellWidth;
            double top = (-camera.VerticalMargin - baseY) / SceneCamera.CellHeight;
            double bottom = (600 + camera.VerticalMargin - baseY) / SceneCamera.CellHeight;
            (double X, double Y)[] corners = [(left, top), (right, top), (left, bottom), (right, bottom)];
            if (spin is WorldSpin turning)
            {
                // Invert the affine draw transform to find the unturned visible cells.
                (double ox, double oy) = turning.Transform(0, 0);
                (double ax, double ay) = turning.Transform(1, 0);
                (double bx, double by) = turning.Transform(0, 1);
                ax -= ox;
                ay -= oy;
                bx -= ox;
                by -= oy;
                double determinant = (ax * by) - (ay * bx);
                for (int i = 0; i < corners.Length; i++)
                {
                    (double x, double y) = corners[i];
                    corners[i] = ((((x - ox) * by) - ((y - oy) * bx)) / determinant,
                        (((y - oy) * ax) - ((x - ox) * ay)) / determinant);
                }
            }

            // Two cells of overscan cover sprite overhang and camera easing between updates.
            int minX = (int)Math.Floor(corners.Min(point => point.X)) - 2;
            int maxX = (int)Math.Ceiling(corners.Max(point => point.X)) + 2;
            int minY = (int)Math.Floor(corners.Min(point => point.Y)) - 2;
            int maxY = (int)Math.Ceiling(corners.Max(point => point.Y)) + 2;
            List<SceneSprite> sprites = [];
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (x >= 0 && y >= 0 && x < _width && y < _height)
                    {
                        continue;
                    }

                    GridPosition cell = OriginalCell(x, y);
                    uint choice = Hash(cell.X, cell.Y);
                    // Keep a continuous border, then break up the terrain with small rocky pockets.
                    bool pocket = IsPocket(cell);
                    // Only decorate pocket floors. Query the procedural terrain directly, so
                    // support does not depend on camera culling or previously generated cells.
                    // Mapping the current downward neighbor also respects completed world turns.
                    if (pocket && !HasPocketObject(x, y))
                    {
                        continue;
                    }

                    AnimationTable table = pocket ? CanRest(x, y) ? _rocks : _dirt : TerrainAt(x, y);
                    AnimationClip clip = table.States[AnimationLibrary.StillState];
                    IReadOnlyList<string> frames = clip.Variants[(int)((choice >> 8) % (uint)clip.Variants.Count)];
                    int pass = table == _pipeHorizontal || table == _pipeVertical ? 1 : 0;
                    sprites.Add(new SceneSprite(default, clip.Page ?? table.Page, frames[0], x, y, pass));
                }
            }

            return sprites;
        }

        private int DistanceFromLevel(GridPosition cell)
        {
            return Math.Max(Math.Max(-cell.X, cell.X - _originalWidth + 1),
                Math.Max(-cell.Y, cell.Y - _originalHeight + 1));
        }

        private bool IsPocket(GridPosition cell)
        {
            return DistanceFromLevel(cell) > 2
                && Hash((int)Math.Floor(cell.X / 3.0), (int)Math.Floor(cell.Y / 3.0)) % 5 == 0;
        }

        private bool HasPocketObject(int x, int y)
        {
            GridPosition cell = OriginalCell(x, y);
            return IsPocket(cell) && Hash(cell.X, cell.Y) % 3 == 0 && !IsPocket(OriginalCell(x, y + 1));
        }

        private bool BlocksCell(int x, int y)
        {
            return !IsPocket(OriginalCell(x, y)) || HasPocketObject(x, y);
        }

        private bool CanRest(int x, int y)
        {
            // GravitySystem only slides a rock off RoundSupport. Dirt and ordinary
            // solid terrain hold it directly; metal needs either a blocked side or
            // blocked diagonal below on both sides, including a three-cell shelf.
            return !_roundSupports.Contains(TerrainAt(x, y + 1))
                || ((BlocksCell(x - 1, y) || BlocksCell(x - 1, y + 1))
                    && (BlocksCell(x + 1, y) || BlocksCell(x + 1, y + 1)));
        }

        private AnimationTable TerrainAt(int x, int y)
        {
            GridPosition cell = OriginalCell(x, y);
            GridPosition edge = new(Math.Clamp(cell.X, 0, _originalWidth - 1), Math.Clamp(cell.Y, 0, _originalHeight - 1));
            int distance = DistanceFromLevel(cell);
            if (distance <= 2 && _edges.TryGetValue(edge, out AnimationTable? bordering))
            {
                return bordering;
            }

            AnimationTable basis = _terrain[(int)(Hash(cell.X, cell.Y) % (uint)_terrain.Count)];
            if (distance <= 2)
            {
                return basis;
            }

            int column = (int)Math.Floor(cell.X / 3.0);
            int row = (int)Math.Floor(cell.Y / 3.0);
            uint patch = Hash(column ^ 0x51A3, row ^ 0x27D5);
            // Keep the sampled terrain dominant; accents form coherent small patches.
            if (patch % 5 != 0)
            {
                return basis;
            }
            int material = (int)((patch >> 8) % (uint)(_accentSolids.Length + 2));
            if (material < _accentSolids.Length)
            {
                return _accentSolids[material];
            }

            bool horizontal = material == _accentSolids.Length;
            int startX = column * 3;
            int startY = row * 3;
            if (horizontal ? cell.Y != startY + 1 : cell.X != startX + 1)
            {
                return basis;
            }
            // Install the entire three-cell run or none of it: no cut pipes at pockets
            // or the preserved border. All decisions use world cells, never the viewport.
            for (int offset = 0; offset < 3; offset++)
            {
                GridPosition segment = horizontal
                    ? new GridPosition(startX + offset, startY + 1)
                    : new GridPosition(startX + 1, startY + offset);
                if (DistanceFromLevel(segment) <= 2 || IsPocket(segment))
                {
                    return basis;
                }
            }
            bool drawnHorizontal = horizontal == (_turns % 2 == 0);
            return drawnHorizontal ? _pipeHorizontal : _pipeVertical;
        }

        private AnimationTable SolidTable(string type, AnimationLibrary library)
        {
            AnimationTable table = library.Table(type);
            if (ArchivalObjectCatalog.Get(type).Interactions.HasFlag(InteractionMask.RoundSupport))
            {
                _ = _roundSupports.Add(table);
            }
            return table;
        }

        private GridPosition OriginalCell(int x, int y)
        {
            return _turns switch
            {
                1 => new GridPosition(y, _originalHeight - 1 - x),
                2 => new GridPosition(_originalWidth - 1 - x, _originalHeight - 1 - y),
                3 => new GridPosition(_originalWidth - 1 - y, x),
                _ => new GridPosition(x, y),
            };
        }

        private uint Hash(int x, int y)
        {
            unchecked
            {
                uint value = _seed ^ ((uint)x * 0x9E3779B9u) ^ ((uint)y * 0x85EBCA6Bu);
                value = (value ^ (value >> 16)) * 0x7FEB352Du;
                value = (value ^ (value >> 15)) * 0x846CA68Bu;
                return value ^ (value >> 16);
            }
        }
    }
}
