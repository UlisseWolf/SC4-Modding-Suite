using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using csDBPF;
using SixLabors.ImageSharp;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.Mcp.Tools;

/// <summary>
/// The actual SC4 DBPF tools - registered by <see cref="Register"/>, one method per tool,
/// each opening the requested package fresh (this server is stateless between calls, unlike
/// the GUI app's own <see cref="DbpfService"/> which keeps one package "open" across many
/// user actions) via the very same <see cref="DbpfService"/>/codec classes that app uses.
///
/// Tool coverage vs. dbpf-mcp (https://github.com/caspervg/dbpf-mcp): this covers its own
/// "Read" tools for Exemplar/Cohort, LTEXT, and generic entry listing/summarizing, plus -
/// going beyond what dbpf-mcp itself has - dedicated readers for TRK, MCO, LDAT, HLS, AVP,
/// SC4Path, LEV, and legacy UI entries, all backed by this repository's own from-scratch
/// codecs (ported from Ilive Reader's real C++ source, not guessed - see each codec's own
/// doc comment). Not yet covered here: S3D geometry/FSH image export, index_plugins/
/// search_index (whole-Plugins-folder scanning), and the write_* tools - see this project's
/// own README.md for the reasoning and what a follow-up would need.
/// </summary>
internal static class DbpfTools
{
    private static readonly Dictionary<string, PropertyDefinitionsRegistry> RegistryCache = new();

