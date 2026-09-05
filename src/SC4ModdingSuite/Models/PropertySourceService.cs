using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SC4ModdingSuite.Models;

/// <summary>The two community-maintained new_properties.xml repositories this app supports.</summary>
public enum PropertySource
{
    /// <summary>https://github.com/NAMTeam/New_Properties.xml - vanilla/BSC-oriented.</summary>
    NamTeam,

    /// <summary>https://github.com/UlisseWolf/New_Properties.xml-patches - CAM-oriented, updates more frequently.</summary>
    UlisseWolfPatches,
}

/// <summary>Cached per-source metadata used to make update checks cheap (HTTP ETag).</summary>
public sealed class PropertySourceMeta
{
    public string? ETag { get; set; }
    public DateTimeOffset? LastChecked { get; set; }
    public DateTimeOffset? LastDownloaded { get; set; }
}

/// <summary>
/// Downloads, caches, and locally overrides new_properties.xml from the two community
/// repositories that publish it:
///
/// <list type="bullet">
/// <item>NAM Team (BSC/vanilla-oriented): https://github.com/NAMTeam/New_Properties.xml</item>
/// <item>UlisseWolf (CAM-oriented patches, updates faster): https://github.com/UlisseWolf/New_Properties.xml-patches</item>
/// </list>
///
/// Both publish a plain <c>new_properties.xml</c> at the root of their <c>staging</c> branch
/// (confirmed by inspecting both repositories directly - neither currently ships it as a
/// GitHub Release asset), so files are fetched via raw.githubusercontent.com with an HTTP
/// conditional request (If-None-Match/ETag) to cheaply detect "no update available" without
/// re-downloading unchanged content every time.
///
/// <para>Local storage layout, under <c>%APPDATA%\SC4ModdingSuite\PropertyDefinitions\</c>:</para>
/// <code>
/// NAMTeam\new_properties.xml     - cached download from NAM Team
/// NAMTeam\meta.json              - ETag/timestamps for the above
/// UlisseWolf\new_properties.xml  - cached download from UlisseWolf's patched version
/// UlisseWolf\meta.json           - ETag/timestamps for the above
/// Local\new_properties.xml       - optional hand-placed override for New_Properties.xml
///                                   developers: if present, it ALWAYS wins over both
///                                   downloaded copies above (no network access needed) so
///                                   someone actively editing the file can just drop their
///                                   working copy here and reopen the app to test it.
/// </code>
/// </summary>
public sealed class PropertySourceService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    private const string NamTeamUrl =
        "https://raw.githubusercontent.com/NAMTeam/New_Properties.xml/staging/new_properties.xml";
    private const string UlisseWolfUrl =
        "https://raw.githubusercontent.com/UlisseWolf/New_Properties.xml-patches/staging/new_properties.xml";

    public string RootFolder { get; }

    public PropertySourceService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        RootFolder = Path.Combine(appData, "SC4ModdingSuite", "PropertyDefinitions");

        Directory.CreateDirectory(Path.Combine(RootFolder, "NAMTeam"));
        Directory.CreateDirectory(Path.Combine(RootFolder, "UlisseWolf"));
        Directory.CreateDirectory(Path.Combine(RootFolder, "Local"));
    }

    /// <summary>Where a New_Properties.xml developer should drop their working copy to override both downloads.</summary>
    public string LocalOverridePath => Path.Combine(RootFolder, "Local", "new_properties.xml");

    public bool HasLocalOverride => File.Exists(LocalOverridePath);

    public string GetCachedPath(PropertySource source) => Path.Combine(RootFolder, FolderName(source), "new_properties.xml");

    public bool HasCachedCopy(PropertySource source) => File.Exists(GetCachedPath(source));

    public static string DisplayName(PropertySource source) => source switch
    {
        PropertySource.NamTeam => "NAM Team (vanilla/BSC)",
        PropertySource.UlisseWolfPatches => "UlisseWolf Patches (CAM-compatible)",
        _ => source.ToString(),
    };

    /// <summary>
    /// Checks the given source for updates and downloads a new copy if the remote content
    /// changed (or if no cached copy exists yet). Never throws on network failure - falls
    /// back to whatever cached copy already exists, since being offline should never block
    /// opening the app.
    /// </summary>
    /// <returns>A short human-readable status message suitable for display in the UI.</returns>
    public async Task<string> CheckForUpdateAsync(PropertySource source)
    {
        var meta = LoadMeta(source);
        var request = new HttpRequestMessage(HttpMethod.Get, SourceUrl(source));
        if (!string.IsNullOrEmpty(meta.ETag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", meta.ETag);
        }

        try
        {
            using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
            meta.LastChecked = DateTimeOffset.UtcNow;

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                SaveMeta(source, meta);
                return "Property database is already up to date.";
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            await File.WriteAllTextAsync(GetCachedPath(source), content).ConfigureAwait(false);

            meta.ETag = response.Headers.ETag?.Tag;
            meta.LastDownloaded = DateTimeOffset.UtcNow;
            SaveMeta(source, meta);

            return "Property database updated successfully.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            SaveMeta(source, meta);
            return HasCachedCopy(source)
                ? "Could not reach GitHub: using the previously saved copy."
                : "Could not reach GitHub and no copy is saved: the database cannot be loaded.";
        }
    }

    /// <summary>
    /// Resolves the actual file path to load definitions from for the given source: the
    /// developer's local override if present (see <see cref="LocalOverridePath"/>),
    /// otherwise the cached download for that source (which the caller should have
    /// requested via <see cref="CheckForUpdateAsync"/> first on a first run).
    /// </summary>
    public string? ResolveActivePath(PropertySource source)
    {
        if (HasLocalOverride)
        {
            return LocalOverridePath;
        }

        return HasCachedCopy(source) ? GetCachedPath(source) : null;
    }

    // ---------------------------------------------------------------
    // "Last used source" persistence - so the startup dialog can default to whatever the
    // person picked last time, even though (per requirements) it always asks again.
    // ---------------------------------------------------------------

    private string SettingsPath => Path.Combine(RootFolder, "settings.json");

    public PropertySource LoadLastUsedSource()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppPropertySettings>(json);
                if (settings is not null && Enum.TryParse<PropertySource>(settings.LastPropertySource, out var parsed))
                {
                    return parsed;
                }
            }
        }
        catch
        {
            // Fall through to the default below.
        }

        return PropertySource.NamTeam;
    }

    public void SaveLastUsedSource(PropertySource source)
    {
        try
        {
            var settings = new AppPropertySettings { LastPropertySource = source.ToString() };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
        }
        catch
        {
            // Best-effort only.
        }
    }

    private sealed class AppPropertySettings
    {
        public string? LastPropertySource { get; set; }
    }

    private static string FolderName(PropertySource source) => source switch
    {
        PropertySource.NamTeam => "NAMTeam",
        PropertySource.UlisseWolfPatches => "UlisseWolf",
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };

    private static string SourceUrl(PropertySource source) => source switch
    {
        PropertySource.NamTeam => NamTeamUrl,
        PropertySource.UlisseWolfPatches => UlisseWolfUrl,
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };

    private string MetaPath(PropertySource source) => Path.Combine(RootFolder, FolderName(source), "meta.json");

    private PropertySourceMeta LoadMeta(PropertySource source)
    {
        var path = MetaPath(source);
        if (!File.Exists(path))
        {
            return new PropertySourceMeta();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PropertySourceMeta>(json) ?? new PropertySourceMeta();
        }
        catch
        {
            return new PropertySourceMeta();
        }
    }

    private void SaveMeta(PropertySource source, PropertySourceMeta meta)
    {
        try
        {
            File.WriteAllText(MetaPath(source), JsonSerializer.Serialize(meta));
        }
        catch
        {
            // Best-effort only; a failed meta write just means we re-check next time
            // instead of trusting a (possibly stale) ETag.
        }
    }
}
