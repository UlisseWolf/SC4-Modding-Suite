using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>One entry's data as captured for the clipboard: its original concrete type, TGI, and raw bytes.</summary>
public sealed class ClipboardEntryPayload
{
    public required string TypeName { get; init; }
    public required string TypeHex { get; init; }
    public required string GroupHex { get; init; }
    public required string InstanceHex { get; init; }
    public required string DataBase64 { get; init; }
}

/// <summary>Envelope written to the clipboard, self-identifying so "Paste" can tell this apart from arbitrary text.</summary>
public sealed class ClipboardEntriesDocument
{
    public string App { get; init; } = EntryClipboard.AppMarker;
    public int Version { get; init; } = 1;
    public List<ClipboardEntryPayload> Entries { get; init; } = new();
}

/// <summary>
/// Copy/paste of entries (full content) and of TGIs (identifiers only) via the system
/// clipboard, so entries can be transferred between two different SC4 package files opened
/// in separate runs of the app (this app edits one file at a time, so "copy in file A, open
/// file B, paste" is the mechanism for moving entries across files, rather than a
/// side-by-side multi-document view).
///
/// <para>
/// <b>Full entry copy</b> serializes each entry's original concrete csDBPF type name
/// (<see cref="Type.AssemblyQualifiedName"/>), TGI, and raw bytes as JSON, tagged with
/// <see cref="AppMarker"/> so a paste from an unrelated clipboard payload (e.g. some
/// random text) is safely rejected instead of producing garbage entries. Reconstructing
/// the entry as the same concrete type it started as (see
/// <see cref="DbpfService.AddEntryFromClipboard"/>) means it keeps working with every
/// existing preview/editor in the app (image, LTEXT, Exemplar properties, ...) after being
/// pasted, exactly as if it had always been part of the destination package.
/// </para>
///
/// <para>
/// <b>TGI-only copy</b> is deliberately plain, human-readable text
/// (<c>TGI: 0xTTTTTTTT-0xGGGGGGGG-0xIIIIIIII</c>) instead of JSON - it's meant to be
/// pasted either back into this app (to re-target an entry's TGI to match another one) or
/// into a text editor/notes/chat, so keeping it readable serves both.
/// </para>
/// </summary>
public static class EntryClipboard
{
    public const string AppMarker = "SC4ModdingSuite.EntriesClipboard";

    public static string SerializeEntries(IEnumerable<DBPFEntry> entries)
    {
        var document = new ClipboardEntriesDocument
        {
            Entries = entries.Select(entry => new ClipboardEntryPayload
            {
                TypeName = entry.GetType().AssemblyQualifiedName ?? entry.GetType().FullName ?? entry.GetType().Name,
                TypeHex = $"0x{entry.TGI.TypeID:X8}",
                GroupHex = $"0x{entry.TGI.GroupID:X8}",
                InstanceHex = $"0x{entry.TGI.InstanceID:X8}",
                DataBase64 = Convert.ToBase64String(entry.ByteData ?? Array.Empty<byte>()),
            }).ToList(),
        };

        return JsonSerializer.Serialize(document);
    }

    /// <summary>Parses a clipboard payload previously written by <see cref="SerializeEntries"/>; null if it isn't one.</summary>
    public static List<ClipboardEntryPayload>? TryDeserializeEntries(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<ClipboardEntriesDocument>(text);
            return document is { Entries.Count: > 0 } && document.App == AppMarker ? document.Entries : null;
        }
        catch
        {
            return null;
        }
    }

    public static string SerializeTgiOnly(IEnumerable<TGI> tgis) =>
        string.Join(
            Environment.NewLine,
            tgis.Select(t => $"TGI: 0x{t.TypeID:X8}-0x{t.GroupID:X8}-0x{t.InstanceID:X8}"));

    /// <summary>Parses the first "TGI: 0xT-0xG-0xI" line found in the clipboard text; null if none matches.</summary>
    public static (uint Type, uint Group, uint Instance)? TryParseSingleTgi(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var line = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(l => l.Contains("0x", StringComparison.OrdinalIgnoreCase));

        if (line is null)
        {
            return null;
        }

        var cleaned = line.Replace("TGI:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        var parts = cleaned.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            return (ParseHex(parts[0]), ParseHex(parts[1]), ParseHex(parts[2]));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Parses a hex string (optionally "0x"-prefixed); an empty string parses as 0. Shared by the TGI-editing and property-editing dialogs.</summary>
    public static uint ParseHex(string text)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        if (text.Length == 0)
        {
            text = "0";
        }

        return uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