    public static void Register(ToolRegistry registry)
    {
        registry.Register(new ToolDefinition(
            "list_entries",
            "Lists every entry in a DBPF package (.dat/.SC4Lot/.SC4Model/.SC4Desc) with its TGI, classified type, size, and compression state.",
            ToolHelpers.Schema(("path", "string", "Absolute path to the DBPF package file.", true)),
            ListEntries));

        registry.Register(new ToolDefinition(
            "summarize_package",
            "Overview of a DBPF package: total entry count, a breakdown by classified entry type, and a few notable/named entries.",
            ToolHelpers.Schema(("path", "string", "Absolute path to the DBPF package file.", true)),
            SummarizePackage));

        registry.Register(new ToolDefinition(
            "explain_entry",
            "Human-readable description of one entry (TGI, classified type, and - for Exemplar/Cohort - its decoded property list) - the same text this app's own Details panel shows.",
            ToolHelpers.Schema(
                ("path", "string", "Absolute path to the DBPF package file.", true),
                ("tgi", "string", "\"TTTTTTTT-GGGGGGGG-IIIIIIII\" (or use type/group/instance instead).", false),
                ("type", "string", "Type ID hex (used with group/instance instead of tgi).", false),
                ("group", "string", "Group ID hex.", false),
                ("instance", "string", "Instance ID hex.", false),
                ("propertyRegistryPath", "string", "Optional new_properties.xml path, for real property names instead of bare IDs.", false)),
            ExplainEntry));

        RegisterExemplarTool(registry, "read_exemplar", isCohort: false);
        RegisterExemplarTool(registry, "read_cohort", isCohort: true);

        registry.Register(new ToolDefinition(
            "read_ltext",
            "Decodes an LTEXT (localizable text) entry to its plain string value.",
            TgiSchema(),
            ReadLtext));

        registry.Register(new ToolDefinition(
            "read_trk",
            "Decodes a TRK (Track Definition, ENT_TRK/ENT_TRK2) entry - header bytes and the full positional field list, including the XA/TLO/HLS instance-ID cross-references at fields 2/3/14.",
            TgiSchema(),
            ReadTrk));

        registry.Register(new ToolDefinition(
            "read_mco",
            "Decodes an MCO (building menu/mayor's-camera preview) entry - its FSH texture reference and F1B animation-viewpoint references.",
            TgiSchema(),
            ReadMco));

        registry.Register(new ToolDefinition(
            "read_ld",
            "Decodes an LDAT (Lot Data) entry - lot dimensions/wealth/stage/type and its required-plugin list.",
            TgiSchema(),
            ReadLd));

        registry.Register(new ToolDefinition(
            "read_hls",
            "Decodes an HLS (Hitlist/playlist) entry - read-only, matching Ilive Reader's own _hls class (decode only, no encoder even there).",
            TgiSchema(),
            ReadHls));

        registry.Register(new ToolDefinition(
            "read_f1b",
            "Decodes an AVP/F1B (Animation Viewpoints) entry - read-only, matching Ilive Reader's own _f1b class (decode only, and its own header has fields the original author only ever named \"unknown\").",
            TgiSchema(),
            ReadF1b));

        registry.Register(new ToolDefinition(
            "read_sc4path",
            "Decodes an SC4Path (Network Path) entry - transit route tag/version/type, transport segments (with coordinates), and stops. Read-only, matching Ilive Reader's own _path class (decode only).",
            TgiSchema(),
            ReadSc4Path));

        registry.Register(new ToolDefinition(
            "read_lev",
            "Decodes a LEV (Level Table) entry - the header string and every #-delimited tab-separated table section as column/value data.",
            TgiSchema(),
            ReadLev));

        registry.Register(new ToolDefinition(
            "read_ui",
            "Decodes a legacy UI entry (TypeID 0x00000000) into its element tree - IID, id, caption, and every other prop per node, recursively.",
            TgiSchema(),
            ReadUi));

        registry.Register(new ToolDefinition(
            "read_s3d",
            "Reports S3D model metadata: vertex/material/mesh-group counts and material texture references. Does not export full geometry (matching dbpf-mcp's own documented S3D scope).",
            TgiSchema(),
            ReadS3D));

        registry.Register(new ToolDefinition(
            "export_fsh_png",
            "Decodes an FSH texture entry's first image and writes it to disk as a PNG file.",
            ToolHelpers.Schema(
                ("path", "string", "Absolute path to the DBPF package file.", true),
                ("tgi", "string", "\"TTTTTTTT-GGGGGGGG-IIIIIIII\" (or use type/group/instance instead).", false),
                ("type", "string", "Type ID hex (used with group/instance instead of tgi).", false),
                ("group", "string", "Group ID hex.", false),
                ("instance", "string", "Instance ID hex.", false),
                ("outputPath", "string", "Where to write the .png file.", true)),
            ExportFshPng));

        registry.Register(new ToolDefinition(
            "write_exemplars",
            "Creates a new (or merges into an existing) DBPF package with new Exemplar/Cohort entries and caller-specified properties. Property type is inferred from a supplied property registry, or declared explicitly (explicit always wins).",
            ToolHelpers.Schema(
                ("entries", "array", "Array of {tgi|type/group/instance, isCohort, properties: [{id, type?, values}]}.", true),
                ("outputPath", "string", "Where to write the package.", true),
                ("overwrite", "boolean", "Replace an existing file at outputPath entirely (default false).", false),
                ("merge", "boolean", "Keep existing entries at outputPath not addressed by this call, replacing/appending by TGI (default false).", false),
                ("compressed", "boolean", "QFS-compress new entries (default true).", false),
                ("propertyRegistryPath", "string", "Optional new_properties.xml path, for inferring property types from IDs.", false)),
            WriteExemplars));

        registry.Register(new ToolDefinition(
            "write_ltext",
            "Creates a new (or merges into an existing) DBPF package with new LTEXT (localizable text) entries.",
            ToolHelpers.Schema(
                ("entries", "array", "Array of {tgi|type/group/instance, text}.", true),
                ("outputPath", "string", "Where to write the package.", true),
                ("overwrite", "boolean", "Replace an existing file at outputPath entirely (default false).", false),
                ("merge", "boolean", "Keep existing entries at outputPath not addressed by this call, replacing/appending by TGI (default false).", false)),
            WriteLtext));

        registry.Register(new ToolDefinition(
            "write_raw_entries",
            "Writes arbitrary bytes to any TGI with no format decoding - for entry kinds without a dedicated encoder here (KEYCFG, TAB, RUL, EFFDIR, PNG, FSH, ...). Bytes are supplied as a base64 string.",
            ToolHelpers.Schema(
                ("entries", "array", "Array of {tgi|type/group/instance, base64Bytes}.", true),
                ("outputPath", "string", "Where to write the package.", true),
                ("overwrite", "boolean", "Replace an existing file at outputPath entirely (default false).", false),
                ("merge", "boolean", "Keep existing entries at outputPath not addressed by this call, replacing/appending by TGI (default false).", false),
                ("compressed", "boolean", "QFS-compress new entries (default true).", false)),
            WriteRawEntries));

        registry.Register(new ToolDefinition(
            "index_plugins",
            "Recursively scans a Plugins folder for DBPF packages (.dat/.SC4Lot/.SC4Model/.SC4Desc) and writes a persistent JSONL index of every Exemplar/Cohort entry's TGI, name, class, and property IDs, cached under ~/.cache/sc4-mcp-server/indexes/. Re-run after adding/removing files.",
            ToolHelpers.Schema(("pluginsPath", "string", "Absolute path to the Plugins folder to index.", true)),
            IndexPlugins));

        registry.Register(new ToolDefinition(
            "index_status",
            "Reports whether a Plugins folder already has a persistent index, when it was built, how many entries it covers, and whether it looks stale (a file changed since indexing).",
            ToolHelpers.Schema(("pluginsPath", "string", "Absolute path to the Plugins folder.", true)),
            IndexStatus));

        registry.Register(new ToolDefinition(
            "search_index",
            "Searches a previously built index (see index_plugins) by TGI substring, resource kind, exemplar name substring, object class, or property ID - without rescanning the folder.",
            ToolHelpers.Schema(
                ("pluginsPath", "string", "Absolute path to the indexed Plugins folder.", true),
                ("tgiContains", "string", "Substring match against \"TTTTTTTT-GGGGGGGG-IIIIIIII\".", false),
                ("nameContains", "string", "Case-insensitive substring match against the Exemplar's own name property.", false),
                ("hasPropertyId", "string", "Only entries that have this property ID (hex).", false),
                ("packagePathContains", "string", "Substring match against the containing package's own file path.", false),
                ("limit", "number", "Maximum results to return (default 100).", false)),
            SearchIndex));
    }

    private static JsonObject TgiSchema() => ToolHelpers.Schema(
        ("path", "string", "Absolute path to the DBPF package file.", true),
        ("tgi", "string", "\"TTTTTTTT-GGGGGGGG-IIIIIIII\" (or use type/group/instance instead).", false),
        ("type", "string", "Type ID hex (used with group/instance instead of tgi).", false),
        ("group", "string", "Group ID hex.", false),
        ("instance", "string", "Instance ID hex.", false));

    // ---------------------------------------------------------------
    // Shared open/find plumbing
    // ---------------------------------------------------------------

    private static DbpfService OpenPackage(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No such file: {path}");
        }

