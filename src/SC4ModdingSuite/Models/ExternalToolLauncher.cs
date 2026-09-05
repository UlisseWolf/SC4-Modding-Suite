using System;
using System.Diagnostics;
using System.IO;

namespace SC4ModdingSuite.Models;

/// <summary>Launches an external SC4 modding tool (PIM-X, DataNode, Mapper, Terraformer, ...) by path.</summary>
public static class ExternalToolLauncher
{
    public static bool TryLaunch(string? exePath, out string error)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            error = "Path not configured. Set it in Options.";
            return false;
        }

        if (!File.Exists(exePath))
        {
            error = $"File not found: {exePath}";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty,
                UseShellExecute = true,
            });
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
