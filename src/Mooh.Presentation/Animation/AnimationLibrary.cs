using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Mooh.Core.Grid;

namespace Mooh.Presentation.Animation
{
    /// <summary>
    /// Loads the recovered animation tables, <c>assets/animations/animations.json</c>, which
    /// <c>tools/recover_animations.py</c> rebuilt from the original bytecode.
    /// </summary>
    public sealed class AnimationLibrary
    {
        /// <summary>The player's table, <c>CGameObjPlayer.SMap</c>.</summary>
        public const string PlayerTable = "player";

        /// <summary>The Green Monster's table, <c>CGameObjGrMon.SMap</c>.</summary>
        public const string GreenMonsterTable = "monster.green";

        /// <summary>The Blue Monster's table, <c>CGameObjMonBlue.SMap</c>.</summary>
        public const string BlueMonsterTable = "monster.blue";

        /// <summary>The bio mass's table, <c>CGameObjBioMass.SMap</c>.</summary>
        public const string BioMassTable = "bio";

        /// <summary>The teleporter's table, <c>CGameObjTPort.SMap</c>.</summary>
        public const string TeleporterTable = "teleporter";

        /// <summary>The water drop's table, <c>CGameObjWaterDrop.SMap</c>.</summary>
        public const string WaterDropTable = "waterdrop";

        /// <summary>The green small beetle's table, <c>GameObjSmGreen</c>.</summary>
        public const string GreenBeetleTable = "beetle.green";

        /// <summary>The blue small beetle's table, <c>GameObjSmBlue</c>.</summary>
        public const string BlueBeetleTable = "beetle.blue";

        /// <summary>The thrown grenade's table, <c>CGameObjFallBase.GrType</c>.</summary>
        public const string GrenadeTable = "grenade";

        /// <summary>The laid mine's table, <c>CGameObjFallBase.MineType</c>.</summary>
        public const string MineTable = "mine";

        /// <summary>The placed big bomb's table, <c>CGameObjFallBase.TypeBBomb</c>.</summary>
        public const string BigBombTable = "bigbomb";

        /// <summary>The left-flying rocket's table, <c>GameWeapRockLeft</c>.</summary>
        public const string RocketLeftTable = "rocket.left";

        /// <summary>The right-flying rocket's table, <c>GameWeapRockRight</c>.</summary>
        public const string RocketRightTable = "rocket.right";

        /// <summary>
        /// The one state of a table whose original type has no state map: terrain, tunnels and a
        /// fired rocket all play a single animation.
        /// </summary>
        public const string StillState = "st_stay";

        /// <summary>The single state a fired rocket plays.</summary>
        public const string FlyState = "st_fly";

        private readonly Dictionary<string, AnimationTable> _tables;
        private readonly Dictionary<string, ObjectLook> _looks;

        private AnimationLibrary(Dictionary<string, AnimationTable> tables, Dictionary<string, ObjectLook> looks, IReadOnlyList<EffectLayer> explosion)
        {
            _tables = tables;
            _looks = looks;
            Explosion = explosion;
        }

        /// <summary>Gets the blast's layers, <c>SfxExpl</c>, which <c>SfxExplAdd1x1</c> plays in a cell.</summary>
        public IReadOnlyList<EffectLayer> Explosion
        {
            get;
        }

        /// <summary>Gets every table id: the named tables above and every archival type with frames.</summary>
        public IReadOnlyCollection<string> TableIds => _tables.Keys;

        /// <summary>Gets a table.</summary>
        /// <param name="id">A named table or an archival type name.</param>
        /// <returns>The table.</returns>
        /// <exception cref="KeyNotFoundException">There is no such table.</exception>
        public AnimationTable Table(string id)
        {
            return _tables[id];
        }

        /// <summary>Looks up a table.</summary>
        /// <param name="id">A named table or an archival type name.</param>
        /// <param name="table">The table, when found.</param>
        /// <returns><see langword="true"/> when the table exists.</returns>
        public bool TryGetTable(string id, out AnimationTable table)
        {
            if (_tables.TryGetValue(id, out AnimationTable? found))
            {
                table = found;
                return true;
            }

            table = null!;
            return false;
        }

