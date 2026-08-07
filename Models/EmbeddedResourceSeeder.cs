using System;
using System.IO;
using System.Reflection;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Copies the built-in default <c>.toml</c> files (embedded in the assembly at build time
/// via <c>&lt;EmbeddedResource&gt;</c> in the <c>.csproj</c>) out to a real folder on disk
/// the first time they're needed, without ever overwriting a file the person may have since
/// edited. Used by <see cref="LocalizationService"/> and <see cref="ThemeService"/> so their
/// data files live under <c>%APPDATA%\SC4ModdingSuite\</c> - the same place as
/// <c>new_properties.xml</c> (see <see cref="PropertySourceService"/>) - instead of next to
/// the built executable, and are still editable there without a rebuild.
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
            if (File.Exists(targetPath))
            {
                continue;
            }

            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream is null)
            {
                continue;
            }

            using var fileStream = File.Create(targetPath);
            resourceStream.CopyTo(fileStream);
        }
    }
}
