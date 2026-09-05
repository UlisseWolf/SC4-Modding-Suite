using System;
using System.IO;
using System.Text.Json;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Loads/saves the app's general options (external tool paths, protected-install paths,
/// language, theme) to a single JSON file under the same per-user data folder used by the
/// rest of the app's settings (e.g. <see cref="PropertySourceService"/>'s cache).
/// </summary>
public sealed class AppOptionsService
{
    private readonly string _path;

    public AppOptionsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "SC4ModdingSuite");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "options.json");
    }

    public AppOptions Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppOptions>(json) ?? new AppOptions();
            }
        }
        catch
        {
            // Fall through to defaults below.
        }

        return new AppOptions();
    }

    public void Save(AppOptions options)
    {
        try
        {
            var json = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Best-effort only; a failed write just means changes aren't persisted this run.
        }
    }
}
