using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SC4ModdingSuite.Mcp.Tools;

namespace SC4ModdingSuite.Mcp;

/// <summary>
/// Parses one JSON-RPC request/notification and dispatches it to the right
/// <see cref="ToolRegistry"/> method, independently of whatever transport carried it in
/// (see <see cref="McpHttpServer"/>, the only transport this app actually exposes) - the
/// message-level protocol logic (initialize/tools/list/tools/call, error codes) is the
/// same regardless of how the bytes arrived.
/// </summary>
public static class McpMessageHandler
{
    /// <summary>
    /// Handles one already-parsed JSON-RPC message and returns the response to send back,
    /// or <see langword="null"/> if none should be sent (a notification, or a request with
    /// no "id" - per JSON-RPC 2.0, only messages with an "id" ever get a response).
    /// </summary>
    public static async Task<JsonNode?> HandleAsync(JsonNode requestNode, ToolRegistry registry)
    {
        var idNode = requestNode["id"];
        var hasId = idNode is not null;
        var method = requestNode["method"]?.GetValue<string>();
        var paramsNode = requestNode["params"];

        if (method is null)
        {
            return hasId ? JsonRpcError(idNode, -32600, "Invalid Request: missing method") : null;
        }

        try
        {
            var result = await Dispatch(method, paramsNode, registry);
            return hasId ? JsonRpcSuccess(idNode, result) : null;
        }
        catch (McpMethodNotFoundException)
        {
            return hasId ? JsonRpcError(idNode, -32601, $"Method not found: {method}") : null;
        }
        catch (Exception ex)
        {
            McpHttpServer.Log($"Unhandled error dispatching '{method}': {ex}");
            return hasId ? JsonRpcError(idNode, -32603, $"Internal error: {ex.Message}") : null;
        }
    }

    /// <summary>Convenience for a raw, not-yet-parsed body - returns a JSON-RPC parse-error response if it isn't valid JSON, rather than throwing.</summary>
    public static async Task<JsonNode?> HandleAsync(string rawMessage, ToolRegistry registry)
    {
        JsonNode? requestNode;
        try
        {
            requestNode = JsonNode.Parse(rawMessage);
        }
        catch (JsonException)
        {
            return JsonRpcError(null, -32700, "Parse error");
        }

        return requestNode is null ? null : await HandleAsync(requestNode, registry);
    }

    private static Task<JsonNode?> Dispatch(string method, JsonNode? paramsNode, ToolRegistry registry) => method switch
    {
        "initialize" => Task.FromResult<JsonNode?>(HandleInitialize(paramsNode)),
        "notifications/initialized" => Task.FromResult<JsonNode?>(null),
        "ping" => Task.FromResult<JsonNode?>(new JsonObject()),
        "tools/list" => Task.FromResult<JsonNode?>(registry.HandleToolsList()),
        "tools/call" => Task.FromResult<JsonNode?>(registry.HandleToolsCall(paramsNode)),
        _ => throw new McpMethodNotFoundException(),
    };

    private static JsonNode HandleInitialize(JsonNode? paramsNode)
    {
        // Echoes back whichever protocolVersion the client itself requested, rather than
        // asserting one fixed version string here - a common, pragmatic MCP server pattern
        // for staying compatible as the spec's own dated protocol versions move forward.
        var requestedVersion = paramsNode?["protocolVersion"]?.GetValue<string>() ?? "2024-11-05";

        return new JsonObject
        {
            ["protocolVersion"] = requestedVersion,
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject(),
            },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "sc4-dbpf-mcp",
                ["version"] = "0.1.0",
            },
        };
    }

    public static JsonObject JsonRpcSuccess(JsonNode? id, JsonNode? result)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
        };
        response["result"] = result ?? new JsonObject();
        return response;
    }

    public static JsonObject JsonRpcError(JsonNode? id, int code, string message) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["error"] = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
        },
    };
}

internal sealed class McpMethodNotFoundException : Exception;
