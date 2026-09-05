using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SC4ModdingSuite.Mcp;

namespace SC4ModdingSuite.Models;

public sealed record McpServerStartResult(bool Success, string Message, int ToolCount);

/// <summary>
/// Starts/stops the in-process <see cref="McpHttpServer"/> from the GUI's own MCP Server
/// panel, and performs a real HTTP handshake against it right after starting (the same
/// opening sequence a real MCP client would send: <c>initialize</c> →
/// <c>notifications/initialized</c> → <c>tools/list</c>) to confirm it actually answers
/// correctly - not just that <see cref="McpHttpServer.Start"/> didn't throw.
/// </summary>
public sealed class McpServerLauncher : IDisposable
{
    private readonly McpHttpServer _server = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public bool IsRunning => _server.IsRunning;
    public string? Url => _server.Url;

    public async Task<McpServerStartResult> StartAsync(int port)
    {
        if (IsRunning)
        {
            return new McpServerStartResult(false, "Already running - stop it first.", 0);
        }

        try
        {
            _server.Start(port);
        }
        catch (Exception ex)
        {
            return new McpServerStartResult(false, ex.Message, 0);
        }

        try
        {
            var toolCount = await HandshakeAsync();
            return new McpServerStartResult(true, "Running - handshake OK.", toolCount);
        }
        catch (Exception ex)
        {
            _server.Stop();
            return new McpServerStartResult(false, $"Started, but the handshake failed: {ex.Message}", 0);
        }
    }

    /// <summary>Sends the same opening sequence a real MCP client sends (initialize → notifications/initialized → tools/list) over real HTTP and returns the reported tool count.</summary>
    private async Task<int> HandshakeAsync()
    {
        var url = _server.Url ?? throw new InvalidOperationException("Not started.");

        var initResponse = await PostAsync(url, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05"}}""");
        var initNode = System.Text.Json.Nodes.JsonNode.Parse(initResponse) ?? throw new InvalidOperationException("initialize: empty/invalid response.");
        if (initNode["error"] is { } initError)
        {
            throw new InvalidOperationException($"initialize error: {initError["message"]}");
        }

        // A notification - no meaningful body expected back.
        await PostAsync(url, """{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        var toolsResponse = await PostAsync(url, """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");
        var toolsNode = System.Text.Json.Nodes.JsonNode.Parse(toolsResponse) ?? throw new InvalidOperationException("tools/list: empty/invalid response.");
        if (toolsNode["error"] is { } toolsError)
        {
            throw new InvalidOperationException($"tools/list error: {toolsError["message"]}");
        }

        return toolsNode["result"]?["tools"]?.AsArray().Count ?? 0;
    }

    private async Task<string> PostAsync(string url, string jsonBody)
    {
        using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(url, content);
        return await response.Content.ReadAsStringAsync();
    }

    public void Stop() => _server.Stop();

    public void Dispose()
    {
        _server.Dispose();
        _http.Dispose();
    }
}
