using System;
using System.Globalization;
using System.Text.Json.Nodes;
using csDBPF;

namespace SC4ModdingSuite.Mcp.Tools;

internal static class ToolHelpers
{
    /// <summary>
    /// Parses a TGI argument the same flexible way dbpf-mcp's own tools accept it: either
    /// one "TTTTTTTT-GGGGGGGG-IIIIIIII" string under <paramref name="args"/>["tgi"], or
    /// separate "type"/"group"/"instance" hex-string fields.
    /// </summary>
    public static TGI RequireTgi(JsonNode? args)
    {
        var tgiText = args?["tgi"]?.GetValue<string>();
        if (tgiText is not null)
        {
            var parts = tgiText.Split('-');
            if (parts.Length != 3)
            {
                throw new ArgumentException($"\"tgi\" must be \"TTTTTTTT-GGGGGGGG-IIIIIIII\", got: {tgiText}");
            }

            return new TGI(ParseHex(parts[0], "type"), ParseHex(parts[1], "group"), ParseHex(parts[2], "instance"));
        }

        var typeText = args?["type"]?.GetValue<string>();
        var groupText = args?["group"]?.GetValue<string>();
        var instanceText = args?["instance"]?.GetValue<string>();
        if (typeText is null || groupText is null || instanceText is null)
        {
            throw new ArgumentException("Provide either \"tgi\" (\"TTTTTTTT-GGGGGGGG-IIIIIIII\") or all three of \"type\"/\"group\"/\"instance\".");
        }

        return new TGI(ParseHex(typeText, "type"), ParseHex(groupText, "group"), ParseHex(instanceText, "instance"));
    }

    public static uint ParseHex(string text, string fieldName)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        if (!uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException($"\"{fieldName}\" is not a valid hex value: {text}");
        }

        return value;
    }

    public static string RequirePath(JsonNode? args)
    {
        var path = args?["path"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Missing required \"path\" argument (a .dat/.SC4Lot/.SC4Model/.SC4Desc file).");
        }

        return path;
    }

    public static string TgiText(TGI tgi) => $"{tgi.TypeID:X8}-{tgi.GroupID:X8}-{tgi.InstanceID:X8}";

    public static JsonObject Schema(params (string Name, string Type, string Description, bool Required)[] properties)
    {
        var props = new JsonObject();
        var required = new JsonArray();
        foreach (var (name, type, description, isRequired) in properties)
        {
            props[name] = new JsonObject { ["type"] = type, ["description"] = description };
            if (isRequired)
            {
                required.Add(name);
            }
        }

        var schema = new JsonObject { ["type"] = "object", ["properties"] = props };
        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }
}
