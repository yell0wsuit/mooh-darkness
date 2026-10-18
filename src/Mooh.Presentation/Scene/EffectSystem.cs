using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Grid;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// Plays the one-shot effect objects the original adds to its world list, starting with the
    /// blast, <c>SfxExplAdd1x1</c>. Each object plays its frames once and is killed at the end,
    /// <c>WEvOnAnimEnd = StateKill</c>.
    /// </summary>
    /// <param name="library">The recovered effect layers.</param>
    /// <param name="random">The source of the effects' random choices.</param>
    internal sealed class EffectSystem(AnimationLibrary library, Random random)
    {
        /// <summary>
        /// <c>_ObjExplWrd</c>'s <c>CellTempAdd(x, y, 1000)</c>: a cell shows a blast at most once a
        /// second, however many blasts reach it.
        /// </summary>
        private const double BlastRepeatSeconds = 1;

        private readonly List<Effect> _effects = [];
        private readonly Dictionary<GridPosition, double> _blastUntil = [];
        private double _time;

        /// <summary>Gets the playing effects' frames.</summary>
        public IEnumerable<SceneSprite> Sprites => _effects.Select(effect => new SceneSprite(
            default,
            effect.Page,
            effect.Frames[Math.Min((int)effect.Position, effect.Frames.Count - 1)],
            effect.X,
            effect.Y,
            effect.Pass));

        /// <summary>Plays the effects on, then starts the ones the frame's events call for.</summary>
        /// <param name="world">The simulated world, after the step.</param>
        /// <param name="events">The step's events.</param>
        /// <param name="seconds">The step's length.</param>
        /// <returns>The cells a blast was shown in.</returns>
        public IReadOnlyList<GridPosition> Update(WorldGrid world, IReadOnlyList<SimulationEvent> events, double seconds)
        {
            List<GridPosition> shown = [];
            _time += seconds;
            foreach (Effect effect in _effects)
            {
                effect.Position += seconds * 1000 / effect.FrameMilliseconds;
            }

            _ = _effects.RemoveAll(effect => effect.Position >= effect.Frames.Count);
            foreach (SimulationEvent item in events)
            {
                GridPosition? position = item switch
                {
                    ExplosionCellReached reached => reached.Position,
                    MagicDiamondSpawned magic => magic.Position,
                    FireContactResolved fire => fire.Position,
                    _ => null,
                };
                if (position is not GridPosition cell)
                {
                    continue;
                }

                bool direct = item is FireContactResolved;
                if ((item is ExplosionCellReached && !SimulationWorld.ShowsBlast(world, cell))
                    || (!direct && _blastUntil.TryGetValue(cell, out double until) && until > _time))
                {
                    continue;
                }

                if (!direct)
                {
                    _blastUntil[cell] = _time + BlastRepeatSeconds;
                }

                Spawn(library.Explosion, cell);
                shown.Add(cell);
            }

            return shown;
        }

        private void Spawn(IReadOnlyList<EffectLayer> layers, GridPosition cell)
        {
            foreach (EffectLayer layer in layers)
            {
                int count = layer.Counts[random.Next(layer.Counts.Count)];
                for (int index = 0; index < count && layer.Variants.Count != 0; index++)
                {
                    double fast = 1 / layer.FrameMilliseconds.Min;
                    double slow = 1 / layer.FrameMilliseconds.Max;
                    _effects.Add(new Effect(
                        layer.Page,
                        layer.Variants[random.Next(layer.Variants.Count)],
                        1 / (fast + (random.NextDouble() * (slow - fast))),
                        cell.X + Between(layer.Shift),
                        cell.Y + Between(layer.Shift),
                        layer.Pass));
                }
            }
        }

        private double Between((double Min, double Max) range)
        {
            return range.Min + (random.NextDouble() * (range.Max - range.Min));
        }

        private sealed class Effect(string page, IReadOnlyList<string> frames, double frameMilliseconds, double x, double y, int pass)
        {
            public string Page { get; } = page;

            public IReadOnlyList<string> Frames { get; } = frames;

            public double FrameMilliseconds { get; } = frameMilliseconds;

            public double X { get; } = x;

            public double Y { get; } = y;

            public int Pass { get; } = pass;

            public double Position
            {
                get; set;
            }
        }
    }
}
