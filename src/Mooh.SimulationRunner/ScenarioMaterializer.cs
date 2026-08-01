using System;
using System.Buffers.Binary;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

namespace Mooh.SimulationRunner
{
    /// <summary>Translates validated scenario declarations into core runtime objects.</summary>
    public static class ScenarioMaterializer
    {
        /// <summary>Materializes a validated scenario document.</summary>
        /// <param name="scenario">The validated scenario declaration.</param>
        /// <returns>The live deterministic simulation and alias maps.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="scenario"/> is <see langword="null"/>.</exception>
        public static MaterializedScenario Materialize(ScenarioDocument scenario)
        {
            ArgumentNullException.ThrowIfNull(scenario);

            Direction gravityDirection = ScenarioValidator.ParseEnum<Direction>(
                scenario.World.Gravity,
                scenario.Name,
                "world.gravity");
            WorldGrid world = new(scenario.World.Width, scenario.World.Height);
            Dictionary<string, EntityId> idsByAlias = [];
            Dictionary<EntityId, string> aliasesById = [];

            for (int index = 0; index < scenario.World.Entities.Count; index++)
            {
                ScenarioEntity definition = scenario.World.Entities[index];
                EntityId id = CreateDeterministicId(index);
                Entity entity = new(
                    id,
                    new GridPosition(definition.Position.X, definition.Position.Y),
                    CreateInteractionMask(definition.Interactions),
                    CreateGravityBody(definition.GravityBody),
                    CreatePlayerBody(definition.PlayerBody, scenario.Name),
                    CreateHealthBody(definition.HealthBody),
                    CreateGreenMonsterBody(definition.GreenMonsterBody, scenario.Name));
                world.Add(entity);
                idsByAlias.Add(definition.Alias, id);
                aliasesById.Add(id, definition.Alias);
            }

            SimulationWorld simulation = new(
                world,
                gravityDirection,
                new DefaultGravityCollisionPolicy(),
                new Random(scenario.World.RandomSeed));
            return new MaterializedScenario(
                scenario,
                world,
                simulation,
                idsByAlias,
                aliasesById);
        }

        private static EntityId CreateDeterministicId(int index)
        {
            Span<byte> bytes = stackalloc byte[16];
            bytes.Clear();
            BinaryPrimitives.WriteInt32LittleEndian(bytes, index + 1);
            return new EntityId(new Guid(bytes));
        }

        private static InteractionMask CreateInteractionMask(IReadOnlyList<string> names)
        {
            InteractionMask mask = InteractionMask.None;
            foreach (string name in names)
            {
                mask |= Enum.Parse<InteractionMask>(name, ignoreCase: false);
            }

            return mask;
        }

        private static GravityBody? CreateGravityBody(ScenarioGravityBody? definition)
        {
            return definition is null
                ? null
                : new GravityBody(definition.FallSpeed, definition.CanSlide);
        }

        private static PlayerBody? CreatePlayerBody(ScenarioPlayerBody? definition, string source)
        {
            return definition is null
                ? null
                : new PlayerBody(
                    definition.MoveSpeed,
                    definition.PushSpeedMultiplier,
                    ScenarioValidator.ParseEnum<Direction>(
                        definition.FacingDirection,
                        source,
                        "playerBody.facingDirection"));
        }

        private static HealthBody? CreateHealthBody(ScenarioHealthBody? definition)
        {
            return definition is null
                ? null
                : new HealthBody(
                    definition.Maximum,
                    definition.Current,
                    TimeSpan.FromMilliseconds(definition.ImmunityMilliseconds));
        }

        private static GreenMonsterBody? CreateGreenMonsterBody(
            ScenarioGreenMonsterBody? definition,
            string source)
        {
            return definition is null
                ? null
                : new GreenMonsterBody(
                    definition.MoveSpeed,
                    ScenarioValidator.ParseEnum<Direction>(
                        definition.FacingDirection,
                        source,
                        "greenMonsterBody.facingDirection"),
                    TimeSpan.FromMilliseconds(definition.TurnMilliseconds),
                    TimeSpan.FromMilliseconds(definition.AttackWindupMilliseconds),
                    TimeSpan.FromMilliseconds(definition.AttackRecoveryMilliseconds),
                    definition.StrikeDamage);
        }
    }
}
