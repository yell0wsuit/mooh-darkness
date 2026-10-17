using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Desktop.Platform.Graphics;

namespace Mooh.Desktop
{
    /// <summary>The desktop command line.</summary>
    /// <param name="LevelIndex">The zero-based level to start on.</param>
    /// <param name="Difficulty">The recovered difficulty profile.</param>
    /// <param name="Renderer">The renderer to force, or <see langword="null"/> to pick the best.</param>
    /// <param name="QuitAfterFrames">The frame count after which to quit, for scripted runs.</param>
    /// <param name="ScreenshotPath">Where to save the last frame as a PNG, for scripted runs.</param>
    /// <param name="Script">
    /// Scripted input, for scripted runs: from each game time in seconds, the keys held until the next.
    /// </param>
    /// <param name="QuitAfterSeconds">The game time after which to quit, for scripted runs.</param>
    /// <param name="DirectLaunch">Whether an explicit level bypasses the menu flow.</param>
    /// <param name="Screen">An optional screen to inspect in a scripted render.</param>
    internal sealed record DesktopOptions(
        int LevelIndex,
        DifficultySpeeds Difficulty,
        GraphicsBackendKind? Renderer,
        int? QuitAfterFrames,
        string? ScreenshotPath,
        IReadOnlyList<(double At, PlayerInputState Input)>? Script = null,
        double? QuitAfterSeconds = null,
        bool DirectLaunch = false,
        string? Screen = null)
    {
        /// <summary>The usage line.</summary>
        internal const string Usage =
            "Mooh [--level N] [--difficulty 0|1|2] [--renderer metal|vulkan|opengl|software] [--frames N | --seconds S] [--screenshot file.png] [--input 2=r,3.5=rf,4=] [--screen intro|main|map|options|scores|prelevel|pause|statistics|complete|outro]";

        private static readonly Dictionary<string, GraphicsBackendKind> Renderers = new(StringComparer.Ordinal)
        {
            ["metal"] = GraphicsBackendKind.Metal,
            ["vulkan"] = GraphicsBackendKind.Vulkan,
            ["opengl"] = GraphicsBackendKind.OpenGL,
            ["software"] = GraphicsBackendKind.Software,
        };

        /// <summary>Reads the command line.</summary>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The options.</returns>
        /// <exception cref="ArgumentException">An argument is unknown, incomplete or out of range.</exception>
        internal static DesktopOptions Parse(IReadOnlyList<string> arguments)
        {
            DesktopOptions options = new(0, DifficultySpeeds.Standard, null, null, null);
            for (int index = 0; index < arguments.Count; index++)
            {
                string name = arguments[index];
                string value = index + 1 < arguments.Count
                    ? arguments[++index]
                    : throw new ArgumentException($"Option '{name}' needs a value. Usage: {Usage}");
                options = name switch
                {
                    "--level" => options with { LevelIndex = Positive(name, value) - 1, DirectLaunch = true },
                    "--screen" => options with { Screen = value },
                    "--difficulty" => options with { Difficulty = DifficultyOf(value) },
                    "--renderer" => options with
                    {
                        Renderer = Renderers.TryGetValue(value, out GraphicsBackendKind kind)
                            ? kind
                            : throw new ArgumentException($"Unknown renderer '{value}'. Usage: {Usage}"),
                    },
                    "--frames" => options with { QuitAfterFrames = Positive(name, value) },
                    "--seconds" => options with
                    {
                        QuitAfterSeconds = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && seconds > 0
                            ? seconds
                            : throw new ArgumentException($"Option '--seconds' needs a positive number. Usage: {Usage}"),
                    },
                    "--screenshot" => options with { ScreenshotPath = value },
                    "--input" => options with { Script = ScriptOf(value) },
                    _ => throw new ArgumentException($"Unknown option '{name}'. Usage: {Usage}"),
                };
            }

            return options;
        }

        /// <summary>Gets the scripted input held at a time.</summary>
        /// <param name="seconds">The time since the game started.</param>
        /// <returns>The keys held then; none before the script's first entry.</returns>
        internal PlayerInputState ScriptedInput(double seconds)
        {
            PlayerInputState held = default;
            foreach ((double at, PlayerInputState input) in Script ?? [])
            {
                if (at <= seconds)
                {
                    held = input;
                }
            }

            return held;
        }

        private static List<(double, PlayerInputState)> ScriptOf(string value)
        {
            List<(double, PlayerInputState)> script = [];
            foreach (string entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = entry.Split('=');
                if (parts.Length != 2
                    || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double at)
                    || parts[1].Any(key => !"lurdf".Contains(key, StringComparison.Ordinal)))
                {
                    throw new ArgumentException($"Option '--input' needs entries like 2=r or 3.5=rf. Usage: {Usage}");
                }

                string keys = parts[1];
                script.Add((at, new PlayerInputState(keys.Contains('l', StringComparison.Ordinal), keys.Contains('u', StringComparison.Ordinal), keys.Contains('r', StringComparison.Ordinal), keys.Contains('d', StringComparison.Ordinal), keys.Contains('f', StringComparison.Ordinal))));
            }

            return script;
        }

        private static int Positive(string name, string value)
        {
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number > 0
                ? number
                : throw new ArgumentException($"Option '{name}' needs a positive number. Usage: {Usage}");
        }

        /// <summary>The recovered difficulty table: 0 reduced, 1 standard, 2 timed.</summary>
        private static DifficultySpeeds DifficultyOf(string value)
        {
            return value switch
            {
                "0" => DifficultySpeeds.DifficultyZero,
                "1" => DifficultySpeeds.Standard,
                "2" => DifficultySpeeds.Timed,
                _ => throw new ArgumentException($"Option '--difficulty' needs 0, 1 or 2. Usage: {Usage}"),
            };
        }
    }
}