        var service = new DbpfService();
        service.Open(path);
        return service;
    }

    private static DBPFEntry FindEntry(DbpfService service, TGI tgi)
    {
        var entry = service.TryGetEntry(tgi);
        if (entry is null)
        {
            throw new InvalidOperationException($"No entry with TGI {ToolHelpers.TgiText(tgi)} in this package.");
        }

        return entry;
    }

    private static PropertyDefinitionsRegistry? LoadRegistry(JsonNode? args)
    {
        var path = args?["propertyRegistryPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (RegistryCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var registry = new PropertyDefinitionsRegistry();
        registry.Load(path, "MCP-supplied");
        RegistryCache[path] = registry;
        return registry;
    }

    // ---------------------------------------------------------------
    // list_entries / summarize_package / explain_entry
    // ---------------------------------------------------------------

    private static JsonNode ListEntries(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var service = OpenPackage(path);

        var array = new JsonArray();
        foreach (var entry in service.Entries)
        {
            array.Add(EntrySummary(entry));
        }

        return new JsonObject
        {
            ["path"] = path,
            ["entryCount"] = service.Entries.Count,
            ["entries"] = array,
        };
    }

    private static JsonObject EntrySummary(DBPFEntry entry) => new()
    {
        ["tgi"] = ToolHelpers.TgiText(entry.TGI),
        ["classifiedType"] = KnownFormats.TryGetName(entry.TGI.TypeID) ?? entry.TGI.GetEntryType().ToString(),
        ["sizeBytes"] = entry.ByteData?.Length ?? 0,
        ["compressed"] = entry.IsCompressed,
    };

    private static JsonNode SummarizePackage(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var service = OpenPackage(path);

        var byType = new JsonObject();
        var counts = new Dictionary<string, int>();
        foreach (var entry in service.Entries)
        {
            var label = KnownFormats.TryGetName(entry.TGI.TypeID) ?? entry.TGI.GetEntryType().ToString();
            counts[label] = counts.GetValueOrDefault(label) + 1;
        }

        foreach (var (label, count) in counts.OrderByDescending(kv => kv.Value))
        {
            byType[label] = count;
        }

        var notable = new JsonArray();
        foreach (var entry in service.Entries)
        {
            if (entry is DBPFEntryEXMP exmp)
            {
                entry.Decode();
                var parsed = ExemplarBinaryParser.Parse(RawEntryBytes.GetDecompressed(entry));
                var nameProp = parsed.Properties.FirstOrDefault(p => p.Id == 0x20 || p.Id == 0x00002026); // ExemplarName / EXT_NAME-ish common IDs
                notable.Add(new JsonObject
                {
                    ["tgi"] = ToolHelpers.TgiText(entry.TGI),
                    ["kind"] = exmp.IsCohort ? "Cohort" : "Exemplar",
                    ["propertyCount"] = parsed.Properties.Count,
                    ["note"] = nameProp is not null ? string.Join(", ", nameProp.Values) : null,
                });

                if (notable.Count >= 20)
                {
                    break;
                }
            }
        }

        return new JsonObject
        {
            ["path"] = path,
            ["entryCount"] = service.Entries.Count,
            ["byType"] = byType,
            ["notableExemplars"] = notable,
        };
    }

    private static JsonNode ExplainEntry(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var service = OpenPackage(path);
        var entry = FindEntry(service, tgi);
        var registry = LoadRegistry(args);

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(entry.TGI),
            ["description"] = EntryDescriber.Describe(entry, registry),
        };
    }

    // ---------------------------------------------------------------
    // Exemplar / Cohort
    // ---------------------------------------------------------------

    private static void RegisterExemplarTool(ToolRegistry registry, string name, bool isCohort)
    {
        registry.Register(new ToolDefinition(
            name,
            $"Decodes an {(isCohort ? "Cohort" : "Exemplar")} entry to JSON: every property's ID, name (if a property registry is supplied), data type, and values.",
            ToolHelpers.Schema(
                ("path", "string", "Absolute path to the DBPF package file.", true),
                ("tgi", "string", "\"TTTTTTTT-GGGGGGGG-IIIIIIII\" (or use type/group/instance instead).", false),
                ("type", "string", "Type ID hex (used with group/instance instead of tgi).", false),
                ("group", "string", "Group ID hex.", false),
                ("instance", "string", "Instance ID hex.", false),
                ("propertyRegistryPath", "string", "Optional new_properties.xml path, for real property names instead of bare IDs.", false)),
            args => ReadExemplar(args)));
    }

    private static JsonNode ReadExemplar(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var service = OpenPackage(path);
        var entry = FindEntry(service, tgi);

        if (entry is not DBPFEntryEXMP exmp)
        {
            throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} is not an Exemplar/Cohort (Type ID 0x{tgi.TypeID:X8}).");
        }

        var registry = LoadRegistry(args);
        var bytes = RawEntryBytes.GetDecompressed(entry);
        var parsed = ExemplarBinaryParser.Parse(bytes);

        var properties = new JsonArray();
        foreach (var property in parsed.Properties.OrderBy(p => p.Id))
        {
            var definition = registry?.FindById(property.Id);
            properties.Add(new JsonObject
            {
                ["id"] = $"0x{property.Id:X8}",
                ["name"] = definition?.Name,
                ["dataType"] = property.DataType.ToString(),
                ["values"] = ValuesToJson(property.Values),
            });
        }

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(entry.TGI),
            ["isCohort"] = exmp.IsCohort,
            ["wellFormed"] = parsed.IsWellFormed,
            ["parseError"] = parsed.Error,
            ["propertyCount"] = parsed.Properties.Count,
            ["properties"] = properties,
        };
    }

    private static JsonArray ValuesToJson(object[] values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(ValueToJson(value));
        }

        return array;
    }

    private static JsonNode? ValueToJson(object value) => value switch
    {
        byte b => JsonValue.Create(b),
        sbyte sb => JsonValue.Create(sb),
        ushort us => JsonValue.Create(us),
        short s => JsonValue.Create(s),
        uint ui => JsonValue.Create(ui),
        int i => JsonValue.Create(i),
        ulong ul => JsonValue.Create(ul),
        long l => JsonValue.Create(l),
        float f => JsonValue.Create(f),
        double d => JsonValue.Create(d),
        bool bo => JsonValue.Create(bo),
        string str => JsonValue.Create(str),
        null => null,
        _ => JsonValue.Create(value.ToString()),
    };

    // ---------------------------------------------------------------
    // LTEXT
    // ---------------------------------------------------------------

    private static JsonNode ReadLtext(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var service = OpenPackage(path);
        var entry = FindEntry(service, tgi);

        string? text;
        if (entry is DBPFEntryLTEXT ltext)
        {
            entry.Decode();
            text = ltext.Text;
        }
        else
        {
            var bytes = RawEntryBytes.GetDecompressed(entry);
            text = EntryTypeClassifier.LooksLikeRiffWav(bytes) ? null : EntryTypeClassifier.TryDecodeAsLtext(bytes);
        }

        if (text is null)
        {
            throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} doesn't decode as LTEXT (it may be WAV/XA audio, which shares this Type ID).");
        }

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(entry.TGI),
            ["text"] = text,
        };
    }

    // ---------------------------------------------------------------
    // TRK / MCO / LDAT / HLS / F1B / SC4Path / LEV
    // ---------------------------------------------------------------

    private static byte[] RequireBytes(DbpfService service, TGI tgi)
    {
        var entry = FindEntry(service, tgi);
        return RawEntryBytes.GetDecompressed(entry) ?? throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} has no data.");
    }

    private static JsonNode ReadTrk(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var bytes = RequireBytes(OpenPackage(path), tgi);

        if (!TkdCodec.TryDecode(bytes, out var data))
        {
            throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} doesn't decode as TRK (missing \"ETKD\" footer).");
        }

        var fields = new JsonArray();
        for (var i = 0; i < data.Fields.Count; i++)
        {
            fields.Add(new JsonObject { ["index"] = i, ["value"] = data.Fields[i] });
        }

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(tgi),
            ["headerHex"] = Convert.ToHexString(data.Header, 0, data.HeaderSize),
            ["fields"] = fields,
            ["xaInstance"] = data.Fields.Count > 2 ? data.Fields[2] : null,
            ["tloInstance"] = data.Fields.Count > 3 ? data.Fields[3] : null,
            ["hlsInstance"] = data.Fields.Count > 14 ? data.Fields[14] : null,
        };
    }

    private static JsonNode ReadMco(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var bytes = RequireBytes(OpenPackage(path), tgi);

        if (!McoCodec.TryDecode(bytes, out var data))
        {
            throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} is too short to be MCO (needs {McoCodec.Size} bytes).");
        }

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(tgi),
            ["mcoType"] = data.McoType,
            ["fshTgi"] = $"{data.FshType:X8}-{data.FshGroup:X8}-{data.FshInstance:X8}",
            ["f1bType"] = $"0x{data.F1BType:X8}",
            ["f1bGroup"] = $"0x{data.F1BGroup:X8}",
            ["f1bInstanceZoom1"] = $"0x{data.F1BInstanceZoom1:X8}",
            ["f1bInstanceZoom2"] = $"0x{data.F1BInstanceZoom2:X8}",
            ["f1bInstanceZoom3"] = $"0x{data.F1BInstanceZoom3:X8}",
            ["f1bInstanceZoom4"] = $"0x{data.F1BInstanceZoom4:X8}",
            ["f1bInstanceZoom5"] = $"0x{data.F1BInstanceZoom5:X8}",
            ["frameCount"] = data.FrameCount,
        };
    }

    private static JsonNode ReadLd(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var bytes = RequireBytes(OpenPackage(path), tgi);

        if (!LdCodec.TryDecode(bytes, out var data))
        {
            throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} doesn't decode as LDAT.");
        }

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(tgi),
            ["version"] = data.Version,
            ["compatibility"] = data.Compatibility,
            ["wealth"] = data.Wealth,
            ["subtype"] = data.Subtype,
            ["type"] = data.Type,
            ["stageLot"] = data.StageLot,
            ["newLot"] = data.NewLot,
            ["lotWidth"] = data.LotWidth,
            ["lotDepth"] = data.LotDepth,
            ["numberProp"] = data.NumberProp,
            ["lotName"] = data.LotName,
            ["requiredPlugins"] = new JsonArray(data.Plugins.Select(p => (JsonNode)JsonValue.Create(p)!).ToArray()),
        };
    }

    private static JsonNode ReadHls(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var bytes = RequireBytes(OpenPackage(path), tgi);

        if (!HlsCodec.TryDecode(bytes, out var data))
        {
            throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} doesn't decode as HLS.");
        }

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(tgi),
            ["header"] = $"0x{data.Header:X8}",
            ["instances"] = new JsonArray(data.Instances.Select(i => (JsonNode)JsonValue.Create($"0x{i:X8}")!).ToArray()),
        };
    }

    private static JsonNode ReadF1b(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var bytes = RequireBytes(OpenPackage(path), tgi);

        if (!F1bCodec.TryDecode(bytes, out var data))
        {
            throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} is too short to be AVP/F1B.");
        }

        var frames = new JsonArray();
        foreach (var frame in data.Frames)
        {
            frames.Add(new JsonObject
            {
                ["plane"] = frame.Plane,
                ["unknown1"] = frame.Unknown1,
                ["filePosition"] = frame.FilePosition,
                ["width"] = frame.Width,
                ["height"] = frame.Height,
                ["pos1"] = frame.Pos1,
                ["pos2"] = frame.Pos2,
            });
        }

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(tgi),
            ["f1bType"] = data.F1BType,
            ["entries"] = data.Entries,
            ["unknown1"] = data.Unknown1,
            ["unknown2"] = data.Unknown2,
            ["unknown3Hex"] = Convert.ToHexString(data.Unknown3),
            ["frames"] = frames,
        };
    }

    private static JsonNode ReadSc4Path(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var bytes = RequireBytes(OpenPackage(path), tgi);

        if (!Sc4PathCodec.TryDecode(bytes, out var data))
        {
            throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} doesn't decode as SC4Path.");
        }

        JsonObject CoordToJson(Sc4PathCodec.Coord c) => new() { ["x"] = c.X, ["y"] = c.Y, ["z"] = c.Z };

        var transports = new JsonArray();
        foreach (var t in data.Transports)
        {
            transports.Add(new JsonObject
            {
                ["name"] = t.Name,
                ["transportationType"] = t.TransportationType,
                ["elevationType"] = t.ElevationType,
                ["val1"] = t.Val1,
                ["val2"] = t.Val2,
                ["coords"] = new JsonArray(t.Coords.Select(c => (JsonNode)CoordToJson(c)).ToArray()),
            });
        }

        var stops = new JsonArray();
        foreach (var s in data.Stops)
        {
            stops.Add(new JsonObject
            {
                ["name"] = s.Name,
                ["stopType"] = s.StopType,
                ["transitType"] = s.TransitType,
                ["class"] = s.Class,
                ["val1"] = s.Val1,
                ["val2"] = s.Val2,
                ["coord"] = s.Coord is { } coord ? CoordToJson(coord) : null,
            });
        }

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(tgi),
            ["tag"] = data.Tag,
            ["version"] = data.Version,
            ["transportPathCount"] = data.TransportPathCount,
            ["simPath"] = data.SimPath,
            ["pathType"] = data.PathType,
            ["transports"] = transports,
            ["stops"] = stops,
        };
    }

    private static JsonNode ReadLev(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var bytes = RequireBytes(OpenPackage(path), tgi);

        if (!LevCodec.LooksLikeLev(bytes) || !LevCodec.TryDecode(bytes, out var data))
        {
            throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} doesn't decode as LEV (no '#' section marker, or not table-shaped text).");
        }

        var sections = new JsonArray();
        foreach (var section in data.Sections)
        {
            var columns = new JsonArray();
            foreach (var column in section.Columns)
            {
                columns.Add(new JsonObject
                {
                    ["title"] = column.Title,
                    ["values"] = new JsonArray(column.Values.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray()),
                });
            }

            sections.Add(new JsonObject { ["columns"] = columns });
        }

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(tgi),
            ["header"] = data.Header,
            ["sections"] = sections,
        };
    }

    // ---------------------------------------------------------------
    // Legacy UI
    // ---------------------------------------------------------------

    private static JsonNode ReadUi(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var bytes = RequireBytes(OpenPackage(path), tgi);

        var text = System.Text.Encoding.UTF8.GetString(bytes);
        var root = UiLegacyParser.Parse(text);

        var children = new JsonArray();
        foreach (var child in root.Children)
        {
            children.Add(UiNodeToJson(child));
        }

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(tgi),
            ["topLevelElements"] = children,
        };
    }

    private static JsonObject UiNodeToJson(UiLegacyNode node)
    {
        var props = new JsonObject();
        foreach (var prop in node.Properties)
        {
            props[prop.Key] = prop.Value;
        }

        var children = new JsonArray();
        foreach (var child in node.Children)
        {
            children.Add(UiNodeToJson(child));
        }

        return new JsonObject
        {
            ["iid"] = node.GetProp("iid"),
            ["properties"] = props,
            ["children"] = children,
        };
    }

    // ---------------------------------------------------------------
    // S3D (metadata only - matching dbpf-mcp's own documented scope: "does not export full
    // geometry, and there is no write_s3d")
    // ---------------------------------------------------------------

    private static JsonNode ReadS3D(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var bytes = RequireBytes(OpenPackage(path), tgi);

        var model = S3DParser.Parse(bytes) ?? throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} doesn't decode as S3D.");

        var materials = new JsonArray();
        foreach (var material in model.Materials)
        {
            var textures = new JsonArray();
            foreach (var texture in material.Textures)
            {
                textures.Add(new JsonObject
                {
                    ["textureId"] = $"0x{texture.TextureId:X8}",
                    ["name"] = texture.Name,
                });
            }

            materials.Add(new JsonObject
            {
                ["materialClass"] = $"0x{material.MaterialClass:X8}",
                ["textures"] = textures,
            });
        }

        var meshes = new JsonArray();
        foreach (var mesh in model.Animation.Meshes)
        {
            meshes.Add(new JsonObject { ["name"] = mesh.Name, ["frameCount"] = mesh.Frames.Count });
        }

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(tgi),
            ["version"] = $"{model.MajorRevision}.{model.MinorRevision}",
            ["vertexBlockCount"] = model.VertexBlocks.Count,
            ["totalVertexCount"] = model.TotalVertexCount,
            ["indexBlockCount"] = model.IndexBlocks.Count,
            ["primBlockCount"] = model.PrimBlocks.Count,
            ["materialCount"] = model.MaterialCount,
            ["materials"] = materials,
            ["hasAnimation"] = model.HasAnimation,
            ["animationMeshes"] = meshes,
        };
    }

    // ---------------------------------------------------------------
    // FSH export (decode + PNG encode only - see write_fsh's own absence, noted in README)
    // ---------------------------------------------------------------

    private static JsonNode ExportFshPng(JsonNode? args)
    {
        var path = ToolHelpers.RequirePath(args);
        var tgi = ToolHelpers.RequireTgi(args);
        var outputPath = args?["outputPath"]?.GetValue<string>() ?? throw new ArgumentException("Missing required \"outputPath\" argument.");

        var service = OpenPackage(path);
        var entry = FindEntry(service, tgi);
        if (entry is not DBPFEntryFSH fsh)
        {
            throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} is not an FSH texture.");
        }

        entry.Decode();
        var image = fsh.Image ?? throw new InvalidOperationException($"Entry {ToolHelpers.TgiText(tgi)} decoded but has no image data.");

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        image.SaveAsPng(outputPath);

        return new JsonObject
        {
            ["tgi"] = ToolHelpers.TgiText(tgi),
            ["outputPath"] = outputPath,
            ["width"] = image.Width,
            ["height"] = image.Height,
        };
    }

    // ---------------------------------------------------------------
    // Write tools - see PrepareOutputPackage's own doc comment for the shared
    // create/merge/overwrite semantics every one of these follows.
    // ---------------------------------------------------------------

    /// <summary>
    /// Opens or creates the package every write_* tool saves into, matching dbpf-mcp's own
    /// documented outputPath/overwrite/merge semantics exactly:
    /// <list type="bullet">
    /// <item><description>No file at <paramref name="outputPath"/> yet - starts a fresh, empty package regardless of <paramref name="overwrite"/>/<paramref name="merge"/>.</description></item>
    /// <item><description><paramref name="merge"/> true - opens the existing file, so already-present entries this call doesn't touch survive; entries this call does address (same TGI) get replaced.</description></item>
    /// <item><description><paramref name="overwrite"/> true (and merge false) - starts a fresh, empty package, discarding whatever was there.</description></item>
    /// <item><description>Neither - refuses, rather than silently clobbering a file the caller may not have meant to replace.</description></item>
    /// </list>
    /// </summary>
    private static DbpfService PrepareOutputPackage(string outputPath, bool overwrite, bool merge)
    {
        var service = new DbpfService();

        if (File.Exists(outputPath))
        {
            if (merge)
            {
                service.Open(outputPath);
            }
            else if (overwrite)
            {
                service.CreateNew();
            }
            else
            {
                throw new InvalidOperationException(
                    $"{outputPath} already exists - pass \"overwrite\": true to replace it entirely, or \"merge\": true to keep its other entries.");
            }
        }
        else
        {
            service.CreateNew();
        }

        return service;
    }

    private static void AddEntryToPackage(DbpfService service, DBPFEntry entry) =>
        DbpfFileFixes.AddOrUpdateEntry(service.CurrentFile!, entry);

    private static JsonArray RequireEntriesArray(JsonNode? args)
    {
        if (args?["entries"] is not JsonArray array || array.Count == 0)
        {
            throw new ArgumentException("Missing or empty required \"entries\" array.");
        }

        return array;
    }

    private static string RequireOutputPath(JsonNode? args) =>
        args?["outputPath"]?.GetValue<string>() ?? throw new ArgumentException("Missing required \"outputPath\" argument.");

    private static void CheckNoDuplicateTgi(HashSet<TGI> seen, TGI tgi)
    {
        if (!seen.Add(tgi))
        {
            throw new InvalidOperationException($"Duplicate TGI {ToolHelpers.TgiText(tgi)} within this request - each entries[] item must have a distinct TGI.");
        }
    }

    private static JsonNode WriteExemplars(JsonNode? args)
    {
        var entriesArg = RequireEntriesArray(args);
        var outputPath = RequireOutputPath(args);
        var overwrite = args?["overwrite"]?.GetValue<bool>() ?? false;
        var merge = args?["merge"]?.GetValue<bool>() ?? false;
        var compressed = args?["compressed"]?.GetValue<bool>() ?? true;
        var registry = LoadRegistry(args);

        var service = PrepareOutputPackage(outputPath, overwrite, merge);
        var seen = new HashSet<TGI>();
        var warnings = new JsonArray();
        var written = new JsonArray();

        foreach (var entryNode in entriesArg)
        {
            var tgi = ToolHelpers.RequireTgi(entryNode);
            CheckNoDuplicateTgi(seen, tgi);

            var exemplar = new DBPFEntryEXMP(tgi) { IsCohort = entryNode?["isCohort"]?.GetValue<bool>() ?? false };

            if (entryNode?["properties"] is JsonArray properties)
            {
                foreach (var propNode in properties)
                {
                    var id = ToolHelpers.ParseHex(propNode?["id"]?.GetValue<string>() ?? throw new ArgumentException("Each property needs an \"id\"."), "id");
                    var explicitType = propNode?["type"]?.GetValue<string>();
                    var valuesNode = propNode?["values"] as JsonArray ?? throw new ArgumentException($"Property 0x{id:X8} needs a \"values\" array.");

                    var (dataType, inferred) = ResolveDataType(id, explicitType, registry);
                    if (inferred)
                    {
                        warnings.Add($"Property 0x{id:X8}: type inferred as {dataType} from the property registry (no explicit \"type\" given).");
                    }

                    var property = BuildProperty(id, dataType, valuesNode);
                    exemplar.AddOrUpdateProperty(property);
                }
            }

            ExemplarEncodeFix.EnsureEncodable(exemplar);
            ExemplarEncodeWorkaround.Encode(exemplar, compressed);
            AddEntryToPackage(service, exemplar);
            written.Add(ToolHelpers.TgiText(tgi));
        }

        service.SaveAs(outputPath);

        return new JsonObject
        {
            ["outputPath"] = outputPath,
            ["entriesWritten"] = written,
            ["warnings"] = warnings,
        };
    }

    private static (DBPFProperty.PropertyDataType DataType, bool Inferred) ResolveDataType(uint id, string? explicitType, PropertyDefinitionsRegistry? registry)
    {
        if (explicitType is not null)
        {
            return (Enum.Parse<DBPFProperty.PropertyDataType>(explicitType, ignoreCase: true), false);
        }

        var fromRegistry = registry?.FindById(id)?.DataType;
        if (fromRegistry is { } dt && dt != DBPFProperty.PropertyDataType.UNKNOWN)
        {
            return (dt, true);
        }

        throw new ArgumentException(
            $"Property 0x{id:X8}: no explicit \"type\" given and no property registry entry found to infer one from - pass \"type\" explicitly (Uint8/Uint16/Uint32/Sint32/Sint64/Float32/Bool/String) or \"propertyRegistryPath\".");
    }

    /// <summary>Mirrors the GUI app's own proven PropertyEditDialogViewModel.BuildProperty exactly (same concrete DBPFProperty subtypes, same DBPF.Encoding.Binary), just taking a JSON values array instead of one comma-separated string.</summary>
    private static DBPFProperty BuildProperty(uint id, DBPFProperty.PropertyDataType dataType, JsonArray valuesNode)
    {
        DBPFProperty property = dataType switch
        {
            DBPFProperty.PropertyDataType.FLOAT32 => new DBPFPropertyFloat(
                valuesNode.Select(v => v!.GetValue<float>()).ToArray(), DBPF.Encoding.Binary),
            DBPFProperty.PropertyDataType.STRING => new DBPFPropertyString(
                valuesNode.Count > 0 ? valuesNode[0]!.GetValue<string>() : string.Empty, DBPF.Encoding.Binary),
            _ => new DBPFPropertyLong(
                dataType,
                valuesNode.Select(v => dataType == DBPFProperty.PropertyDataType.BOOL
                    ? (v!.GetValue<bool>() ? 1L : 0L)
                    : v!.GetValue<long>()).ToArray(),
                DBPF.Encoding.Binary),
        };

        property.ID = id;
        return property;
    }

    private static JsonNode WriteLtext(JsonNode? args)
    {
        var entriesArg = RequireEntriesArray(args);
        var outputPath = RequireOutputPath(args);
        var overwrite = args?["overwrite"]?.GetValue<bool>() ?? false;
        var merge = args?["merge"]?.GetValue<bool>() ?? false;

        var service = PrepareOutputPackage(outputPath, overwrite, merge);
        var seen = new HashSet<TGI>();
        var written = new JsonArray();

        foreach (var entryNode in entriesArg)
        {
            var tgi = ToolHelpers.RequireTgi(entryNode);
            CheckNoDuplicateTgi(seen, tgi);
            var text = entryNode?["text"]?.GetValue<string>() ?? throw new ArgumentException($"Entry {ToolHelpers.TgiText(tgi)} needs a \"text\" field.");

            service.UpsertLtextEntry(tgi, text);
            written.Add(ToolHelpers.TgiText(tgi));
        }

        service.SaveAs(outputPath);

        return new JsonObject
        {
            ["outputPath"] = outputPath,
            ["entriesWritten"] = written,
        };
    }

    private static JsonNode WriteRawEntries(JsonNode? args)
    {
        var entriesArg = RequireEntriesArray(args);
        var outputPath = RequireOutputPath(args);
        var overwrite = args?["overwrite"]?.GetValue<bool>() ?? false;
        var merge = args?["merge"]?.GetValue<bool>() ?? false;
        var compressed = args?["compressed"]?.GetValue<bool>() ?? true;

        var service = PrepareOutputPackage(outputPath, overwrite, merge);
        var seen = new HashSet<TGI>();
        var written = new JsonArray();

        foreach (var entryNode in entriesArg)
        {
            var tgi = ToolHelpers.RequireTgi(entryNode);
            CheckNoDuplicateTgi(seen, tgi);
            var base64 = entryNode?["base64Bytes"]?.GetValue<string>() ?? throw new ArgumentException($"Entry {ToolHelpers.TgiText(tgi)} needs a \"base64Bytes\" field.");

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException($"Entry {ToolHelpers.TgiText(tgi)}: \"base64Bytes\" isn't valid base64 ({ex.Message}).");
            }

            service.AddNewEntry(tgi, bytes, compressed);
            written.Add(ToolHelpers.TgiText(tgi));
        }

        service.SaveAs(outputPath);

        return new JsonObject
        {
            ["outputPath"] = outputPath,
            ["entriesWritten"] = written,
        };
    }

    // ---------------------------------------------------------------
    // index_plugins / index_status / search_index
    // ---------------------------------------------------------------

    private static readonly string[] IndexableExtensions = { ".dat", ".sc4lot", ".sc4model", ".sc4desc" };

    private static string GetIndexCacheDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "sc4-mcp-server", "indexes");

    /// <summary>Same idea as dbpf-mcp's own index cache naming - one file per distinct Plugins path, named from a stable hash of the full path so it survives re-runs and doesn't collide across different folders.</summary>
    private static string GetIndexFilePath(string pluginsPath)
    {
        var normalized = Path.GetFullPath(pluginsPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized)))[..16];
        return Path.Combine(GetIndexCacheDir(), $"{hash}.jsonl");
    }

    private static JsonNode IndexPlugins(JsonNode? args)
    {
        var pluginsPath = args?["pluginsPath"]?.GetValue<string>() ?? throw new ArgumentException("Missing required \"pluginsPath\" argument.");
        if (!Directory.Exists(pluginsPath))
        {
            throw new DirectoryNotFoundException($"No such folder: {pluginsPath}");
        }

        var files = Directory.EnumerateFiles(pluginsPath, "*", SearchOption.AllDirectories)
            .Where(f => IndexableExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        var indexPath = GetIndexFilePath(pluginsPath);
        Directory.CreateDirectory(GetIndexCacheDir());

        var entryCount = 0;
        var packageCount = 0;
        var errorCount = 0;

        using (var writer = new StreamWriter(indexPath, append: false))
        {
            foreach (var filePath in files)
            {
                DbpfService fileService;
                try
                {
                    fileService = OpenPackage(filePath);
                }
                catch (Exception ex)
                {
                    McpHttpServer.Log($"index_plugins: skipping unreadable file {filePath}: {ex.Message}");
                    errorCount++;
                    continue;
                }

                packageCount++;
                foreach (var entry in fileService.Entries)
                {
                    var record = new JsonObject
                    {
                        ["packagePath"] = filePath,
                        ["tgi"] = ToolHelpers.TgiText(entry.TGI),
                        ["classifiedType"] = KnownFormats.TryGetName(entry.TGI.TypeID) ?? entry.TGI.GetEntryType().ToString(),
                    };

                    if (entry is DBPFEntryEXMP exmp)
                    {
                        try
                        {
                            entry.Decode();
                            var parsed = ExemplarBinaryParser.Parse(RawEntryBytes.GetDecompressed(entry));
                            var nameProp = parsed.Properties.FirstOrDefault(p => p.Id == 0x20 || p.Id == 0x00002026);
                            record["isCohort"] = exmp.IsCohort;
                            record["name"] = nameProp is not null ? string.Join(", ", nameProp.Values) : null;
                            record["propertyIds"] = new JsonArray(parsed.Properties.Select(p => (JsonNode)JsonValue.Create($"0x{p.Id:X8}")!).ToArray());
                        }
                        catch (Exception ex)
                        {
                            McpHttpServer.Log($"index_plugins: couldn't decode Exemplar {ToolHelpers.TgiText(entry.TGI)} in {filePath}: {ex.Message}");
                        }
                    }

                    writer.WriteLine(record.ToJsonString());
                    entryCount++;
                }
            }
        }

        return new JsonObject
        {
            ["pluginsPath"] = Path.GetFullPath(pluginsPath),
            ["indexFile"] = indexPath,
            ["packagesIndexed"] = packageCount,
            ["packagesSkipped"] = errorCount,
            ["entriesIndexed"] = entryCount,
            ["indexedAt"] = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private static JsonNode IndexStatus(JsonNode? args)
    {
        var pluginsPath = args?["pluginsPath"]?.GetValue<string>() ?? throw new ArgumentException("Missing required \"pluginsPath\" argument.");
        var indexPath = GetIndexFilePath(pluginsPath);

        if (!File.Exists(indexPath))
        {
            return new JsonObject
            {
                ["pluginsPath"] = Path.GetFullPath(pluginsPath),
                ["indexed"] = false,
                ["message"] = "No index yet - run index_plugins first.",
            };
        }

        var indexWriteTime = File.GetLastWriteTimeUtc(indexPath);
        var lineCount = File.ReadLines(indexPath).Count();

        var stale = false;
        if (Directory.Exists(pluginsPath))
        {
            stale = Directory.EnumerateFiles(pluginsPath, "*", SearchOption.AllDirectories)
                .Where(f => IndexableExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Any(f => File.GetLastWriteTimeUtc(f) > indexWriteTime);
        }

        return new JsonObject
        {
            ["pluginsPath"] = Path.GetFullPath(pluginsPath),
            ["indexed"] = true,
            ["indexFile"] = indexPath,
            ["indexedAt"] = indexWriteTime.ToString("O"),
            ["entryCount"] = lineCount,
            ["stale"] = stale,
        };
    }

    private static JsonNode SearchIndex(JsonNode? args)
    {
        var pluginsPath = args?["pluginsPath"]?.GetValue<string>() ?? throw new ArgumentException("Missing required \"pluginsPath\" argument.");
        var indexPath = GetIndexFilePath(pluginsPath);
        if (!File.Exists(indexPath))
        {
            throw new InvalidOperationException($"No index for {pluginsPath} yet - run index_plugins first.");
        }

        var tgiContains = args?["tgiContains"]?.GetValue<string>();
        var nameContains = args?["nameContains"]?.GetValue<string>();
        var hasPropertyId = args?["hasPropertyId"]?.GetValue<string>();
        var hasPropertyIdNormalized = hasPropertyId is not null ? $"0x{ToolHelpers.ParseHex(hasPropertyId, "hasPropertyId"):X8}" : null;
        var packagePathContains = args?["packagePathContains"]?.GetValue<string>();
        var limit = (int?)args?["limit"]?.GetValue<double>() ?? 100;

        var results = new JsonArray();
        foreach (var line in File.ReadLines(indexPath))
        {
            if (results.Count >= limit)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var record = JsonNode.Parse(line);
            if (record is null)
            {
                continue;
            }

            if (tgiContains is not null && !(record["tgi"]?.GetValue<string>() ?? "").Contains(tgiContains, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (nameContains is not null && !(record["name"]?.GetValue<string>() ?? "").Contains(nameContains, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (packagePathContains is not null && !(record["packagePath"]?.GetValue<string>() ?? "").Contains(packagePathContains, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (hasPropertyIdNormalized is not null)
            {
                var propertyIds = record["propertyIds"] as JsonArray;
                if (propertyIds is null || !propertyIds.Any(p => string.Equals(p?.GetValue<string>(), hasPropertyIdNormalized, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            results.Add(record);
        }

        return new JsonObject
        {
            ["pluginsPath"] = Path.GetFullPath(pluginsPath),
            ["resultCount"] = results.Count,
            ["results"] = results,
        };
    }
}
