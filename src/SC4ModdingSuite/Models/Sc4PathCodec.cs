using System;
using System.Collections.Generic;
using System.Globalization;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Port of Ilive Reader's <c>_path::DecodeSC4Path</c> (<c>or_dat/cl_path.cpp</c>,
/// ENT_SC4PATH 0x296678F7) - a plain-text, CRLF-line-delimited format describing a
/// transit route: a header (tag/version/transport-path count/[sim-path count if
/// version&gt;1]/path type), then that many "transport" segments (each an optional
/// "--name" comment line, type/elevation/two values/a coordinate-block count, then that
/// many Z,X,Y coordinate lines), then - reusing the <i>same</i> transport-path count again
/// (not a separate stop count, reproduced as-is) - that many "stop" segments (an optional
/// name, type/transit/class/two values, then a single Z,X,Y coordinate line).
///
/// Read-only, matching Ilive Reader: <c>_path</c> has no <c>EncodeSC4Path</c>, only decode.
/// </summary>
public static class Sc4PathCodec
{
    public sealed record Coord(int X, int Y, int Z)
    {
        public override string ToString() => $"{X},{Y},{Z}";
    }

    public sealed record TransportElement(string? Name, int TransportationType, int ElevationType, int Val1, int Val2, IReadOnlyList<Coord> Coords);

    public sealed record StopElement(string? Name, int StopType, int TransitType, int Class, int Val1, int Val2, Coord? Coord);

    public sealed record PathData(string Tag, float Version, int TransportPathCount, int? SimPath, int PathType,
        IReadOnlyList<TransportElement> Transports, IReadOnlyList<StopElement> Stops);

    public static bool TryDecode(byte[] input, out PathData data)
    {
        data = new PathData(string.Empty, 0, 0, null, 0, Array.Empty<TransportElement>(), Array.Empty<StopElement>());

        var text = System.Text.Encoding.Latin1.GetString(input);
        var lines = new Queue<string>(text.Split(new[] { "\r\n" }, StringSplitOptions.None));

        string? NextLine() => lines.Count > 0 ? lines.Dequeue() : null;

        var tag = NextLine();
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        var versionText = NextLine();
        if (string.IsNullOrEmpty(versionText))
        {
            return false;
        }

        var version = float.TryParse(versionText, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

        var transportPathText = NextLine();
        if (string.IsNullOrEmpty(transportPathText))
        {
            return false;
        }

        var transportPathCount = ParseLeadingInt(transportPathText);

        int? simPath = null;
        if (version > 1.0f)
        {
            var simPathText = NextLine();
            if (string.IsNullOrEmpty(simPathText))
            {
                return false;
            }

            simPath = ParseLeadingInt(simPathText);
        }

        var pathTypeText = NextLine();
        if (string.IsNullOrEmpty(pathTypeText))
        {
            return false;
        }

        var pathType = ParseLeadingInt(pathTypeText);

        var transports = new List<TransportElement>();
        for (var i = 0; i < transportPathCount; i++)
        {
            var line = NextLine();
            string? name = null;
            if (line is { Length: > 2 } && line.StartsWith("--", StringComparison.Ordinal))
            {
                name = line;
                line = NextLine();
            }

            var transportationType = ParseLeadingInt(line);
            var elevationType = ParseLeadingInt(NextLine());
            var val1 = ParseLeadingInt(NextLine());
            var val2 = ParseLeadingInt(NextLine());
            var blockNum = ParseLeadingInt(NextLine());

            var coords = new List<Coord>();
            for (var j = 0; j < blockNum; j++)
            {
                coords.Add(ParseCoord(NextLine()));
            }

            transports.Add(new TransportElement(name, transportationType, elevationType, val1, val2, coords));
        }

        var stops = new List<StopElement>();
        for (var i = 0; i < transportPathCount; i++)
        {
            var line = NextLine();
            string? name = null;
            if (line is { Length: > 2 } && line.StartsWith("--", StringComparison.Ordinal))
            {
                name = line;
                line = NextLine();
            }

            var stopType = ParseLeadingInt(line);
            var transitType = ParseLeadingInt(NextLine());
            var classValue = ParseLeadingInt(NextLine());
            var val1 = ParseLeadingInt(NextLine());
            var val2 = ParseLeadingInt(NextLine());
            var coord = ParseCoord(NextLine());

            stops.Add(new StopElement(name, stopType, transitType, classValue, val1, val2, coord));
        }

        data = new PathData(tag, version, transportPathCount, simPath, pathType, transports, stops);
        return true;
    }

    private static int ParseLeadingInt(string? text) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static Coord ParseCoord(string? line)
    {
        // DecodeSC4Path reads Z, then X, then Y, in that comma-separated order - reproduced
        // as-is (the field order in the resulting Coord record here is X,Y,Z for readability
        // elsewhere in this app, but which raw token maps to which axis matches the original
        // exactly).
        var parts = (line ?? string.Empty).Split(',');
        var z = parts.Length > 0 ? ParseLeadingInt(parts[0]) : 0;
        var x = parts.Length > 1 ? ParseLeadingInt(parts[1]) : 0;
        var y = parts.Length > 2 ? ParseLeadingInt(parts[2]) : 0;
        return new Coord(x, y, z);
    }
}
