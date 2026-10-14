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

        /// <summary>The original's <c>CNT_CMaskResRShadow</c>, <c>1 &lt;&lt; 17</c>.</summary>
        private const long ShadowReceiverMask = 1L << 17;

        private readonly Dictionary<string, AnimationTable> _tables;
        private readonly Dictionary<string, ObjectLook> _looks;

        private readonly Shadows _shadows;

        private AnimationLibrary(Dictionary<string, AnimationTable> tables, Dictionary<string, ObjectLook> looks, IReadOnlyList<EffectLayer> explosion, Shadows shadows)
        {
            _shadows = shadows;
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

        /// <summary>Gets one of the game module's shadow animations, such as <c>SfxShSm</c>.</summary>
        /// <param name="name">The animation's name in the game module.</param>
        /// <returns>The page and frames.</returns>
        /// <exception cref="KeyNotFoundException">There is no such shadow.</exception>
        public (string Page, IReadOnlyList<string> Frames) ShadowNamed(string name)
        {
            Shadow shadow = _shadows["GMod." + name];
            return (shadow.Page, shadow.Frames);
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
            Shadows shadows = ShadowsOf(root);
            Dictionary<string, AnimationTable> tables = new(StringComparer.Ordinal)
            {
                [PlayerTable] = StateMap(classTables, "CGameObjPlayer.SMap", "SpritePlayer", shadows),
                [GreenMonsterTable] = StateMap(classTables, "CGameObjGrMon.SMap", "SpriteMonGreen", shadows),
                [BlueMonsterTable] = StateMap(classTables, "CGameObjMonBlue.SMap", "SpriteMonBlue", shadows),
                [BioMassTable] = StateMap(classTables, "CGameObjBioMass.SMap", "SpriteObjBio", shadows),
                [TeleporterTable] = StateMap(classTables, "CGameObjTPort.SMap", "SpriteObjTPort", shadows),
                [WaterDropTable] = StateMap(classTables, "CGameObjWaterDrop.SMap", "SpriteObjWaterDrop", shadows),
                [GreenBeetleTable] = StateMap(typeFunctions, "GameObjSmBeetle.GameObjSmGreen", "SpriteObjSmBeetle", shadows),
                [BlueBeetleTable] = StateMap(typeFunctions, "GameObjSmBeetle.GameObjSmBlue", "SpriteObjSmBeetle", shadows),
                [GrenadeTable] = TypeTable(Required(classTables, "CGameObjFallBase.GrType"), shadows),
                [MineTable] = TypeTable(Required(classTables, "CGameObjFallBase.MineType"), shadows),
                [BigBombTable] = TypeTable(Required(classTables, "CGameObjFallBase.TypeBBomb"), shadows),
                [RocketLeftTable] = SingleClipTable(Required(typeFunctions, "GameObjWeap.GameWeapRockLeft"), FlyState, null, shadows),
                [RocketRightTable] = SingleClipTable(Required(typeFunctions, "GameObjWeap.GameWeapRockRight"), FlyState, null, shadows),
            };

            Dictionary<string, ObjectLook> looks = [with(StringComparer.Ordinal)];
            JsonElement effects = root.TryGetProperty("effects", out JsonElement recovered) ? recovered : default;
            foreach (JsonProperty registration in root.GetProperty("objectTypes").EnumerateObject())
            {
                AnimationTable? table = RegisteredTable(registration.Value, shadows);
                if (table is not null)
                {
                    tables[registration.Name] = table;
                }

                looks[registration.Name] = LookOf(registration.Value, effects);
            }

            List<EffectLayer> explosion = effects.ValueKind == JsonValueKind.Object
                && effects.TryGetProperty("SfxExpl", out JsonElement layers)
                ? [.. layers.EnumerateArray().Select(EffectLayerOf)]
                : [];
            return new AnimationLibrary(tables, looks, explosion, shadows);
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

        /// <summary>
        /// Reads the game module's shadow animations, <c>SfxShBig</c>, <c>SfxShMed</c> and
        /// <c>SfxShSm</c>, each an <c>AnimMake</c> on <c>SpriteParts</c>.
        /// </summary>
        private static Shadows ShadowsOf(JsonElement root)
        {
            Shadows shadows = new(StringComparer.Ordinal);
            if (!root.TryGetProperty("effects", out JsonElement effects))
            {
                return shadows;
            }

            foreach (JsonProperty effect in effects.EnumerateObject())
            {
                if (effect.Name.StartsWith("SfxSh", StringComparison.Ordinal)
                    && effect.Value.ValueKind == JsonValueKind.Object
                    && effect.Value.TryGetProperty("args", out JsonElement args)
                    && effect.Value.TryGetProperty("call", out JsonElement call)
                    && call.ValueKind == JsonValueKind.Object
                    && call.TryGetProperty("of", out JsonElement page)
                    && page.TryGetProperty("args", out JsonElement pageArgs))
                {
                    shadows["GMod." + effect.Name] = new Shadow(
                        pageArgs[0].GetString() ?? string.Empty,
                        [.. args[0].EnumerateArray().Select(name => name.GetString() ?? string.Empty)]);
                }
            }

            return shadows;
        }

        /// <summary>Reads a state's shadow: its <c>DInds</c> or, for the player, its <c>DList</c>.</summary>
        private static Shadow? ShadowOf(JsonElement state, Shadows shadows)
        {
            foreach (string field in (string[])["DInds", "DList"])
            {
                if (state.TryGetProperty(field, out JsonElement value)
                    && value.ValueKind == JsonValueKind.Object
                    && value.TryGetProperty("ref", out JsonElement name)
                    && shadows.TryGetValue(name.GetString() ?? string.Empty, out Shadow? shadow))
                {
                    return shadow;
                }
            }

            return null;
        }

        private static ObjectLook LookOf(JsonElement registration, JsonElement effects)
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
            JsonElement parameter = registration.TryGetProperty("Prm", out JsonElement prm) ? prm : default;
            long? mask = MaskOf(parameter);
            bool fallObject = registration.TryGetProperty("Type", out JsonElement type)
                && type.ValueKind == JsonValueKind.Object
                && type.TryGetProperty("ref", out JsonElement typeName)
                && typeName.GetString() == "GameObjs.CGameObjFallBase";
            return new ObjectLook(
                backdrop,
                depth,
                mask is long bits && (bits & ShadowReceiverMask) != 0,
                fallObject,
                LayerOf(parameter, "GlowAnm", effects),
                LayerOf(parameter, "OverAnm", effects));
        }

        /// <summary>
        /// Reads a type's extra animation: a recovered <c>(speed, AnimMake(...), page)</c> tuple, or
        /// a reference to one of the game module's, such as <c>GMod.SfxGlowBon</c>.
        /// </summary>
        private static LayerAnimation? LayerOf(JsonElement parameter, string field, JsonElement effects)
        {
            if (parameter.ValueKind != JsonValueKind.Object || !parameter.TryGetProperty(field, out JsonElement value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Object
                && value.TryGetProperty("ref", out JsonElement reference)
                && reference.GetString() is string name
                && name.StartsWith("GMod.", StringComparison.Ordinal)
                && effects.ValueKind == JsonValueKind.Object
                && effects.TryGetProperty(name["GMod.".Length..], out JsonElement shared))
            {
                value = shared;
            }

            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 3 || value[0].ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            JsonElement page = value[2];
            string? pageName = page.TryGetProperty("page", out JsonElement direct)
                ? direct.GetString()
                : page.TryGetProperty("args", out JsonElement args) ? args[0].GetString() : null;
            return value[1].TryGetProperty("args", out JsonElement frames) && pageName is not null
                ? new LayerAnimation(pageName, [.. frames[0].EnumerateArray().Select(frame => frame.GetString() ?? string.Empty)], value[0].GetDouble())
                : null;
        }

        /// <summary>
        /// Reads a registration's collision mask: a type's <c>ulCMask</c>, or a cell type's third
        /// constructor argument, either a number or a recovered <c>OR</c> of numbers.
        /// </summary>
        private static long? MaskOf(JsonElement parameter)
        {
            return parameter.ValueKind != JsonValueKind.Object
                ? null
                : parameter.TryGetProperty("ulCMask", out JsonElement mask)
                ? Evaluate(mask)
                : parameter.TryGetProperty("args", out JsonElement args) && args.ValueKind == JsonValueKind.Array && args.GetArrayLength() > 2
                ? Evaluate(args[2])
                : null;
        }

        private static long? Evaluate(JsonElement value)
        {
            return value.ValueKind == JsonValueKind.Number
                ? value.GetInt64()
                : value.ValueKind == JsonValueKind.Object
                && value.TryGetProperty("op", out JsonElement op)
                && op.GetString() == "OR"
                && Evaluate(value.GetProperty("left")) is long left
                && Evaluate(value.GetProperty("right")) is long right
                ? left | right
                : null;
        }

        private static JsonElement Required(JsonElement container, string name)
        {
            return container.TryGetProperty(name, out JsonElement value)
                ? value
                : throw new InvalidDataException($"The animation data has no '{name}' table.");
        }

        private static AnimationTable StateMap(JsonElement container, string name, string page, Shadows shadows)
        {
            return new AnimationTable(page, States(Required(container, name), shadows));
        }

        /// <summary>Reads a recovered <c>CType</c> that carries a page and a state map.</summary>
        private static AnimationTable TypeTable(JsonElement type, Shadows shadows)
        {
            return new AnimationTable(PageOf(type) ?? throw new InvalidDataException("A type table has no page."), States(type.GetProperty("SMap"), shadows));
        }

        /// <summary>Reads a recovered type whose single animation is its <c>AList</c>.</summary>
        private static AnimationTable SingleClipTable(JsonElement type, string state, string? page, Shadows shadows)
        {
            string resolvedPage = PageOf(type) ?? page ?? throw new InvalidDataException("A single-clip table has no page.");
            AnimationClip clip = Clip(type, variantsPerName: false, shadows);
            return new AnimationTable(resolvedPage, new Dictionary<string, AnimationClip>(StringComparer.Ordinal) { [state] = clip });
        }

        /// <summary>
        /// Reads an archival registration, <c>CObjRepInfo</c>. Behaviour classes with a state map of
        /// their own (the player, the monsters, the bio mass, the teleporter) carry none here.
        /// </summary>
        private static AnimationTable? RegisteredTable(JsonElement registration, Shadows shadows)
        {
            if (!registration.TryGetProperty("Prm", out JsonElement parameter) || parameter.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (parameter.TryGetProperty("SMap", out JsonElement stateMap) && stateMap.ValueKind == JsonValueKind.Object)
            {
                return TypeTable(parameter, shadows);
            }

            string typeName = registration.TryGetProperty("Type", out JsonElement type) && type.TryGetProperty("ref", out JsonElement reference)
                ? reference.GetString() ?? string.Empty
                : string.Empty;
            return typeName switch
            {
                // Terrain picks one still frame by editor index: AList is the variants.
                "GameCells.CGameCellBase" when parameter.TryGetProperty("AList", out _) => SingleClipTable(parameter, StillState, null, shadows) with
                {
                    States = new Dictionary<string, AnimationClip>(StringComparer.Ordinal)
                    {
                        [StillState] = Clip(parameter, variantsPerName: true, shadows) with { FrameMilliseconds = [0] },
                    },
                },
                "GameCellPipes.CGameCellPipes" => new AnimationTable("SpriteCellPipes", new Dictionary<string, AnimationClip>(StringComparer.Ordinal)
                {
                    [StillState] = Clip(parameter, variantsPerName: true, shadows),
                }),
                _ => null,
            };
        }

        private static Dictionary<string, AnimationClip> States(JsonElement stateMap, Shadows shadows)
        {
            Dictionary<string, AnimationClip> states = [with(StringComparer.Ordinal)];
            foreach (JsonProperty state in stateMap.EnumerateObject())
            {
                if (state.Value.ValueKind == JsonValueKind.Object && state.Value.TryGetProperty("AList", out _))
                {
                    states[state.Name] = Clip(state.Value, variantsPerName: false, shadows);
                }
            }

            return states;
        }

        private static AnimationClip Clip(JsonElement state, bool variantsPerName, Shadows shadows)
        {
            Shadow? shadow = ShadowOf(state, shadows);
            return new AnimationClip(
                PageOf(state),
                Frames(state.GetProperty("AList"), variantsPerName),
                state.TryGetProperty("FList", out JsonElement fly) && fly.ValueKind == JsonValueKind.Array ? Frames(fly, variantsPerName: false) : null,
                FrameTimes(state),
                state.TryGetProperty("AEnd", out JsonElement end) && end.ValueKind == JsonValueKind.String ? end.GetString() : null,
                state.TryGetProperty("AEnd", out JsonElement handler) && handler.ValueKind == JsonValueKind.Object && handler.TryGetProperty("ref", out JsonElement name)
                    ? (name.GetString() ?? string.Empty).Split('.')[^1]
                    : null,
                DirectionOf(state),
                shadow?.Frames,
                shadow?.Page);
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

        /// <summary>A shadow animation: its page and frames.</summary>
        private sealed record Shadow(string Page, IReadOnlyList<string> Frames);

        /// <summary>The shadow animations by their recovered reference, such as <c>GMod.SfxShMed</c>.</summary>
        private sealed class Shadows(StringComparer comparer) : Dictionary<string, Shadow>(comparer);
    }
}
