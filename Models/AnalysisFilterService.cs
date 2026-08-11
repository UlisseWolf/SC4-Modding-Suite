using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SC4ModdingSuite.Models;

/// <summary>One saved Find/Index Analyser filter preset.</summary>
public sealed class SavedAnalysisFilter
{
    public string Name { get; set; } = string.Empty;
    public bool FilterType { get; set; }
    public string TypeHex { get; set; } = "0x00000000";
    public bool FilterGroup { get; set; }
    public string GroupHex { get; set; } = "0x00000000";
    public bool FilterInstance { get; set; }
    public string InstanceHex { get; set; } = "0x00000000";
    public string HexPattern { get; set; } = string.Empty;
}

/// <summary>
/// Saves/loads named Find/Index Analyser filter presets as a single JSON file - Ilive
/// Reader's "advanced saveable filters" (DlgFilters/DlgFiltersEx, surfaced via the
/// WorkspaceFilter pane). Same persistence shape as <see cref="AppOptionsService"/> (one
/// JSON file under the app's per-user data folder), just a list instead of a single object.
/// </summary>
public sealed class AnalysisFilterService
{
    private readonly string _path;

    public AnalysisFilterService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "SC4ModdingSuite");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "analysis-filters.json");
    }

    public List<SavedAnalysisFilter> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<List<SavedAnalysisFilter>>(json) ?? new List<SavedAnalysisFilter>();
            }
        }
        catch
        {
            // Fall through to empty list below.
        }

        return new List<SavedAnalysisFilter>();
    }

    public void Save(List<SavedAnalysisFilter> filters)
    {
        try
        {
            var json = JsonSerializer.Serialize(filters, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Best-effort only; a failed write just means the presets aren't persisted this run.
        }
    }
}
