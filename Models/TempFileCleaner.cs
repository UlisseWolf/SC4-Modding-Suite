using System.IO;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Deletes this app's own leftover temporary files - ports Ilive Reader's
/// DlgOption::OnBnClickedclean ("Clean" button -&gt; theApp.CleanState()). The only temp
/// files this app currently produces are WavPlayer's "sc4modsuite_*.wav" scratch copies
/// (see Models/WavPlayer.cs) - normally deleted right after playback finishes/the external
/// player process exits, but can be left behind if the app or that process is killed
/// mid-play.
/// </summary>
public static class TempFileCleaner
{
    private const string FilePrefix = "sc4modsuite_";

    /// <returns>Count of files deleted and their total size in bytes.</returns>
    public static (int count, long bytes) Clean()
    {
        var count = 0;
        long bytes = 0;

        try
        {
            foreach (var path in Directory.EnumerateFiles(Path.GetTempPath(), $"{FilePrefix}*"))
            {
                try
                {
                    var info = new FileInfo(path);
                    var size = info.Length;
                    info.Delete();
                    count++;
                    bytes += size;
                }
                catch
                {
                    // Still in use (e.g. currently playing) or already gone - skip it, not fatal to the batch.
                }
            }
        }
        catch
        {
            // Temp folder itself inaccessible - nothing to clean.
        }

        return (count, bytes);
    }
}