        /// <summary>Looks up what an archival registration says about drawing its objects.</summary>
        /// <param name="type">The archival type name.</param>
        /// <param name="look">The look, when the type is registered.</param>
        /// <returns><see langword="true"/> when the type is registered.</returns>
        public bool TryGetLook(string type, out ObjectLook look)
        {
            if (_looks.TryGetValue(type, out ObjectLook? found))
            {
                look = found;
                return true;
            }

            look = null!;
            return false;
        }

        /// <summary>Reads the recovered animation tables.</summary>
        /// <param name="stream">The <c>animations.json</c> bytes.</param>
        /// <returns>The library.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidDataException">A named table is missing.</exception>
        public static AnimationLibrary Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            JsonElement classTables = root.GetProperty("classTables");
            JsonElement typeFunctions = root.GetProperty("typeFunctions");
            Dictionary<string, AnimationTable> tables = new(StringComparer.Ordinal)
            {
                [PlayerTable] = StateMap(classTables, "CGameObjPlayer.SMap", "SpritePlayer"),
                [GreenMonsterTable] = StateMap(classTables, "CGameObjGrMon.SMap", "SpriteMonGreen"),
                [BlueMonsterTable] = StateMap(classTables, "CGameObjMonBlue.SMap", "SpriteMonBlue"),
                [BioMassTable] = StateMap(classTables, "CGameObjBioMass.SMap", "SpriteObjBio"),
                [TeleporterTable] = StateMap(classTables, "CGameObjTPort.SMap", "SpriteObjTPort"),
                [WaterDropTable] = StateMap(classTables, "CGameObjWaterDrop.SMap", "SpriteObjWaterDrop"),
                [GreenBeetleTable] = StateMap(typeFunctions, "GameObjSmBeetle.GameObjSmGreen", "SpriteObjSmBeetle"),
                [BlueBeetleTable] = StateMap(typeFunctions, "GameObjSmBeetle.GameObjSmBlue", "SpriteObjSmBeetle"),
                [GrenadeTable] = TypeTable(Required(classTables, "CGameObjFallBase.GrType")),
                [MineTable] = TypeTable(Required(classTables, "CGameObjFallBase.MineType")),
                [BigBombTable] = TypeTable(Required(classTables, "CGameObjFallBase.TypeBBomb")),
                [RocketLeftTable] = SingleClipTable(Required(typeFunctions, "GameObjWeap.GameWeapRockLeft"), FlyState, null),
                [RocketRightTable] = SingleClipTable(Required(typeFunctions, "GameObjWeap.GameWeapRockRight"), FlyState, null),
            };

            Dictionary<string, ObjectLook> looks = [with(StringComparer.Ordinal)];
            foreach (JsonProperty registration in root.GetProperty("objectTypes").EnumerateObject())
            {
                AnimationTable? table = RegisteredTable(registration.Value);
                if (table is not null)
                {
                    tables[registration.Name] = table;
                }

                looks[registration.Name] = LookOf(registration.Value);
            }

            List<EffectLayer> explosion = root.TryGetProperty("effects", out JsonElement effects)
                && effects.TryGetProperty("SfxExpl", out JsonElement layers)
                ? [.. layers.EnumerateArray().Select(EffectLayerOf)]
                : [];
            return new AnimationLibrary(tables, looks, explosion);
        }

        /// <summary>Reads a recovered <c>CSfxAnimObj.CType(Page, AList, ulATime, fSh, Num, RPass)</c>.</summary>
        private static EffectLayer EffectLayerOf(JsonElement layer)
        {
            string page = layer.GetProperty("Page") is { ValueKind: JsonValueKind.Object } pageCall
                && pageCall.TryGetProperty("args", out JsonElement pageArgs)
                ? pageArgs[0].GetString() ?? string.Empty
                : PageOf(layer) ?? string.Empty;
            return new EffectLayer(
                page,
                Frames(layer.GetProperty("AList"), variantsPerName: false),
                PairOf(layer.GetProperty("ulATime")),
                PairOf(layer.GetProperty("fSh")),
                layer.GetProperty("Num") is { ValueKind: JsonValueKind.Array } counts
                    ? [.. counts.EnumerateArray().Select(count => count.GetInt32())]
                    : [layer.GetProperty("Num").GetInt32()],
                layer.TryGetProperty("RPass", out JsonElement pass) && pass.ValueKind == JsonValueKind.Number ? pass.GetInt32() : 0);
        }

