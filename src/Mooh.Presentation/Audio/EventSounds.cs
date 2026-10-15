using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Audio
{
    /// <summary>
    /// Cues the sounds the original's handlers play for what the simulation reports: a landing's
    /// <c>SndLand</c>, a push's <c>SndMove</c>, eating's <c>SndEat</c>, a kill's <c>SndKill</c>, a
    /// blast's <c>SndExpl</c>, a hurt player's three cues, and the weapons', doors' and turns'.
    /// </summary>
    /// <param name="library">The recovered registrations and sound aliases.</param>
    /// <param name="random">The timing of delayed food sounds.</param>
    internal sealed class EventSounds(AnimationLibrary library, Random random)
    {
        private readonly List<(SoundCue Cue, double Remaining)> _delayed = [];

        /// <summary>Gets the cues for a frame's events.</summary>
        /// <param name="events">The events.</param>
        /// <param name="seen">Where each entity was last drawn, and its archival type.</param>
        /// <param name="seconds">The elapsed simulation seconds, excluding paused time.</param>
        /// <returns>The cues.</returns>
        public IEnumerable<SoundCue> For(IReadOnlyList<SimulationEvent> events, IReadOnlyDictionary<EntityId, Sighting> seen, double seconds)
        {
            for (int index = 0; index < _delayed.Count;)
            {
                (SoundCue cue, double remaining) = _delayed[index];
                remaining -= seconds;
                if (remaining <= 1e-10)
                {
                    _delayed.RemoveAt(index);
                    yield return cue;
                }
                else
                {
                    _delayed[index++] = (cue, remaining);
                }
            }

            HashSet<EntityId> eaten = [];
            HashSet<EntityId> silentKills = [];
            foreach (SimulationEvent interaction in events)
            {
                switch (interaction)
                {
                    case PlayerAte ate:
                        _ = silentKills.Add(ate.EatenEntityId);
                        break;
                    case PickupCollected pickup:
                        _ = silentKills.Add(pickup.PickupEntityId);
                        break;
                    case FrozenMonsterShattered ice:
                        _ = silentKills.Add(ice.EntityId);
                        break;
                    default:
                        break;
                }
            }
            HashSet<EntityId> floodBlasts = [.. events.OfType<ExplosionCellReached>()
                .Where(cell => cell.Pattern == ExplosionPattern.Flood).Select(cell => cell.EntityId)];
            foreach (SimulationEvent item in events)
            {
                string? alias = null;
                EntityId at = item.EntityId;
                switch (item)
                {
                    case ExplosionCellReached { Pattern: ExplosionPattern.CrossArm, Direction: Direction direction } arm:
                        GridPosition origin = arm.Position.Offset(direction.Opposite());
                        yield return new SoundCue(Sound("SndBarrelExpl"), origin.X, origin.Y);
                        break;
                    case ExplosionCellReached { Pattern: ExplosionPattern.Flood } flood:
                        yield return new SoundCue(Sound("SndWeapBBombExpl"), flood.Position.X, flood.Position.Y);
                        break;
                    case MagicActivated magic:
                        yield return new SoundCue(Sound("SndMagicWork"), magic.Position.X, magic.Position.Y);
                        break;
                    case FrozenMonsterShattered ice:
                        yield return new SoundCue(Sound("SndBonIceWork"), ice.Position.X, ice.Position.Y);
                        break;
                    case SmallBeetleKilled beetle:
                        yield return new SoundCue(Sound("SndSmallKill"), beetle.Position.X, beetle.Position.Y);
                        break;
                    case FireContactResolved fire:
                        string fireSound = fire.Kind switch
                        {
                            FireContactKind.Player => "SndFirePop",
                            FireContactKind.MutualDestruction => "SndFireBob",
                            FireContactKind.Consumed => "SndFireBrk",
                            _ => throw new InvalidOperationException("Undefined fire interaction."),
                        };
                        yield return new SoundCue(Sound(fireSound), fire.Position.X, fire.Position.Y);
                        break;
                    case Landed landed:
                        alias = Registered(seen, landed.EntityId, "SndLand") ?? Sound("SndDefFall");
                        break;
                    case StrikePushStopped stopped:
                        alias = Registered(seen, stopped.EntityId, "SndLand") ?? Sound("SndDefFall");
                        break;
                    case PushStarted pushed:
                        alias = Registered(seen, pushed.EntityId, "SndMove") ?? (pushed.IsStrike ? null : Sound("SndDefMove"));
                        break;
                    case PlayerAte ate when eaten.Add(ate.EatenEntityId):
                        at = ate.EatenEntityId;
                        alias = seen.TryGetValue(ate.EatenEntityId, out Sighting food) && IsEdibleEnemy(food.Type)
                            ? Sound("SndSmallGet")
                            : Registered(seen, ate.EatenEntityId, "SndEat") ?? Sound("SndCellBrk");
                        break;
                    case PickupCollected collected when eaten.Add(collected.PickupEntityId):
                        at = collected.PickupEntityId;
                        alias = Registered(seen, collected.PickupEntityId, "SndEat") ?? Sound("SndCellBrk");
                        break;
                    case EntityDestroyed destroyed when !silentKills.Contains(destroyed.EntityId):
                        alias = Registered(seen, destroyed.EntityId, "SndKill");
                        break;
                    case ExplosionStarted blast:
                        alias = floodBlasts.Contains(blast.EntityId) ? null : BlastSound(seen, blast.EntityId);
                        break;
                    case KeyDoorOpeningStarted door:
                        at = door.DoorEntityId;
                        alias = Sound("SndKeyDoor");
                        break;
                    case DamageApplied { Amount: > 0 } damage when seen.TryGetValue(damage.EntityId, out Sighting hurt) && hurt.IsPlayer:
                        foreach (string cue in (string[])[damage.RemainingHealth <= 0 ? Sound("SndPlFatal") : Sound("SndPlHeal"), Sound("SndVoice1"), Sound("SndPlStr")])
                        {
                            yield return new SoundCue(cue, hurt.X, hurt.Y);
                        }

                        break;
                    case PlayerWeaponEmpty:
                        alias = Sound("SndWeapNo");
                        break;
                    case PlayerWeaponSelected:
                        alias = Sound("SndWeapChng");
                        break;
                    case MineLaid:
                        alias = Sound("SndWeapMinePlace");
                        break;
                    case RocketFired:
                        alias = Sound("SndWeapRockFly");
                        break;
                    case BigBombPlaced placed:
                        at = placed.BombEntityId;
                        alias = Sound("SndWeapBBombPlace");
                        break;
                    case BioMassSpread spread:
                        alias = Sound("SndBioNew");
                        break;
                    case WorldRotationStarted:
                        alias = Sound("SndWorldRot");
                        break;
                    case HelpTextRequested:
                        alias = Sound("SndTableTxt");
                        break;
                    default:
                        break;
                }

                if (alias is not null && seen.TryGetValue(at, out Sighting where))
                {
                    yield return new SoundCue(alias, where.X, where.Y);
                    if (where.Type is "OEat01" or "OEat02" or "OEat03"
                        && item is PlayerAte or PickupCollected)
                    {
                        _delayed.Add((new SoundCue(Sound("SndEatEat2"), where.X, where.Y), random.Next(200, 250) / 1000.0));
                    }
                }
            }
        }

        private static bool IsEdibleEnemy(string? type)
        {
            return type is "OSmGr" or "OSmBlue"
                || type?.StartsWith("MGr", StringComparison.Ordinal) == true
                || type?.StartsWith("MBlue", StringComparison.Ordinal) == true;
        }

        /// <summary>
        /// <c>ObjExplRange3x3</c> plays the source's <c>SndExpl</c> or the default; a barrel's cross
        /// blast and the big bomb have their own.
        /// </summary>
        private string? BlastSound(IReadOnlyDictionary<EntityId, Sighting> seen, EntityId source)
        {
            return !seen.TryGetValue(source, out Sighting sighting)
                ? Sound("SndDefExpl")
                : sighting.Type switch
                {
                    "OBarel" => Sound("SndBarrelMainExpl"),
                    null when sighting.IsBigBomb => Sound("SndWeapBBombExpl"),
                    _ => Registered(seen, source, "SndExpl") ?? Sound("SndDefExpl"),
                };
        }

        private string? Registered(IReadOnlyDictionary<EntityId, Sighting> seen, EntityId entity, string field)
        {
            return seen.TryGetValue(entity, out Sighting sighting)
                && sighting.Type is string type
                && library.TryGetLook(type, out ObjectLook look)
                && look.Sounds.TryGetValue(field, out string? alias)
                ? alias
                : null;
        }

        private string Sound(string name)
        {
            return library.Sound(name);
        }
    }

    /// <summary>Where an entity was last drawn, and what it is.</summary>
    /// <param name="Type">Its archival type, or <see langword="null"/> for a runtime-built entity.</param>
    /// <param name="X">The drawn column.</param>
    /// <param name="Y">The drawn row.</param>
    /// <param name="IsPlayer">Whether it is the player.</param>
    /// <param name="IsBigBomb">Whether it is a placed big bomb.</param>
    /// <param name="IsFrozenMonster">Whether the monster was frozen when last seen.</param>
    internal readonly record struct Sighting(string? Type, double X, double Y, bool IsPlayer, bool IsBigBomb, bool IsFrozenMonster = false);
}
