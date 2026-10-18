using System;

using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a pushed balloon entered its recovered <c>ST_Work</c> and started the fuse
    /// that ends in its own 3x3 blast. The recovered <c>PlPush_Ballon</c> deletes itself, so a
    /// balloon reports this at most once.
    /// </summary>
    /// <param name="EntityId">The balloon whose fuse was lit.</param>
    /// <param name="Duration">The whole fuse, from ignition to detonation.</param>
    public sealed record BalloonFuseLit(EntityId EntityId, TimeSpan Duration) : SimulationEvent(EntityId);
}
