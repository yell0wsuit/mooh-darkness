using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;

using Mooh.Core.Player;

namespace Mooh.SimulationRunner
{
    /// <summary>
    /// Serves the interactive play shell on the loopback interface.
    /// </summary>
    /// <remarks>
    /// The browser is a presentation shim, not a second implementation: it contributes real
    /// key-down and key-up events, which a terminal cannot provide, and colour emoji rendering.
    /// Every gameplay decision stays in the deterministic simulation on this side.
    /// </remarks>
    /// <param name="session">The session driven by incoming frame requests.</param>
    public sealed class PlayServer(PlaySession session)
    {
        private readonly Lock _gate = new();
        private readonly PlaySession _session = session ?? throw new ArgumentNullException(nameof(session));

        /// <summary>Listens on the loopback interface until the process is stopped.</summary>
        /// <param name="port">The TCP port to bind.</param>
        /// <param name="output">The startup message destination.</param>
        /// <param name="cancellationToken">Stops the listener.</param>
        /// <exception cref="HttpListenerException">The port could not be bound.</exception>
        public void Listen(int port, TextWriter output, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(output);

            using HttpListener listener = new();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            using CancellationTokenRegistration registration = cancellationToken.Register(listener.Stop);
            output.WriteLine($"Playing on http://127.0.0.1:{port}/ — press Ctrl+C to stop.");

            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (Exception exception) when (exception is HttpListenerException or InvalidOperationException)
                {
                    return;
                }

                Handle(context);
            }
        }

        /// <summary>
        /// Produces the response for one request. Routing is separated from the socket so a frame
        /// can be requested directly, without a listener.
        /// </summary>
        /// <param name="path">The requested absolute path.</param>
        /// <param name="query">The parsed query string.</param>
        /// <returns>The response content type and body.</returns>
        /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
        public (string ContentType, string Body) Route(string path, NameValueCollection query)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(query);

            if (!string.Equals(path, "/frame", StringComparison.Ordinal))
            {
                return ("text/html; charset=utf-8", PlayPage.Html);
            }

            PlayFrame frame;
            lock (_gate)
            {
                switch (query["command"])
                {
                    case "restart":
                        _session.RestartLevel();
                        break;
                    case "next":
                        _session.AdvanceToNextLevel();
                        break;
                    default:
                        break;
                }

                frame = _session.Advance(new PlayerInputState(
                    IsHeld(query, "l"),
                    IsHeld(query, "u"),
                    IsHeld(query, "r"),
                    IsHeld(query, "d"),
                    IsHeld(query, "f")));
            }

            return (
                "application/json; charset=utf-8",
                JsonSerializer.Serialize(frame, PlayJsonSerializerContext.Default.PlayFrame));
        }

        private static bool IsHeld(NameValueCollection query, string name)
        {
            return string.Equals(query[name], "1", StringComparison.Ordinal);
        }

        private void Handle(HttpListenerContext context)
        {
            (string contentType, string body) = Route(
                context.Request.Url?.AbsolutePath ?? "/",
                context.Request.QueryString);
            byte[] payload = Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = payload.Length;
            context.Response.Headers.Add("Cache-Control", "no-store");
            try
            {
                context.Response.OutputStream.Write(payload);
            }
            catch (Exception exception) when (exception is IOException or HttpListenerException)
            {
                // The browser abandoned the frame request; the next one supersedes it.
            }
            finally
            {
                context.Response.Close();
            }
        }
    }
}
