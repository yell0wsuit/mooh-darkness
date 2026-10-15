using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Audio;
using Mooh.Presentation.Sprites;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// The world particles of <c>PartAddSeq</c> and the overlay parts of <c>PartsOvr.AddSeq</c>,
    /// with the effect functions of <c>CGameType</c> that emit them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A particle plays its frames once at its own rate and is gone after the last, as
    /// <c>PartsCycle</c> runs it: positions are in cells, speeds in cells per millisecond, and
    /// gravity, <c>fGravity</c>, accelerates it down. None of the effects ported here orbits
    /// (<c>fR</c>, <c>fD</c>, <c>fSR</c>, <c>fSD</c>). A particle that crosses into a
    /// cell holding a <c>CNT_CMaskPartsCollide</c> object turns back along that axis at
    /// <c>CNT_GPrm_PartsCollResp</c>, 0.75 of its speed. Its cell is its rounded position, the
    /// native's conversion under round-to-nearest.
    /// </para>
    /// <para>
    /// The overlay parts, landing glows, play at a fixed 35 ms a frame and are drawn after the
    /// world, as <c>ModRender</c> renders <c>PartsOvr</c> last. The pickup sparks of
    /// <c>SfxGlowKill</c> sample the eaten sprite's solid pixels, so they need a sprite mask.
    /// </para>
    /// </remarks>
    /// <param name="library">The recovered particle frames and registrations.</param>
    /// <param name="random">The source of the effects' random choices.</param>
    /// <param name="masks">The sprites' solid pixels, which the pickup sparks need.</param>
    internal sealed class ParticleSystem(AnimationLibrary library, Random random, ISpriteMask? masks)
    {
        /// <summary><c>SfxGlowKill</c>'s sampling step, <c>CNT_SX</c> and <c>CNT_SY</c>.</summary>
        private const int SparkStep = 7;

        /// <summary>
        /// The pickups whose eating throws sparks, <c>SfxGetStars</c>: the treasure of
        /// <c>CtrlCellEat</c>, its weapons, and the keys and bonuses of their <c>OnPlEat</c>.
        /// </summary>
        private static readonly HashSet<EntityKind> SparklingPickups =
        [
            EntityKind.Coin, EntityKind.Diamond, EntityKind.Grenade, EntityKind.GrenadeBox, EntityKind.Rocket,
            EntityKind.RocketBox, EntityKind.MineBox, EntityKind.BigBomb, EntityKind.Key, EntityKind.Cola, EntityKind.Ice,
        ];

        /// <summary><c>SfxEPartsObj</c>'s <c>fGravity</c>.</summary>
        private const double BurstGravity = 4e-6;

        /// <summary><c>SfxLandFire</c>'s and <c>SfxMoveFire</c>'s <c>fGravity</c>.</summary>
        private const double FireGravity = 8e-6;

        /// <summary><c>CNT_GPrm_PartsCollResp</c>.</summary>
        private const double Bounce = 0.75;

        /// <summary>The landing glow's frame time, <c>SfxLandGlow</c>'s <c>AddSeq(..., 35, ...)</c>.</summary>
        private const double GlowFrameMilliseconds = 35;

        /// <summary>
        /// <c>SfxLandSize</c>: the strip along each side of a cell that landing and pushing effects
        /// rise from, by direction.
        /// </summary>
        private static readonly Dictionary<Direction, ((double, double) X, (double, double) Y)> SideStrips = new()
        {
            [Direction.Left] = ((-0.4, -0.5), (-0.4, 0.4)),
            [Direction.Up] = ((-0.4, 0.4), (-0.4, -0.5)),
            [Direction.Right] = ((0.4, 0.5), (-0.4, 0.4)),
            [Direction.Down] = ((-0.4, 0.4), (0.4, 0.5)),
        };

        /// <summary>The big bomb's glint period, <c>_Prt_BBombGet</c>'s <c>EvSet(..., 400)</c>.</summary>
        private const double BigBombGlintMilliseconds = 400;

        /// <summary><c>App.Rand.Prob(10000)</c>: a glint is a flash instead about three times in ten.</summary>
        private const int FlashChance = 10000;

        /// <summary><c>_Prt_BBombGet</c>'s <c>App.Rand.Prob(8096)</c>.</summary>
        private const int BigBombFlashChance = 8096;

        private readonly List<Particle> _world = [];
        private readonly List<Particle> _overlay = [];
        private readonly Dictionary<EntityId, Twinkle> _twinkles = [];
        private TimeSpan _cola;
        private double? _bubbleMilliseconds;

        /// <summary>Gets world particles in the terrain pass, followed by overlay particles.</summary>
        public IEnumerable<SceneSprite> Sprites => _world.Select(particle => SpriteOf(particle, 1))
            .Concat(_overlay.Select(particle => SpriteOf(particle, int.MaxValue)));

        private static SceneSprite SpriteOf(Particle particle, int pass)
        {
            return new SceneSprite(default, particle.Page, particle.Frames[(int)particle.Index],
                particle.X, particle.Y, pass, particle.Additive)
            {
                SortAnchor = particle.Cell
            };
        }

        /// <summary>Moves the particles on, then emits what the frame's events call for.</summary>
        /// <param name="world">The simulated world, after the step.</param>
        /// <param name="events">The step's events.</param>
        /// <param name="seen">Where every entity was last drawn, including the ones now gone.</param>
        /// <param name="blasts">The cells a blast was shown in this frame.</param>
        /// <param name="steps">Where the player sounded a step this frame.</param>
        /// <param name="drawn">The frame an entity was last drawn with, if any.</param>
        /// <param name="seconds">The step's length.</param>
        public void Update(
            WorldGrid world,
            IReadOnlyList<SimulationEvent> events,
            IReadOnlyDictionary<EntityId, Sighting> seen,
            IReadOnlyList<GridPosition> blasts,
            IReadOnlyList<(double X, double Y)> steps,
            Func<EntityId, (string Page, string Frame)?> drawn,
            double seconds)
        {
            double milliseconds = seconds * 1000;
            foreach (Particle particle in _world)
            {
                particle.Advance(milliseconds, cell => StopsParticles(world, cell));
            }

            foreach (Particle particle in _overlay)
            {
                particle.Advance(milliseconds, _ => false);
            }

            _ = _world.RemoveAll(particle => particle.IsGone);
            _ = _overlay.RemoveAll(particle => particle.IsGone);
            Emit(world, events, seen, drawn);
            RunSparkles(world, milliseconds);
            Cola(world, events, milliseconds);
            foreach (GridPosition cell in blasts)
            {
                Smoke(cell.X, cell.Y);
            }

            foreach ((double x, double y) in steps)
            {
                WalkSmoke(x, y);
            }
        }

        private void Emit(
            WorldGrid world,
            IReadOnlyList<SimulationEvent> events,
            IReadOnlyDictionary<EntityId, Sighting> seen,
            Func<EntityId, (string Page, string Frame)?> drawn)
        {
            HashSet<EntityId> eaten = [.. events.OfType<PlayerAte>().Select(ate => ate.EatenEntityId)];
            eaten.UnionWith(events.OfType<PickupCollected>().Select(collected => collected.PickupEntityId));
            HashSet<GridPosition> blasted = [.. events.OfType<ExplosionCellReached>().Select(reached => reached.Position)];
            HashSet<EntityId> exploded = [.. events.OfType<ExplosionStarted>().Select(started => started.EntityId)];
            HashSet<EntityId> frozen = [.. seen.Where(pair => pair.Value.IsFrozenMonster).Select(pair => pair.Key)];
            foreach (SimulationEvent item in events)
            {
                switch (item)
                {
                    case ShakeImpact impact:
                        ImpactGlow(impact.X, impact.Y);
                        break;
                    case PlayerAte ate when seen.TryGetValue(ate.EatenEntityId, out Sighting beetle)
                        && beetle.Type is "OSmGr" or "OSmBlue":
                        CollectBeetle(beetle);
                        break;
                    case Landed landed when seen.TryGetValue(landed.EntityId, out Sighting at) && Exists(world, landed.EntityId):
                        // CGameObjFallBase's landing: RInfo.PrtLand, or SfxLandPuff.
                        Puff(LookOf(at)?.LandPuff ?? ParticlePuff.Smoke, at, landed.Direction, landing: true);
                        break;
                    case StrikePushStopped stopped when seen.TryGetValue(stopped.EntityId, out Sighting at) && LookOf(at)?.LandPuff is ParticlePuff puff:
                        // PlPush_OnCell's stop: RInfo.PrtLand only when the type names one.
                        Puff(puff, at, stopped.Direction, landing: true);
                        break;
                    case PushStarted pushed when seen.TryGetValue(pushed.EntityId, out Sighting at) && IsSupportedSideways(world, pushed):
                        Puff(LookOf(at)?.MovePuff ?? ParticlePuff.Smoke, at, pushed.Direction, landing: false);
                        break;
                    case PickupCollected collected when SparklingPickups.Contains(collected.Kind)
                        && seen.TryGetValue(collected.PickupEntityId, out Sighting at)
                        && library.Particles.PickupStars is ParticleBurst stars:
                        Sparks(stars, at, drawn(collected.PickupEntityId));
                        break;
                    case EnemyFrozen iced:
                        _ = frozen.Add(iced.EntityId);
                        if (seen.TryGetValue(iced.EntityId, out Sighting freezing))
                        {
                            IceSnow(freezing, 30, 0.4, 0.5);
                        }

                        break;
                    case EnemyThawed thawed:
                        _ = frozen.Remove(thawed.EntityId);
                        if (seen.TryGetValue(thawed.EntityId, out Sighting thawing))
                        {
                            IceSnow(thawing, 30, 0.4, 0.5);
                        }

                        break;
                    case EntityDestroyed destroyed when frozen.Contains(destroyed.EntityId)
                        && seen.TryGetValue(destroyed.EntityId, out Sighting icy):
                        ShatterIce(icy);
                        break;
                    case EntityDestroyed destroyed when !eaten.Contains(destroyed.EntityId) && seen.TryGetValue(destroyed.EntityId, out Sighting at):
                        Destroyed(at, exploded.Contains(destroyed.EntityId) || blasted.Contains(new GridPosition((int)Math.Round(at.X), (int)Math.Round(at.Y))), drawn(destroyed.EntityId));
                        break;
                    case DamageApplied { Amount: > 0 } damage when seen.TryGetValue(damage.EntityId, out Sighting hurt) && hurt.IsPlayer:
                        Hearts(hurt.X, hurt.Y);
                        break;
                    default:
                        break;
                }
            }
        }

        private void ImpactGlow(double x, double y)
        {
            ParticleFrames glow = library.Particles.ImpactGlow;
            if (glow.Variants.Count > 0)
            {
                _overlay.Add(new Particle(glow.Page, glow.Variants[0], 1.0 / 25, x, y)
                {
                    Additive = true,
                    Collides = false,
                });
            }
        }

        private void CollectBeetle(Sighting at)
        {
            ParticleFrames glow = library.Particles.BeetleCollectionGlow;
            if (glow.Variants.Count > 0)
            {
                _world.Add(new Particle(glow.Page, Pick(glow.Variants), Between(0.01, 0.02), at.X, at.Y)
                {
                    Additive = true,
                });
            }

            ParticleFrames stars = library.Particles.Flash;
            for (int index = 0; index < 10 && stars.Variants.Count > 0; index++)
            {
                _world.Add(new Particle(stars.Page, Pick(stars.Variants), Between(0.01, 0.02),
                    Between(at.X - 0.3, at.X + 0.3), Between(at.Y - 0.4, at.Y + 0.4))
                {
                    Additive = true,
                });
            }
        }

        /// <summary>
        /// Runs each resting pickup's <c>PFunc</c>: once as it first appears, as <c>GameInit</c>
        /// does, then every 750 to 1500 ms (the food's 1000 to 1500, the big bomb's 400).
        /// </summary>
        private void RunSparkles(WorldGrid world, double milliseconds)
        {
            HashSet<EntityId> present = [];
            foreach (Entity entity in world.Entities)
            {
                if (entity.ArchivalIdentity is not { } identity
                    || !library.TryGetLook(identity.ArchivalType, out ObjectLook look)
                    || look.Sparkle is not IdleSparkle sparkle)
                {
                    continue;
                }

                _ = present.Add(entity.Id);
                if (!_twinkles.TryGetValue(entity.Id, out Twinkle? twinkle))
                {
                    twinkle = new Twinkle();
                    _twinkles[entity.Id] = twinkle;
                }
                else if ((twinkle.Milliseconds -= milliseconds) > 0)
                {
                    continue;
                }

                (double x, double y) = DrawnPosition.Of(entity);
                Glint(sparkle, twinkle, x, y);
                twinkle.Milliseconds = Math.Max(0, twinkle.Milliseconds) + (sparkle switch
                {
                    IdleSparkle.BigBomb => BigBombGlintMilliseconds,
                    IdleSparkle.Steam => RandRange(1000, 1500),
                    IdleSparkle.Gem1 or IdleSparkle.Gem2 or IdleSparkle.Gem3 or IdleSparkle.Gem5 or IdleSparkle.Flash
                        or IdleSparkle.Grenade or IdleSparkle.Rocket => RandRange(750, 1500),
                    _ => RandRange(750, 1500),
                });
            }

            foreach (EntityId gone in _twinkles.Keys.Where(id => !present.Contains(id)).ToList())
            {
                _ = _twinkles.Remove(gone);
            }
        }

        private void Glint(IdleSparkle sparkle, Twinkle twinkle, double x, double y)
        {
            if (sparkle == IdleSparkle.Flash
                || (sparkle is not (IdleSparkle.Steam or IdleSparkle.BigBomb) && Prob(FlashChance)))
            {
                Flash(x, y);
                return;
            }

            ParticleFrames frames = library.Particles.Sparkles.GetValueOrDefault(sparkle) ?? ParticleFrames.None;
            if (frames.Variants.Count == 0)
            {
                return;
            }

            if (sparkle == IdleSparkle.Steam)
            {
                _world.Add(new Particle(frames.Page, Pick(frames.Variants), Between(0.01, 0.02), x, y)
                {
                    SpeedY = Between(-0.0002, -0.0004),
                    Collides = false,
                    Additive = true,
                });
                return;
            }

            if (sparkle == IdleSparkle.BigBomb)
            {
                if (Prob(BigBombFlashChance))
                {
                    Flash(x, y);
                }

                // Two glints opposite each other on a 0.4-cell circle that turns half a radian a time.
                twinkle.Rotation -= 0.5;
                (double sine, double cosine) = Math.SinCos(twinkle.Rotation);
                (double offsetX, double offsetY) = (0.4 * cosine, 0.4 * sine);
                double centerY = y - 0.1875;
                foreach (int side in (int[])[1, -1])
                {
                    _world.Add(new Particle(frames.Page, frames.Variants[0], Between(0.02, 0.03), x + (side * offsetX), centerY + (side * offsetY))
                    {
                        SpeedX = side * offsetX * 0.00015,
                        SpeedY = side * offsetY * 0.00015,
                        Collides = false,
                        Additive = true,
                    });
                }

                return;
            }

            _world.Add(new Particle(frames.Page, Pick(frames.Variants), Between(0.02, 0.03), x + Between(-0.3, 0.3), y + Between(-0.4, 0.4))
            {
                Collides = false,
                Additive = true,
            });
        }

        /// <summary><c>_Ptr_Flash</c>: a mine star near the pickup.</summary>
        private void Flash(double x, double y)
        {
            ParticleFrames flash = library.Particles.Flash;
            if (flash.Variants.Count > 0)
            {
                _world.Add(new Particle(flash.Page, Pick(flash.Variants), Between(0.02, 0.03), x + Between(-0.3, 0.3), y + Between(-0.4, 0.4))
                {
                    Collides = false,
                    Additive = true,
                });
            }
        }

        /// <summary>
        /// The cola's bubbles, <c>BonColaWork</c>: forty at once as its protection starts or runs
        /// out and whenever it soaks up a hit (<c>_Prt_ColaProt</c>), and one every 150 to 300 ms
        /// while it lasts (<c>_Prt_ColaOn</c>).
        /// </summary>
        private void Cola(WorldGrid world, IReadOnlyList<SimulationEvent> events, double milliseconds)
        {
            if (world.Player is not Entity player || player.PlayerBody?.Progress is not PlayerProgress progress)
            {
                return;
            }

            TimeSpan cola = progress.ColaRemaining;
            (double x, double y) = DrawnPosition.Of(player);
            bool started = _cola == TimeSpan.Zero && cola > TimeSpan.Zero;
            bool ranOut = cola == TimeSpan.Zero && _cola > TimeSpan.Zero && _cola.TotalMilliseconds <= milliseconds + 1;
            bool soaked = cola > TimeSpan.Zero && events.Any(item => item is DamageIgnored ignored && ignored.EntityId == player.Id);
            _cola = cola;
            if (started || ranOut || soaked)
            {
                for (int index = 0; index < 40; index++)
                {
                    Bubble(x, y);
                }
            }

            if (cola == TimeSpan.Zero)
            {
                _bubbleMilliseconds = null;
                return;
            }

            if (_bubbleMilliseconds is not double left)
            {
                Bubble(x, y);
                _bubbleMilliseconds = RandRange(150, 300);
                return;
            }

            left -= milliseconds;
            if (left <= 0)
            {
                Bubble(x, y);
                left += RandRange(150, 300);
            }

            _bubbleMilliseconds = left;
        }

        private void Bubble(double x, double y)
        {
            ParticleFrames bubbles = library.Particles.ColaBubbles;
            if (bubbles.Variants.Count > 0)
            {
                _world.Add(new Particle(bubbles.Page, Pick(bubbles.Variants), Between(0.02, 0.04), Between(x - 0.3, x + 0.3), Between(y - 0.4, y + 0.4))
                {
                    SpeedX = Between(-0.0002, 0.0002),
                    SpeedY = Between(-0.0004, 0),
                    Additive = true,
                });
            }
        }

        /// <summary>Emits additive snow around a freezing, thawing or shattering monster.</summary>
        private void IceSnow(Sighting at, int count, double spreadX, double spreadY)
        {
            ParticleFrames snow = library.Particles.IceSnow;
            if (snow.Variants.Count == 0)
            {
                return;
            }

            for (int index = 0; index < count; index++)
            {
                _world.Add(new Particle(snow.Page, Pick(snow.Variants), Between(0.01, 0.005),
                    Between(at.X - spreadX, at.X + spreadX), Between(at.Y - spreadY, at.Y + spreadY))
                {
                    SpeedX = Between(-0.0005, 0.0005),
                    SpeedY = Between(-0.0005, 0.0005),
                    Additive = true
                });
            }
        }

        private void ShatterIce(Sighting at)
        {
            ParticleFrames fragments = library.Particles.IceFragments;
            if (fragments.Variants.Count > 0)
            {
                for (int index = 0; index < 20; index++)
                {
                    _world.Add(new Particle(fragments.Page, Pick(fragments.Variants), Between(0.01, 0.005),
                        Between(at.X - 0.3, at.X + 0.3), Between(at.Y - 0.4, at.Y + 0.4))
                    {
                        SpeedX = Between(-0.0005, 0.0005),
                        SpeedY = Between(0, -0.001),
                        Gravity = BurstGravity
                    });
                }
            }

            IceSnow(at, 10, 0.3, 0.4);
        }

        /// <summary>
        /// Bursts a killed object: <c>ObjKill</c>'s <c>RInfo.PrtKill</c>, then, when a blast
        /// killed it, <c>_ObjKillWrd</c>'s <c>RInfo.EParts</c>.
        /// </summary>
        private void Destroyed(Sighting at, bool blasted, (string Page, string Frame)? frame)
        {
            if (LookOf(at) is not ObjectLook look)
            {
                return;
            }

            foreach (ParticleBurst burst in look.KillBursts)
            {
                if (burst.Kind == ParticleBurstKind.Stars)
                {
                    Sparks(burst, at, frame);
                }
                else
                {
                    Burst(burst, at.X, at.Y);
                }
            }

            if (!blasted)
            {
                return;
            }

            foreach (ParticleBurst burst in look.BlastBursts)
            {
                Burst(burst, at.X, at.Y);
            }
        }

        /// <summary><c>SfxEPartsObj</c> and <c>SfxEPartsObjAir</c>: pieces strewn over the cell.</summary>
        private void Burst(ParticleBurst burst, double x, double y)
        {
            if (burst.Kind == ParticleBurstKind.Stars)
            {
                return;
            }

            int count = RandRange(burst.MinCount, burst.MaxCount);
            for (int index = 0; index < count; index++)
            {
                _world.Add(new Particle(
                    burst.Page,
                    Pick(burst.Variants),
                    Between(1 / burst.MinFrameMilliseconds, 1 / burst.MaxFrameMilliseconds),
                    Between(x - 0.5, x + 0.5),
                    Between(y - 0.5, y + 0.5))
                {
                    SpeedX = Between(-0.001, 0.001),
                    SpeedY = Between(-0.001, 0.001),
                    Gravity = burst.Kind == ParticleBurstKind.Ground ? BurstGravity : 0,
                });
            }
        }

        /// <summary>
        /// <c>SfxGlowKill</c>: a spark at every solid point of a 7 px grid over the object's frame,
        /// flying out from its cell's middle at 0.00001 to 0.0002 cells/ms, turned up to half a
        /// radian.
        /// </summary>
        private void Sparks(ParticleBurst burst, Sighting at, (string Page, string Frame)? drawn)
        {
            if (masks is null || drawn is not (string page, string name) || masks.FrameOf(page, name) is not SpriteFrame frame)
            {
                return;
            }

            for (int row = 0; row < frame.Height; row += SparkStep)
            {
                for (int column = 0; column < frame.Width; column += SparkStep)
                {
                    if (!masks.IsSolid(page, frame.X + column, frame.Y + row))
                    {
                        continue;
                    }

                    double x = at.X + ((frame.PivotX + column) / SceneCamera.CellWidth);
                    double y = at.Y + ((frame.PivotY + row) / SceneCamera.CellHeight);
                    (double outX, double outY) = (x - Math.Round(x), y - Math.Round(y));
                    double length = Math.Sqrt((outX * outX) + (outY * outY));
                    double speed = Between(1e-5, 0.0002);
                    (double speedX, double speedY) = length > 0 ? (outX / length * speed, outY / length * speed) : (0, 0);
                    (speedX, speedY) = Rotate(speedX, speedY, Between(-0.5, 0.5));
                    _world.Add(new Particle(
                        burst.Page,
                        Pick(burst.Variants),
                        Between(1 / burst.MinFrameMilliseconds, 1 / burst.MaxFrameMilliseconds),
                        x,
                        y)
                    {
                        SpeedX = speedX,
                        SpeedY = speedY,
                        Additive = true,
                    });
                }
            }
        }

        /// <summary>
        /// <c>SfxLandPuff</c>, <c>SfxLandFire</c>, <c>SfxMovePuff</c> and <c>SfxMoveFire</c>: smoke
        /// or sparks from the side of the cell it landed on, or from under a pushed object, thrown
        /// back against the push.
        /// </summary>
        private void Puff(ParticlePuff puff, Sighting at, Direction direction, bool landing)
        {
            ((double, double) stripX, (double, double) stripY) = SideStrips[landing ? direction : Direction.Down];
            (double minX, double maxX) = (at.X + stripX.Item1, at.X + stripX.Item2);
            (double minY, double maxY) = (at.Y + stripY.Item1, at.Y + stripY.Item2);
            bool fire = puff == ParticlePuff.Fire;
            IReadOnlyList<IReadOnlyList<string>> variants = fire ? library.Particles.Fire : library.Particles.WalkSmoke;
            int count = fire ? RandRange(5, 10) : RandRange(3, 6);
            (int pushX, int pushY) = Step(Opposite(direction));
            for (int index = 0; index < count && variants.Count > 0; index++)
            {
                (double speedX, double speedY) = (Between(-0.0005, 0.0005), Between(-0.0005, 0.0005));
                if (landing && fire)
                {
                    (speedX, speedY) = (Between(-0.002, 0.002), Between(-0.002, -0.004));
                }
                else if (!landing)
                {
                    double speed = Between(0.0005, 0.001);
                    double turn = Between(-0.8, 0.8);
                    (speedX, speedY) = Rotate(pushX * speed, pushY * speed, turn);
                }

                _world.Add(new Particle(
                    ParticleEffects.PartsPage,
                    Pick(variants),
                    Between(0.01, 0.02),
                    Between(minX, maxX),
                    Between(minY, maxY))
                {
                    SpeedX = speedX,
                    SpeedY = speedY,
                    Gravity = fire ? FireGravity : 0,
                });
            }

            if (landing)
            {
                Glow(direction, minX, maxX, minY, maxY);
            }
        }

        /// <summary>
        /// <c>SfxLandGlow</c>: an overlay glow at a point of the strip, horizontal for a landing
        /// along a vertical direction.
        /// </summary>
        private void Glow(Direction direction, double minX, double maxX, double minY, double maxY)
        {
            IReadOnlyList<string> frames = direction is Direction.Up or Direction.Down
                ? library.Particles.GlowLandHorizontal
                : library.Particles.GlowLandVertical;
            if (frames.Count > 0)
            {
                _overlay.Add(new Particle(ParticleEffects.GlowPage, frames, 1 / GlowFrameMilliseconds, Between(minX, maxX), Between(minY, maxY))
                {
                    Additive = true,
                    Collides = false,
                });
            }
        }

        /// <summary><c>SfxPlWalk</c>: one puff at the player's feet.</summary>
        private void WalkSmoke(double x, double y)
        {
            if (library.Particles.WalkSmoke.Count > 0)
            {
                _world.Add(new Particle(
                    ParticleEffects.PartsPage,
                    Pick(library.Particles.WalkSmoke),
                    Between(0.01, 0.02),
                    x + Between(-0.3, 0.3),
                    y + Between(0.4, 0.5)));
            }
        }

        /// <summary>The five puffs of smoke <c>SfxExplAdd1x1</c> leaves in a blast cell.</summary>
        private void Smoke(double x, double y)
        {
            for (int index = 0; index < 5 && library.Particles.Smoke.Count > 0; index++)
            {
                _world.Add(new Particle(
                    ParticleEffects.PartsPage,
                    Pick(library.Particles.Smoke),
                    Between(0.01, 0.02),
                    x + Between(-0.5, 0.5),
                    y + Between(-0.5, 0.5))
                {
                    SpeedX = Between(-0.0001, 0.0001),
                    SpeedY = Between(-0.0001, 0.0001),
                });
            }
        }

        /// <summary>The hearts a hurt player sheds, <c>SfxHealth</c> in <c>GamePlayer</c>'s damage handler.</summary>
        private void Hearts(double x, double y)
        {
            int count = RandRange(3, 7);
            for (int index = 0; index < count && library.Particles.Hearts.Count > 0; index++)
            {
                _world.Add(new Particle(
                    ParticleEffects.PartsPage,
                    Pick(library.Particles.Hearts),
                    Between(0.02, 0.04),
                    x + Between(-0.3, 0.3),
                    y + Between(-0.5, 0.5))
                {
                    SpeedX = Between(-0.0005, 0.0005),
                    SpeedY = Between(-0.0005, 0.0005),
                });
            }
        }

        /// <summary>
        /// Models the push's puff test: a sideways push of an object with something in the cell
        /// below its rounded column.
        /// </summary>
        private static bool IsSupportedSideways(WorldGrid world, PushStarted pushed)
        {
            if (pushed.Direction is Direction.Up or Direction.Down || !Exists(world, pushed.EntityId))
            {
                return false;
            }

            GridPosition below = world.GetEntity(pushed.EntityId).Position.Offset(Direction.Down);
            return world.Contains(below) && world.GetOccupants(below).Count > 0;
        }

        private static bool Exists(WorldGrid world, EntityId id)
        {
            return world.Entities.Any(entity => entity.Id == id);
        }

        private bool StopsParticles(WorldGrid world, GridPosition cell)
        {
            return world.Contains(cell)
                && world.GetOccupants(cell).Any(occupant => occupant.ArchivalIdentity is { } identity
                    && library.TryGetLook(identity.ArchivalType, out ObjectLook look)
                    && look.StopsParticles);
        }

        private ObjectLook? LookOf(Sighting at)
        {
            return at.Type is string type && library.TryGetLook(type, out ObjectLook look) ? look : null;
        }

        private static Direction Opposite(Direction direction)
        {
            return direction switch
            {
                Direction.Left => Direction.Right,
                Direction.Right => Direction.Left,
                Direction.Up => Direction.Down,
                Direction.Down => Direction.Up,
                _ => direction,
            };
        }

        private static (int X, int Y) Step(Direction direction)
        {
            GridPosition step = new GridPosition(0, 0).Offset(direction);
            return (step.X, step.Y);
        }

        private static (double X, double Y) Rotate(double x, double y, double angle)
        {
            (double sine, double cosine) = Math.SinCos(angle);
            return ((x * cosine) - (y * sine), (x * sine) + (y * cosine));
        }

        /// <summary><c>App.Rand.Range</c>: a whole number from the first up to, not including, the second.</summary>
        private int RandRange(int min, int max)
        {
            return max > min ? random.Next(min, max) : min;
        }

        /// <summary><c>App.Rand.Prob(k)</c>: true when a 15-bit draw is at most <paramref name="chance"/>.</summary>
        private bool Prob(int chance)
        {
            return random.Next(32768) <= chance;
        }

        /// <summary><c>App.Rand.FRange</c>.</summary>
        private double Between(double min, double max)
        {
            return min + (random.NextDouble() * (max - min));
        }

        private IReadOnlyList<string> Pick(IReadOnlyList<IReadOnlyList<string>> variants)
        {
            return variants[random.Next(variants.Count)];
        }

        /// <summary>A resting pickup's glint timer and, for the big bomb, its glints' turn.</summary>
        private sealed class Twinkle
        {
            public double Milliseconds
            {
                get; set;
            }

            public double Rotation
            {
                get; set;
            }
        }

        /// <summary>One particle as <c>PartsCycle</c> moves it.</summary>
        private sealed class Particle(string page, IReadOnlyList<string> frames, double framesPerMillisecond, double x, double y)
        {
            private (int X, int Y) _cell = ((int)Math.Round(x), (int)Math.Round(y));

            public (double X, double Y) Cell => _cell;

            public string Page { get; } = page;

            public IReadOnlyList<string> Frames { get; } = frames;

            public double Index
            {
                get; private set;
            }

            public double X { get; private set; } = x;

            public double Y { get; private set; } = y;

            public double SpeedX
            {
                get; set;
            }

            public double SpeedY
            {
                get; set;
            }

            public double Gravity
            {
                get; init;
            }

            public bool Additive
            {
                get; init;
            }

            public bool Collides { get; init; } = true;

            public bool IsGone
            {
                get; private set;
            }

            public void Advance(double milliseconds, Func<GridPosition, bool> stops)
            {
                Index += framesPerMillisecond * milliseconds;
                if (Index >= Frames.Count)
                {
                    IsGone = true;
                    return;
                }

                X += SpeedX * milliseconds;
                Y += SpeedY * milliseconds;
                if (Gravity != 0)
                {
                    SpeedY += Gravity * milliseconds;
                    Y += Gravity * milliseconds * milliseconds * 0.5;
                }

                (int X, int Y) cell = ((int)Math.Round(X), (int)Math.Round(Y));
                if (cell == _cell)
                {
                    return;
                }

                if (Collides && stops(new GridPosition(cell.X, cell.Y)))
                {
                    int dx = cell.X - _cell.X;
                    int dy = cell.Y - _cell.Y;
                    if ((dx > 0 && SpeedX > 0) || (dx < 0 && SpeedX < 0))
                    {
                        SpeedX = -Bounce * SpeedX;
                    }

                    if ((dy > 0 && SpeedY > 0) || (dy < 0 && SpeedY < 0))
                    {
                        SpeedY = -Bounce * SpeedY;
                    }
                }

                _cell = cell;
            }
        }
    }
}
