using System;
using System.Collections.Generic;
using System.Globalization;

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
    internal sealed record DesktopOptions(
        int LevelIndex,
        DifficultySpeeds Difficulty,
        GraphicsBackendKind? Renderer,
        int? QuitAfterFrames,
        string? ScreenshotPath)
    {
        /// <summary>The usage line.</summary>
        internal const string Usage =
            "Mooh [--level N] [--difficulty 0|1|2] [--renderer metal|opengl|software] [--frames N] [--screenshot file.png]";

        private static readonly Dictionary<string, GraphicsBackendKind> Renderers = new(StringComparer.Ordinal)
        {
            ["metal"] = GraphicsBackendKind.Metal,
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
                    "--level" => options with { LevelIndex = Positive(name, value) - 1 },
                    "--difficulty" => options with { Difficulty = DifficultyOf(value) },
                    "--renderer" => options with
                    {
                        Renderer = Renderers.TryGetValue(value, out GraphicsBackendKind kind)
                            ? kind
                            : throw new ArgumentException($"Unknown renderer '{value}'. Usage: {Usage}"),
                    },
                    "--frames" => options with { QuitAfterFrames = Positive(name, value) },
                    "--screenshot" => options with { ScreenshotPath = value },
                    _ => throw new ArgumentException($"Unknown option '{name}'. Usage: {Usage}"),
                };
            }

            return options;
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
