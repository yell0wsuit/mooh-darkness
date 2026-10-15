using System.Collections.Generic;

using Mooh.Core.Entities;
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
    internal sealed class EventSounds(AnimationLibrary library)
    {
        /// <summary>Gets the cues for a frame's events.</summary>
        /// <param name="events">The events.</param>
        /// <param name="seen">Where each entity was last drawn, and its archival type.</param>
        /// <returns>The cues.</returns>
        public IEnumerable<SoundCue> For(IReadOnlyList<SimulationEvent> events, IReadOnlyDictionary<EntityId, Sighting> seen)
        {
            HashSet<EntityId> eaten = [];
            foreach (SimulationEvent item in events)
            {
                string? alias = null;
                EntityId at = item.EntityId;
                switch (item)
                {
                    case Landed landed:
                        alias = Registered(seen, landed.EntityId, "SndLand") ?? Sound("SndDefFall");
                        break;
                    case StrikePushStopped stopped:
                        alias = Registered(seen, stopped.EntityId, "SndLand") ?? Sound("SndDefFall");
                        break;
                    case PushStarted pushed:
                        alias = Registered(seen, pushed.EntityId, "SndMove") ?? Sound("SndDefMove");
                        break;
                    case PlayerAte ate when eaten.Add(ate.EatenEntityId):
                        at = ate.EatenEntityId;
                        alias = Registered(seen, ate.EatenEntityId, "SndEat") ?? Sound("SndCellBrk");
                        break;
                    case PickupCollected collected when eaten.Add(collected.PickupEntityId):
                        at = collected.PickupEntityId;
                        alias = Registered(seen, collected.PickupEntityId, "SndEat") ?? Sound("SndCellBrk");
                        break;
                    case EntityDestroyed destroyed when !eaten.Contains(destroyed.EntityId):
                        alias = Registered(seen, destroyed.EntityId, "SndKill");
                        break;
                    case ExplosionStarted blast:
                        alias = BlastSound(seen, blast.EntityId);
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
                }
            }
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
    internal readonly record struct Sighting(string? Type, double X, double Y, bool IsPlayer, bool IsBigBomb);
}
