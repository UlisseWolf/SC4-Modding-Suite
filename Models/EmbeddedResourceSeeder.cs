using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Copies the built-in default <c>.toml</c> files (embedded in the assembly at build time
/// via <c>&lt;EmbeddedResource&gt;</c> in the <c>.csproj</c>) out to a real folder on disk
/// the first time they're needed, without ever overwriting a value the person may have since
/// edited. Used by <see cref="LocalizationService"/> and <see cref="ThemeService"/> so their
/// data files live under <c>%APPDATA%\SC4ModdingSuite\</c> - the same place as
/// <c>new_properties.xml</c> (see <see cref="PropertySourceService"/>) - instead of next to
/// the built executable, and are still editable there without a rebuild.
///
/// <para>
/// If the target file already exists (from a previous run), only the <c>key = "value"</c>
/// lines it's still missing are appended - it is never overwritten wholesale. This is what
/// lets an app update add new keys (e.g. the LUA Editor's toolbar strings) and have them
/// actually show up for people who already have an on-disk copy from an older build; a
/// plain "skip if the file exists" (the previous behavior) left those new keys permanently
/// unreachable, since <see cref="LocalizationService.Get"/> falls back to printing the raw
/// key name when a translation is missing.
/// </para>
/// </summary>
public static class EmbeddedResourceSeeder
{
    public static void SeedFolder(string targetFolder, string resourceFolderName)
    {
        Directory.CreateDirectory(targetFolder);

        var assembly = Assembly.GetExecutingAssembly();
        var prefix = $"SC4ModdingSuite.{resourceFolderName}.";

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = resourceName[prefix.Length..];
            var targetPath = Path.Combine(targetFolder, fileName);

            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream is null)
            {
                continue;
            }

            using var reader = new StreamReader(resourceStream);
            var defaultText = reader.ReadToEnd();

            if (!File.Exists(targetPath))
            {
                File.WriteAllText(targetPath, defaultText);
                continue;
            }

            AppendMissingKeys(targetPath, defaultText);
        }
    }

    private static void AppendMissingKeys(string targetPath, string defaultText)
    {
        var existingKeys = TomlParser.Parse(File.ReadAllText(targetPath)).Keys;
        var missingLines = new List<string>();

        foreach (var rawLine in defaultText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith('['))
            {
                continue;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..equalsIndex].Trim().Trim('"');
            if (!existingKeys.Contains(key))
            {
                missingLines.Add(line);
            }
        }

        if (missingLines.Count > 0)
        {
            File.AppendAllLines(targetPath, missingLines);
        }
    }
}