        private static (double, double) PairOf(JsonElement value)
        {
            return value.ValueKind == JsonValueKind.Array
                ? (value[0].GetDouble(), value[1].GetDouble())
                : (value.GetDouble(), value.GetDouble());
        }

        private static ObjectLook LookOf(JsonElement registration)
        {
            BackdropStyle? backdrop = registration.TryGetProperty("BackType", out JsonElement back)
                && back.ValueKind == JsonValueKind.Object
                && back.TryGetProperty("ref", out JsonElement name)
                ? name.GetString() switch
                {
                    "self.BackInfo.LayerTypeNrm" => BackdropStyle.Normal,
                    "self.BackInfo.LayerTypeTrop" => BackdropStyle.Tropical,
                    "self.BackInfo.LayerTypeDesert" => BackdropStyle.Desert,
                    _ => null,
                }
                : null;
            (double, double)? depth = registration.TryGetProperty("fShZ", out JsonElement shift)
                && shift.ValueKind == JsonValueKind.Array
                && shift.GetArrayLength() == 2
                ? (shift[0].GetDouble(), shift[1].GetDouble())
                : null;
            return new ObjectLook(backdrop, depth);
        }

        private static JsonElement Required(JsonElement container, string name)
        {
            return container.TryGetProperty(name, out JsonElement value)
                ? value
                : throw new InvalidDataException($"The animation data has no '{name}' table.");
        }

        private static AnimationTable StateMap(JsonElement container, string name, string page)
        {
            return new AnimationTable(page, States(Required(container, name)));
        }

        /// <summary>Reads a recovered <c>CType</c> that carries a page and a state map.</summary>
        private static AnimationTable TypeTable(JsonElement type)
        {
            return new AnimationTable(PageOf(type) ?? throw new InvalidDataException("A type table has no page."), States(type.GetProperty("SMap")));
        }

        /// <summary>Reads a recovered type whose single animation is its <c>AList</c>.</summary>
        private static AnimationTable SingleClipTable(JsonElement type, string state, string? page)
        {
            string resolvedPage = PageOf(type) ?? page ?? throw new InvalidDataException("A single-clip table has no page.");
            AnimationClip clip = Clip(type, variantsPerName: false);
            return new AnimationTable(resolvedPage, new Dictionary<string, AnimationClip>(StringComparer.Ordinal) { [state] = clip });
        }

