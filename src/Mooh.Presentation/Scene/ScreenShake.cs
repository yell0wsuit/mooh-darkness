using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Simulation;
using Mooh.Presentation.Audio;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// The view's jerks, <c>fScrX</c> and <c>fScrY</c>: an event puts the view off its place and it
    /// springs back on <c>CNT_ScrStiffPrms</c>, ω = 0.075 and λ = −0.01 per millisecond.
    /// </summary>
    /// <remarks>
    /// A blast jerks it 2.5 px, <c>CNT_ScrStiffImpS</c>, in a random direction, and the big bomb
    /// 5 px, <c>CNT_ScrStiffImpB</c>. A landing jerks it along gravity, 5 px for a hard mover; a
    /// struck object stopping jerks it 2.5 px along the push; a strike on what neither takes
    /// control nor gives way jerks it 5 px along the strike. Each jerk replaces the last and keeps
    /// the speed, and the spring moves before the frame's jerks, as <c>ModCycle</c> runs it.
    /// </remarks>
    internal sealed class ScreenShake
    {
        private const double Soft = 2.5;
        private const double Hard = 5;

        private readonly DampedSpring _spring = new(0.075, -0.01);

        public double X => _spring.X;

        public double Y => _spring.Y;

        public void Update(
            WorldGrid world,
            IReadOnlyList<SimulationEvent> events,
            IReadOnlyDictionary<EntityId, Sighting> seen,
            double seconds,
            Random random)
        {
            _spring.Advance(seconds * 1000);
            foreach (SimulationEvent item in events)
            {
                switch (item)
                {
                    case ExplosionStarted blast:
                        Jerk(seen.TryGetValue(blast.EntityId, out Sighting source) && source.IsBigBomb ? Hard : Soft, random);
                        break;
                    case Landed landed:
                        Jerk(landed.Direction, world.Entities.FirstOrDefault(entity => entity.Id == landed.EntityId)?.GravityBody?.ImpactStrength == GravityImpactStrength.Hard ? Hard : Soft);
                        break;
                    case StrikePushStopped stopped:
                        Jerk(stopped.Direction, Soft);
                        break;
                    case PlayerStruck struck when HitsSolid(world, events, struck):
                        Jerk(struck.Direction, Hard);
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Models <c>_AEnd_Str</c>'s test: something is left in the struck cell after the strike
        /// ate, and none of it was pushed, launched or turned the world.
        /// </summary>
        private static bool HitsSolid(WorldGrid world, IReadOnlyList<SimulationEvent> events, PlayerStruck struck)
        {
            return world.Contains(struck.Position)
                && world.GetOccupants(struck.Position).Any(occupant => occupant.Id != struck.EntityId)
                && !events.Any(item => item is PushStarted or ArrowLaunched or WorldRotationStarted);
        }

        /// <summary>A jerk in a random direction, <c>ModScrStiff</c>.</summary>
        private void Jerk(double distance, Random random)
        {
            double angle = random.NextDouble() * Math.PI * 2;
            _spring.Set(distance * Math.Cos(angle), distance * Math.Sin(angle));
        }

        /// <summary>A jerk along a direction, <c>ModScrStiffDir</c> and <c>ModScrStiffDown</c>.</summary>
        private void Jerk(Direction direction, double distance)
        {
            GridPosition step = new GridPosition(0, 0).Offset(direction);
            _spring.Set(step.X * distance, step.Y * distance);
        }
    }
}
