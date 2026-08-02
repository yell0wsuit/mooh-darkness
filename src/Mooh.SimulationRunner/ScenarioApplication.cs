using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

using Mooh.Core.Content;

namespace Mooh.SimulationRunner
{
    /// <summary>Runs scenario files selected by command-line paths.</summary>
    public static class ScenarioApplication
    {
        /// <summary>Runs the deterministic scenario command-line application.</summary>
        /// <param name="arguments">A scenario path, or <c>level-board</c> followed by a level path.</param>
        /// <param name="output">The pass/fail summary destination.</param>
        /// <param name="error">The usage and failure diagnostic destination.</param>
        /// <returns>Zero on success, one on scenario failure, or two on usage/path failure.</returns>
        /// <exception cref="ArgumentNullException">
        /// Any argument is <see langword="null"/>.
        /// </exception>
        public static int Run(
            IReadOnlyList<string> arguments,
            TextWriter output,
            TextWriter error)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(error);

            if (arguments.Count == 2
                && string.Equals(arguments[0], "level-board", StringComparison.Ordinal))
            {
                return RunBoard(arguments, output, error);
            }

            if (arguments.Count >= 1 && string.Equals(arguments[0], "play", StringComparison.Ordinal))
            {
                return RunPlay(arguments, output, error);
            }

            if (arguments.Count != 1)
            {
                WriteUsage(error);
                return 2;
            }

            string selectedPath = arguments[0];
            string[] files;
            if (File.Exists(selectedPath))
            {
                files = [selectedPath];
            }
            else if (Directory.Exists(selectedPath))
            {
                try
                {
                    files =
                    [
                        .. Directory.EnumerateFiles(selectedPath, "*", SearchOption.TopDirectoryOnly)
                            .Where(path => string.Equals(
                                Path.GetExtension(path),
                                ".json",
                                StringComparison.OrdinalIgnoreCase))
                            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal),
                    ];
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    error.WriteLine($"Unable to enumerate scenario directory '{selectedPath}': {exception.Message}");
                    return 2;
                }

                if (files.Length == 0)
                {
                    error.WriteLine($"No top-level JSON scenario files found in '{selectedPath}'.");
                    return 2;
                }
            }
            else
            {
                error.WriteLine($"Scenario path does not exist: '{selectedPath}'.");
                WriteUsage(error);
                return 2;
            }

            bool failed = false;
            foreach (string file in files)
            {
                ScenarioRunResult result = RunFile(file);
                if (result.Passed)
                {
                    output.WriteLine($"PASS {result.Name}");
                    continue;
                }

                failed = true;
                output.WriteLine($"FAIL {result.Name}");
                error.WriteLine(result.Diagnostic);
            }

            return failed ? 1 : 0;
        }

        private static int RunBoard(
            IReadOnlyList<string> arguments,
            TextWriter output,
            TextWriter error)
        {
            if (arguments.Count != 2)
            {
                WriteUsage(error);
                return 2;
            }

            string path = arguments[1];
            if (!File.Exists(path))
            {
                error.WriteLine($"Level path does not exist: '{path}'.");
                WriteUsage(error);
                return 2;
            }

            try
            {
                string json = File.ReadAllText(path);
                LevelDocument level = JsonLevelLoader.Load(json, path);
                output.Write(LevelBoardRenderer.Render(level, Path.GetFileName(path)));
                return 0;
            }
            catch (LevelValidationException exception)
            {
                error.WriteLine(exception.Message);
                return 2;
            }
            catch (IOException exception)
            {
                error.WriteLine($"{path}: Unable to read level: {exception.Message}");
                return 2;
            }
            catch (UnauthorizedAccessException exception)
            {
                error.WriteLine($"{path}: Unable to read level: {exception.Message}");
                return 2;
            }
        }

        private static ScenarioRunResult RunFile(string path)
        {
            string fallbackName = Path.GetFileNameWithoutExtension(path);
            try
            {
                string json = File.ReadAllText(path);
                ScenarioDocument scenario = ScenarioLoader.Load(json, path);
                return ScenarioExecutor.Run(scenario, path);
            }
            catch (ScenarioValidationException exception)
            {
                return new ScenarioRunResult(
                    fallbackName,
                    path,
                    false,
                    ScenarioFailureKind.Validation,
                    exception.Message);
            }
            catch (IOException exception)
            {
                return new ScenarioRunResult(
                    fallbackName,
                    path,
                    false,
                    ScenarioFailureKind.Validation,
                    $"{path}: Unable to read scenario: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                return new ScenarioRunResult(
                    fallbackName,
                    path,
                    false,
                    ScenarioFailureKind.Validation,
                    $"{path}: Unable to read scenario: {exception.Message}");
            }
        }

        private static void WriteUsage(TextWriter error)
        {
            error.WriteLine("Usage: Mooh.SimulationRunner <scenario.json|directory>");
            error.WriteLine("       Mooh.SimulationRunner level-board <level.json>");
            error.WriteLine("       Mooh.SimulationRunner play [manifest.json] [--level N] [--port N]");
        }

        private static int RunPlay(
            IReadOnlyList<string> arguments,
            TextWriter output,
            TextWriter error)
        {
            string manifestPath = Path.Combine("assets", "levels", "manifest.json");
            int level = 1;
            int port = 8080;
            for (int index = 1; index < arguments.Count; index++)
            {
                string argument = arguments[index];
                if (string.Equals(argument, "--level", StringComparison.Ordinal)
                    || string.Equals(argument, "--port", StringComparison.Ordinal))
                {
                    if (index + 1 >= arguments.Count
                        || !int.TryParse(arguments[index + 1], out int value)
                        || value < 1)
                    {
                        error.WriteLine($"Option '{argument}' requires a positive integer value.");
                        return 2;
                    }

                    if (string.Equals(argument, "--level", StringComparison.Ordinal))
                    {
                        level = value;
                    }
                    else
                    {
                        port = value;
                    }

                    index++;
                }
                else
                {
                    manifestPath = argument;
                }
            }

            LoadedLevelPack pack;
            try
            {
                pack = JsonLevelPackLoader.Load(manifestPath);
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException or LevelValidationException)
            {
                error.WriteLine($"Unable to load level pack '{manifestPath}': {exception.Message}");
                return 2;
            }

            int levelCount = pack.Levels?.Count ?? 0;
            if (level > levelCount)
            {
                error.WriteLine($"Level {level} is outside the pack, which holds {levelCount} levels.");
                return 2;
            }

            using CancellationTokenSource cancellation = new();
            void Handler(object? sender, ConsoleCancelEventArgs eventArguments)
            {
                eventArguments.Cancel = true;
                cancellation.Cancel();
            }

            Console.CancelKeyPress += Handler;
            try
            {
                new PlayServer(new PlaySession(pack, level - 1)).Listen(port, output, cancellation.Token);
                return 0;
            }
            catch (HttpListenerException exception)
            {
                error.WriteLine($"Unable to listen on port {port}: {exception.Message}");
                return 2;
            }
            finally
            {
                Console.CancelKeyPress -= Handler;
            }
        }
    }
}