        /// <summary>
        /// Reads an archival registration, <c>CObjRepInfo</c>. Behaviour classes with a state map of
        /// their own (the player, the monsters, the bio mass, the teleporter) carry none here.
        /// </summary>
        private static AnimationTable? RegisteredTable(JsonElement registration)
        {
            if (!registration.TryGetProperty("Prm", out JsonElement parameter) || parameter.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (parameter.TryGetProperty("SMap", out JsonElement stateMap) && stateMap.ValueKind == JsonValueKind.Object)
            {
                return TypeTable(parameter);
            }

            string typeName = registration.TryGetProperty("Type", out JsonElement type) && type.TryGetProperty("ref", out JsonElement reference)
                ? reference.GetString() ?? string.Empty
                : string.Empty;
            return typeName switch
            {
                // Terrain picks one still frame by editor index: AList is the variants.
                "GameCells.CGameCellBase" when parameter.TryGetProperty("AList", out _) => SingleClipTable(parameter, StillState, null) with
                {
                    States = new Dictionary<string, AnimationClip>(StringComparer.Ordinal)
                    {
                        [StillState] = Clip(parameter, variantsPerName: true) with { FrameMilliseconds = [0] },
                    },
                },
                "GameCellPipes.CGameCellPipes" => new AnimationTable("SpriteCellPipes", new Dictionary<string, AnimationClip>(StringComparer.Ordinal)
                {
                    [StillState] = Clip(parameter, variantsPerName: true),
                }),
                _ => null,
            };
        }

        private static Dictionary<string, AnimationClip> States(JsonElement stateMap)
        {
            Dictionary<string, AnimationClip> states = [with(StringComparer.Ordinal)];
            foreach (JsonProperty state in stateMap.EnumerateObject())
            {
                if (state.Value.ValueKind == JsonValueKind.Object && state.Value.TryGetProperty("AList", out _))
                {
                    states[state.Name] = Clip(state.Value, variantsPerName: false);
                }
            }

            return states;
        }

        private static AnimationClip Clip(JsonElement state, bool variantsPerName)
        {
            return new AnimationClip(
                PageOf(state),
                Frames(state.GetProperty("AList"), variantsPerName),
                state.TryGetProperty("FList", out JsonElement fly) && fly.ValueKind == JsonValueKind.Array ? Frames(fly, variantsPerName: false) : null,
                FrameTimes(state),
                state.TryGetProperty("AEnd", out JsonElement end) && end.ValueKind == JsonValueKind.String ? end.GetString() : null,
                state.TryGetProperty("AEnd", out JsonElement handler) && handler.ValueKind == JsonValueKind.Object && handler.TryGetProperty("ref", out JsonElement name)
                    ? (name.GetString() ?? string.Empty).Split('.')[^1]
                    : null,
                DirectionOf(state));
        }

        /// <summary>
        /// Reads a recovered <c>AList</c>: one frame name, a list of names, or a list of variants.
        /// With <paramref name="variantsPerName"/> a flat list is one single-frame variant per name,
        /// which is how terrain picks a frame by editor index.
        /// </summary>
        private static List<IReadOnlyList<string>> Frames(JsonElement frames, bool variantsPerName)
        {
            if (frames.ValueKind == JsonValueKind.String)
            {
                return [[frames.GetString()!]];
            }

            // A state with no frames, such as the player hidden in the shelter, draws nothing.
            if (frames.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            List<JsonElement> items = [.. frames.EnumerateArray()];
            if (items.Count != 0 && items.All(item => item.ValueKind == JsonValueKind.Array))
            {
                return [.. items.Select(item => (IReadOnlyList<string>)[.. item.EnumerateArray().Select(name => name.GetString()!)])];
            }

            string[] names = [.. items.Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)];
            return variantsPerName ? [.. names.Select(name => (IReadOnlyList<string>)[name])] : [names];
        }

        private static int[] FrameTimes(JsonElement state)
        {
            if (!state.TryGetProperty("ulATime", out JsonElement time))
            {
                return [0];
            }

            // A pair is the original's per-entry choice of frame time, such as a diamond's 45 or 55 ms.
            return time.ValueKind == JsonValueKind.Array
                ? [.. time.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.Number).Select(value => (int)value.GetDouble())]
                : time.ValueKind == JsonValueKind.Number
                ? [(int)time.GetDouble()]
                : throw new InvalidDataException("A recovered frame time is neither a number nor a list.");
        }

        private static string? PageOf(JsonElement element)
        {
            return element.TryGetProperty("Page", out JsonElement page) && page.ValueKind == JsonValueKind.Object && page.TryGetProperty("page", out JsonElement name)
                ? name.GetString()
                : null;
        }

        private static Direction? DirectionOf(JsonElement state)
        {
            foreach (string field in (string[])["ulDDir", "ulDir", "ulFDir"])
            {
                if (state.TryGetProperty(field, out JsonElement value)
                    && value.ValueKind == JsonValueKind.Object
                    && value.TryGetProperty("direction", out JsonElement direction)
                    && direction.ValueKind == JsonValueKind.String)
                {
                    return Enum.Parse<Direction>(direction.GetString()!);
                }
            }

            return null;
        }
    }
}
