using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SC4ModdingSuite.Mcp.Tools;

namespace SC4ModdingSuite.Mcp;

/// <summary>
/// An in-process Model Context Protocol server over the "Streamable HTTP" transport
/// (https://modelcontextprotocol.io/specification - the current standard HTTP-based MCP
/// transport): a single <c>POST /mcp</c> endpoint that accepts one JSON-RPC message per
/// request and returns the response directly as JSON (no SSE upgrade - none of this app's
/// tools ever need to push an unsolicited message to the client, so a plain
/// request/response body is sufficient and simpler than a streaming one).
///
/// <para>
/// Runs inside this same GUI process, started/stopped from the MCP Server panel's own
/// START/STOP buttons - there is no separate process or command-line mode for this
/// (see <see cref="Views.McpServerDialog"/>). A real MCP client (Claude Desktop, Claude
/// Code, or any other HTTP-capable MCP client) is configured with this server's own URL
/// (<see cref="Url"/>) and connects to it directly, the same way it would to any other
/// remote/HTTP MCP server - the difference is this one happens to be running locally,
/// inside this app, rather than on a separate host.
/// </para>
///
/// <para>
/// Only ever binds to the loopback address (<c>127.0.0.1</c>), never <c>0.0.0.0</c>/<c>+</c>:
/// this keeps the server unreachable from the network (security - nothing outside this
/// machine can ever reach it) and, as a direct consequence of binding strictly to loopback,
/// avoids the URL-ACL/administrator-privilege requirement <see cref="HttpListener"/>
/// otherwise has on Windows for non-loopback prefixes.
/// </para>
/// </summary>
public sealed class McpHttpServer : IDisposable
{
    private const string SessionHeaderName = "Mcp-Session-Id";

    private readonly ToolRegistry _registry = ToolRegistry.BuildDefault();
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public bool IsRunning => _listener is { IsListening: true };
    public int Port { get; private set; }

    /// <summary>The URL a real MCP client's own config should point at - null while not running.</summary>
    public string? Url => IsRunning ? $"http://127.0.0.1:{Port}/mcp" : null;

    /// <summary>Starts listening on <paramref name="port"/> - throws if it couldn't start (most commonly: the port is already in use).</summary>
    public void Start(int port)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Already running - stop it first.");
        }

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            throw new InvalidOperationException($"Couldn't bind to port {port}: {ex.Message}", ex);
        }

        _listener = listener;
        Port = port;
        _cts = new CancellationTokenSource();
        _ = ListenLoopAsync(listener, _cts.Token);
    }

    private async Task ListenLoopAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        Log($"MCP HTTP server listening on http://127.0.0.1:{Port}/mcp");

        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested || !listener.IsListening)
            {
                break; // Stop() was called - GetContextAsync throws once the listener is closed.
            }
            catch (Exception ex)
            {
                Log($"Listener error: {ex.Message}");
                continue;
            }

            // Fire-and-forget: each request is independent (no shared per-connection state
            // this server needs), so handling them concurrently rather than one at a time
            // keeps one slow tool call from blocking unrelated requests.
            _ = HandleRequestAsync(context);
        }

        Log("MCP HTTP server stopped listening.");
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            if (request.Url?.AbsolutePath != "/mcp")
            {
                await WriteStatusOnlyAsync(response, 404);
                return;
            }

            switch (request.HttpMethod)
            {
                case "POST":
                    await HandlePostAsync(request, response);
                    break;
                case "DELETE":
                    // Session termination - this server keeps no real per-session state
                    // (every tool call already takes a full path/TGI and opens its own
                    // package fresh), so there's nothing to actually clean up; acknowledging
                    // it is enough to satisfy a client that sends it.
                    await WriteStatusOnlyAsync(response, 204);
                    break;
                default:
                    await WriteStatusOnlyAsync(response, 405);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"Request handling error: {ex}");
            try
            {
                await WriteStatusOnlyAsync(context.Response, 500);
            }
            catch
            {
                // Response may already be closed/broken - nothing more to do.
            }
        }
    }

    private async Task HandlePostAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        string body;
        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
        {
            body = await reader.ReadToEndAsync();
        }

        var responseNode = await McpMessageHandler.HandleAsync(body, _registry);

        // initialize gets a fresh session id on the response header, per the Streamable
        // HTTP transport - this server doesn't actually need to validate it comes back on
        // later requests (no real per-session state to protect - see the DELETE handler's
        // own comment), so it's issued for client compatibility without being enforced.
        if (JsonNode.Parse(body) is { } requestNode && requestNode["method"]?.GetValue<string>() == "initialize")
        {
            response.Headers[SessionHeaderName] = Guid.NewGuid().ToString("N");
        }

        if (responseNode is null)
        {
            // A notification - JSON-RPC 2.0 says no response body, but the HTTP request
            // itself still needs *some* status code.
            await WriteStatusOnlyAsync(response, 202);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(responseNode.ToJsonString());
        response.StatusCode = 200;
        response.ContentType = "application/json";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.OutputStream.Close();
    }

    private static async Task WriteStatusOnlyAsync(HttpListenerResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        response.ContentLength64 = 0;
        await Task.Run(response.OutputStream.Close);
    }

    /// <summary>Stops listening - in-flight requests are abandoned (this server has nothing that needs a graceful drain: every tool call is a short-lived, self-contained file operation).</summary>
    public void Stop()
    {
        if (_listener is null)
        {
            return;
        }

        try
        {
            _cts?.Cancel();
            _listener.Stop();
            _listener.Close();
        }
        catch
        {
            // Best-effort.
        }
        finally
        {
            _cts?.Dispose();
            _listener = null;
            _cts = null;
        }
    }

    public void Dispose() => Stop();

    /// <summary>Diagnostic logging - this server has no stdout-purity constraint the way the (removed) stdio transport did, so this just goes to stderr for visibility without interfering with anything.</summary>
    internal static void Log(string message) => Console.Error.WriteLine($"[sc4-mcp-http] {message}");
}
