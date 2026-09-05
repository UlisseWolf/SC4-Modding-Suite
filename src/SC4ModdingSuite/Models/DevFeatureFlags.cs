using System;
using System.IO;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Checks for a hidden, developer-only feature flag that unlocks the "NAM Development
/// Suite" button in the External Tools panel. The flag lives in
/// <c>%APPDATA%\SC4ModdingSuite\dev-features.toml</c> - the same parent folder as
/// <c>new_properties.xml</c>, the theme palettes, and the language files - but, unlike
/// those, this file is <b>never</b> seeded/shipped by <see cref="EmbeddedResourceSeeder"/>
/// and is not part of the public distribution: it simply doesn't exist unless someone who
/// already knows about it places it there by hand, which is the point (an internal/trusted
/// switch, not a discoverable setting). If the file is absent, or the flag inside it isn't
/// literally <c>true</c>, every part of the UI gated on <see cref="IsNamDevelopmentSuiteEnabled"/>
/// stays completely hidden (not just disabled) - no visible hint the feature exists.
///
/// <code>
/// # %APPDATA%\SC4ModdingSuite\dev-features.toml
/// nam_development_suite = true
/// </code>
/// </summary>
public static class DevFeatureFlags
{
    private const string FileName = "dev-features.toml";

    private static string FolderPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SC4ModdingSuite");

    /// <summary>True only if the (unshipped, hand-placed) flag file exists and sets <c>nam_development_suite = true</c>.</summary>
    public static bool IsNamDevelopmentSuiteEnabled()
    {
        try
        {
            var path = Path.Combine(FolderPath, FileName);
            if (!File.Exists(path))
            {
                return false;
            }

            var map = TomlParser.Parse(File.ReadAllText(path));
            return map.TryGetValue("nam_development_suite", out var value)
                   && bool.TryParse(value, out var enabled)
                   && enabled;
        }
        catch
        {
            // A missing/malformed/inaccessible flag file just means the feature stays
            // locked, never an error the person sees.
            return false;
        }
    }
}
