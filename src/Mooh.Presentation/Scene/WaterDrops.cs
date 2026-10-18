using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Audio;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// The drips of <c>GameWaterSpawn</c> and <c>CGameObjWaterDrop</c>, which only decorate the
    /// level.
    /// </summary>
    /// <remarks>
    /// As play starts, and then every 1500 to 3000 ms (<c>CNT_WDropTime</c>), a drop forms in a
    /// random cell within <c>CNT_WDropLSize</c> = 4 of the player that holds nothing locking motion
    /// and lies under a <c>CNT_CMaskSpawnWaterDrop</c> cell. It forms over three 75 ms frames
    /// (<c>st_in</c>), falls at <c>CNT_DropSp</c> = 0.02 cells/ms while the cell below is free
    /// (<c>st_fly</c>), then splashes there over five 75 ms frames (<c>st_out</c>), playing
    /// <c>SndWDrop</c>. Gravity is taken to point down.
    /// </remarks>
    /// <param name="library">The recovered drop states and registrations.</param>
    /// <param name="random">The source of the drips' timing and places.</param>
    internal sealed class WaterDrops(AnimationLibrary library, Random random)
    {
        private const int Reach = 4;
        private const double FallSpeed = 0.02;
        private const int DownVariant = 3;

        private readonly AnimationTable _table = library.Table(AnimationLibrary.WaterDropTable);
        private readonly List<Drop> _drops = [];
        private double? _untilNext;

        /// <summary>Gets the drops' frames.</summary>
        public IEnumerable<SceneSprite> Sprites => _drops.Select(drop => new SceneSprite(
            default,
            _table.Page,
            Frames(drop.State)[Math.Min((int)drop.Index, Frames(drop.State).Count - 1)],
            drop.X,
            drop.Y,
            0));

        public IEnumerable<SoundCue> Update(WorldGrid world, double seconds)
        {
            double milliseconds = seconds * 1000;
            List<SoundCue> splashes = [];
            foreach (Drop drop in _drops)
            {
                Advance(world, drop, milliseconds, splashes);
            }

            _ = _drops.RemoveAll(drop => drop.IsGone);
            if (world.Player is not Entity player)
            {
                return splashes;
            }

            if (_untilNext is double left && (left -= milliseconds) > 0)
            {
                _untilNext = left;
                return splashes;
            }

            Spawn(world, player.Position);
            _untilNext = random.Next(1500, 3000);
            return splashes;
        }

        private void Spawn(WorldGrid world, GridPosition player)
        {
            List<GridPosition> cells = [];
            for (int x = Math.Max(0, player.X - Reach); x < Math.Min(player.X + Reach + 1, world.Width); x++)
            {
                for (int y = Math.Max(0, player.Y - Reach); y < Math.Min(world.Height, player.Y + Reach); y++)
                {
                    GridPosition cell = new(x, y);
                    GridPosition above = cell.Offset(Direction.Up);
                    if (!Locks(world, cell) && world.Contains(above) && world.GetOccupants(above).Any(occupant => LookOf(occupant)?.Drips == true))
                    {
                        cells.Add(cell);
                    }
                }
            }

            if (cells.Count > 0)
            {
                GridPosition chosen = cells[random.Next(cells.Count)];
                _drops.Add(new Drop(chosen.X, chosen.Y));
            }
        }

        private void Advance(WorldGrid world, Drop drop, double milliseconds, List<SoundCue> splashes)
        {
            if (drop.State == DropState.Falling)
            {
                drop.Y += FallSpeed * milliseconds;
                while (drop.Y >= drop.Row + 1)
                {
                    drop.Row++;
                    if (Locks(world, new GridPosition(drop.X, drop.Row + 1)))
                    {
                        Splash(drop, splashes);
                        return;
                    }
                }

                return;
            }

            drop.Index += milliseconds / 75;
            if (drop.Index < Frames(drop.State).Count)
            {
                return;
            }

            if (drop.State == DropState.Splashing)
            {
                drop.IsGone = true;
                return;
            }

            // st_in's end: _Move falls on, or splashes at once on a locked cell below.
            drop.State = DropState.Falling;
            drop.Index = 0;
            if (Locks(world, new GridPosition(drop.X, drop.Row + 1)))
            {
                Splash(drop, splashes);
            }
        }

        private void Splash(Drop drop, List<SoundCue> splashes)
        {
            drop.State = DropState.Splashing;
            drop.Index = 0;
            drop.Y = drop.Row;
            splashes.Add(new SoundCue(library.Sound("SndWDrop"), drop.X, drop.Y));
        }

        /// <summary>
        /// Models <c>CellObjCheck(cell, CNT_CMaskCellLockMotion)</c>: outside the level, or holding
        /// the player, a monster, the bio mass or a type whose mask locks motion.
        /// </summary>
        private bool Locks(WorldGrid world, GridPosition cell)
        {
            return !world.Contains(cell)
                || world.GetOccupants(cell).Any(occupant => occupant.PlayerBody is not null
                    || occupant.GreenMonsterBody is not null
                    || occupant.BlueMonsterBody is not null
                    || occupant.BioMassBody is not null
                    || LookOf(occupant)?.LocksMotion == true);
        }

        private ObjectLook? LookOf(Entity entity)
        {
            return entity.ArchivalIdentity is { } identity && library.TryGetLook(identity.ArchivalType, out ObjectLook look) ? look : null;
        }

        private IReadOnlyList<string> Frames(DropState state)
        {
            string name = state switch
            {
                DropState.Forming => "st_in",
                DropState.Falling => "st_fly",
                DropState.Splashing => "st_out",
                _ => "st_in",
            };
            IReadOnlyList<IReadOnlyList<string>> variants = _table.States[name].Variants;
            return variants[Math.Min(DownVariant, variants.Count - 1)];
        }

        private enum DropState
        {
            Forming,
            Falling,
            Splashing,
        }

        private sealed class Drop(int x, int row)
        {
            public int X { get; } = x;

            public int Row { get; set; } = row;

            public double Y { get; set; } = row;

            public DropState State { get; set; } = DropState.Forming;

            public double Index
            {
                get; set;
            }

            public bool IsGone
            {
                get; set;
            }
        }
    }
}
