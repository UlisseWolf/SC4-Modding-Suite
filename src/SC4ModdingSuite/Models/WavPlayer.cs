using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Plays raw WAV byte data, cross-platform:
///
/// <list type="bullet">
/// <item>
/// <b>Windows</b>: the Windows Multimedia API (<c>winmm.dll</c>'s <c>PlaySound</c>, using
/// <c>SND_MEMORY</c>) plays the bytes directly from memory - the same underlying API
/// Ilive Reader itself uses for sound playback (<c>SoundFile.cpp</c>, via <c>mmio*</c>/
/// <c>waveOut*</c>). No temp file needed.
/// </item>
/// <item>
/// <b>macOS</b>: the bytes are written to a temp <c>.wav</c> file and played with
/// <c>afplay</c>, a command-line player bundled with every macOS install since 10.5 -
/// no extra dependency to install.
/// </item>
/// <item>
/// <b>Linux</b>: same temp-file approach, trying <c>paplay</c> (PulseAudio/PipeWire-pulse,
/// present on most modern desktop distros), then <c>aplay</c> (ALSA, present on almost
/// every distro regardless of desktop environment), then <c>ffplay</c> (only if ffmpeg
/// happens to be installed) - the first one that actually starts wins.
/// </item>
/// </list>
///
/// This avoids pulling in a native cross-platform audio library (which would need
/// per-platform native binaries bundled with the app) by reusing whatever audio player
/// each OS already ships with or commonly has installed. If none of the Linux candidates
/// are present, <see cref="Play"/> returns <see langword="false"/> and the caller can
/// show a message suggesting one of them be installed.
/// </summary>
public static class WavPlayer
{
    [SupportedOSPlatform("windows")]
    [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool PlaySound(byte[]? data, IntPtr hMod, uint flags);

    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;

    // (executable, extra args before the file path) tried in order on Linux.
    private static readonly (string Exe, string[] Args)[] LinuxPlayers =
    {
        ("paplay", Array.Empty<string>()),
        ("aplay", Array.Empty<string>()),
        ("ffplay", new[] { "-nodisp", "-autoexit", "-loglevel", "quiet" }),
    };

    private static Process? _currentProcess;
    private static string? _currentTempFile;

    /// <summary>Starts playing <paramref name="wavData"/> asynchronously (returns immediately).</summary>
    public static bool Play(byte[] wavData)
    {
        Stop();

        if (OperatingSystem.IsWindows())
        {
            return PlaySound(wavData, IntPtr.Zero, SndMemory | SndAsync | SndNoDefault);
        }

        if (OperatingSystem.IsMacOS())
        {
            return TryPlayViaProcess(wavData, "afplay", Array.Empty<string>());
        }

        if (OperatingSystem.IsLinux())
        {
            foreach (var (exe, args) in LinuxPlayers)
            {
                if (TryPlayViaProcess(wavData, exe, args))
                {
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    /// <summary>Stops any sound currently playing via <see cref="Play"/>.</summary>
    public static void Stop()
    {
        if (OperatingSystem.IsWindows())
        {
            PlaySound(null, IntPtr.Zero, 0);
        }

        try
        {
            if (_currentProcess is { HasExited: false } process)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort; the process may have already exited on its own.
        }

        _currentProcess = null;
        CleanupTempFile();
    }

    private static bool TryPlayViaProcess(byte[] wavData, string executable, string[] extraArgs)
    {
        string? tempFile = null;
        try
        {
            tempFile = Path.Combine(Path.GetTempPath(), $"sc4modsuite_{Guid.NewGuid():N}.wav");
            File.WriteAllBytes(tempFile, wavData);

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var arg in extraArgs)
            {
                startInfo.ArgumentList.Add(arg);
            }

            startInfo.ArgumentList.Add(tempFile);

            var process = Process.Start(startInfo);
            if (process is null)
            {
                File.Delete(tempFile);
                return false;
            }

            _currentProcess = process;
            _currentTempFile = tempFile;

            // Clean up the temp file once playback finishes on its own (Stop() handles
            // the case where playback is cut short manually).
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => CleanupTempFile();

            return true;
        }
        catch
        {
            // Executable not found/not runnable - let the caller try the next candidate.
            if (tempFile is not null && File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // Ignore.
                }
            }

            return false;
        }
    }

    private static void CleanupTempFile()
    {
        var path = _currentTempFile;
        if (path is null)
        {
            return;
        }

        _currentTempFile = null;

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A stray temp file left behind isn't worth surfacing an error for.
        }
    }
}
