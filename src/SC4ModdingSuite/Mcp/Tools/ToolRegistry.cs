using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using SC4ModdingSuite.Mcp;

namespace SC4ModdingSuite.Mcp.Tools;

public sealed record ToolDefinition(string Name, string Description, JsonObject InputSchema, Func<JsonNode?, JsonNode> Handler);

/// <summary>Holds every registered tool and implements the "tools/list"/"tools/call" MCP methods generically - see <see cref="Tools.DbpfTools"/> for the actual SC4-specific tools.</summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, ToolDefinition> _tools = new();

    public void Register(ToolDefinition tool) => _tools[tool.Name] = tool;

    public JsonNode HandleToolsList()
    {
        var array = new JsonArray();
        foreach (var tool in _tools.Values)
        {
            array.Add(new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.InputSchema.DeepClone(),
            });
        }

        return new JsonObject { ["tools"] = array };
    }

    public JsonNode HandleToolsCall(JsonNode? paramsNode)
    {
        var name = paramsNode?["name"]?.GetValue<string>();
        if (name is null)
        {
            return ErrorContent("Missing required \"name\" field in tools/call params.");
        }

        if (!_tools.TryGetValue(name, out var tool))
        {
            return ErrorContent($"Unknown tool: {name}");
        }

        var arguments = paramsNode?["arguments"];

        try
        {
            var resultNode = tool.Handler(arguments);
            return new JsonObject
            {
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = resultNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    },
                },
                ["isError"] = false,
            };
        }
        catch (Exception ex)
        {
            McpHttpServer.Log($"Tool '{name}' failed: {ex}");
            return ErrorContent(ex.Message);
        }
    }

    private static JsonObject ErrorContent(string message) => new()
    {
        ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = message } },
        ["isError"] = true,
    };

    public static ToolRegistry BuildDefault()
    {
        var registry = new ToolRegistry();
        DbpfTools.Register(registry);
        return registry;
    }
}
