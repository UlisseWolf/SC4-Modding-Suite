using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using csDBPF;
using SC4ModdingSuite.ViewModels;
using ImgSharpImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Automatically indexes every UI element (TGI Type 0x00000000, the same "UI/UI" entries the
/// entry list already labels that way) - and, alongside that, every image entry (TGI Type
/// 0x856DDBAC, shared with BMP/JPEG), so UI dialogs referencing a shared chrome/background
/// image that isn't in their own file (see ResolveSharedImage) can find it without ever
/// having to scan the installation folder themselves - across the SC4 installation folder
/// and the Plugins folder, the same way SC4 PIM-X keeps its own index of Plugins content up
/// to date: a scan runs once in the background shortly after the app starts (see
/// App.axaml.cs), and each scanned file's result is cached to disk keyed by that file's own
/// size + last-write time, so an unchanged file is never re-opened/re-parsed on a later run -
/// only new or modified files actually get rescanned. One shared instance for the whole app
/// (constructed once in App.axaml.cs, like <see cref="PropertySourceService"/>/
/// <see cref="AppOptionsService"/>), not per-tab, since the index describes the Plugins/
/// Install folders themselves rather than any one open document.
/// </summary>
public sealed class UiElementIndexService : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly string _cachePath;
    private Dictionary<string, CachedFileEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>(Group,Instance) of an image entry -> which scanned file it lives in. Built
    /// alongside <see cref="_cache"/> (LoadCache/ScanFolders), read-only lookup table for
    /// <see cref="ResolveSharedImage"/> - never itself the trigger for any file-system scan,
    /// which is what previously made the UI Editor freeze (see that method's own doc comment).</summary>
    private Dictionary<(uint Group, uint Instance), string> _imageLocationIndex = new();

    public UiElementIndexService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "SC4ModdingSuite");
        Directory.CreateDirectory(folder);
        _cachePath = Path.Combine(folder, "ui_element_index.json");
        LoadCache();
        RebuildImageLocationIndex();
    }

    /// <summary>Every UI element found so far - live-updated in place as the background scan
    /// progresses (see RefreshAsync), so a DataGrid bound directly to this fills in
    /// incrementally instead of staying empty until the whole scan finishes.</summary>
    public ObservableCollection<AnalysisResultRowViewModel> Results { get; } = new();

    private string _statusMessage = "Not scanned yet.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusMessage)));
            }
        }
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (_isScanning != value)
            {
                _isScanning = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsScanning)));
            }
        }
    }

    private const uint ImageTypeId = 0x856DDBAC;
    private static readonly string[] Sc4FileExtensions = { ".dat", ".sc4lot", ".sc4desc", ".sc4model" };

    private readonly Dictionary<(uint Type, uint Group, uint Instance), ImgSharpImage?> _sharedImageCache = new();

    /// <summary>
    /// Looks up one specific entry (typically a shared UI chrome/background image) using the
    /// image-location index this service already builds during its normal background scan
    /// (see <see cref="RefreshAsync"/>/<see cref="ScanFolders"/>) - used as a fallback when a
    /// UI dialog references an image that isn't among the currently open file's own entries
    /// (see MainWindowViewModel.DecodeUiSourceImage), which is common: dialogs frequently
    /// reuse shared graphics (rounded borders, button skins, ...) that live in the base
    /// game's own package files rather than being duplicated into every single UI-definition
    /// .dat that uses them. Ilive Reader itself would show the same plain-fillcolor fallback
    /// box in that case if opening just that one file in isolation; searching the
    /// installation folder is this app's own addition on top of that.
    ///
    /// <para>
    /// PRIORITY INSTRUCTION: this method must NEVER itself trigger a filesystem scan (no
    /// Directory.EnumerateFiles over the whole installation folder). An earlier version did
    /// exactly that, synchronously, right on the UI thread that runs CollectPreviewBoxes -
    /// since a real SC4 installation folder holds several very large .dat files, opening
    /// every one of them (often several times over, once per distinct missing image
    /// reference in a single dialog) made switching to the UI Editor and selecting an
    /// element freeze the whole app for seconds at a time. This method only ever consults
    /// <see cref="_imageLocationIndex"/> (already built, no I/O) and then opens the ONE
    /// specific file it points to - if the background scan hasn't reached that file yet (or
    /// the image genuinely isn't anywhere in the installation folder), this returns null
    /// immediately without touching the filesystem at all; the caller falls back to a plain
    /// fillcolor box exactly as it would with no installation folder configured.
    /// </para>
    /// </summary>
    public ImgSharpImage? ResolveSharedImage(uint typeId, uint group, uint instance)
    {
        var key = (typeId, group, instance);
        if (_sharedImageCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        ImgSharpImage? found = null;

        // Only images are indexed by (Group,Instance) today (see ScanFolders) - if a caller
        // ever asks for a different TypeID, there's nothing to look up.
        if (typeId == ImageTypeId && _imageLocationIndex.TryGetValue((group, instance), out var path))
        {
            try
            {
                var file = new DBPFFile(path);
                var entry = file.ListOfEntries.FirstOrDefault(e =>
                    e.TGI.TypeID == typeId && e.TGI.GroupID == group && e.TGI.InstanceID == instance);

                // PRIORITY INSTRUCTION: never call DBPFEntryPNG.Decode() directly on a
                // possibly-compressed entry - see MainWindowViewModel.DecodePng's own doc
                // comment for the full explanation (confirmed against csDBPF's own source):
                // its Decode() skips the IsCompressed/QFS.Decompress check every other
                // structured entry type in csDBPF does, so a compressed image (routine for
                // base-game/shared UI chrome) got hex-sniffed by ImageSharp as if it were
                // already raw pixel data - "succeeding" with essentially garbage width/
                // height instead of throwing, which is why a dialog's background rendered
                // at some unrelated size no matter how many times the coordinate math and
                // sizing guarantees around it were re-verified as correct. Decoding directly
                // via ImageSharp after this app's own decompression sidesteps the gap.
                if (entry is not null)
                {
                    var bytes = RawEntryBytes.GetDecompressed(entry);
                    if (bytes is { Length: > 0 })
                    {
                        using var image = SixLabors.ImageSharp.Image.Load(bytes);
                        found = image.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
                    }
                }
            }
            catch
            {
                // File went missing/changed since it was indexed, is unreadable, or this
                // entry isn't actually a decodable PNG (a BMP/JPEG sharing the same TGI
                // Type) - leave found as null, same as "not indexed at all".
            }
        }

        // Only cache a SUCCESSFUL resolution. A null here can simply mean the (Group,
        // Instance) hasn't been reached by the background scan yet (RefreshAsync) - caching
        // that permanently would mean this image never resolves even once the scan finds
        // it moments later, which is exactly the "images sometimes just don't show up"
        // symptom this fixes. A genuinely-absent image is still cheap to look up again
        // (one dictionary miss) on the next call.
        if (found is not null)
        {
            _sharedImageCache[key] = found;
        }

        return found;
    }

    /// <summary>
    /// Rescans <paramref name="folders"/> (the SC4 installation folder and the Plugins folder -
    /// safe to call with either/both missing or nonexistent, e.g. before Options has been set
    /// up yet). Runs the actual file IO/parsing on a background thread; only touches
    /// <see cref="Results"/> on the caller's thread via the ObservableCollection additions
    /// below being marshalled back - callers on the UI thread (App.axaml.cs) get live updates
    /// for free since ObservableCollection raises CollectionChanged synchronously wherever
    /// the mutating call happens to run, so this method hops back with Dispatcher.UIThread.
    /// </summary>
    /// <summary>
    /// Rescans <paramref name="folders"/> - safe to call with either/both missing or
    /// nonexistent, e.g. before Options has been set up yet, and safe to call more than once
    /// with different folders (e.g. once for the SC4 installation folder, awaited before the
    /// main window appears - see App.axaml.cs - and again later for the Plugins folder in the
    /// background): only entries under the given folders are touched, previously-scanned
    /// folders' own results/cache entries are left alone rather than being wiped. Runs the
    /// actual file IO/parsing on a background thread; only touches <see cref="Results"/> on
    /// the caller's thread via the ObservableCollection additions below being marshalled back
    /// - callers on the UI thread get live updates for free since ObservableCollection raises
    /// CollectionChanged synchronously wherever the mutating call happens to run, so this
    /// method hops back with Dispatcher.UIThread.
    /// </summary>
    public async Task RefreshAsync(IEnumerable<string?> folders)
    {
        if (IsScanning)
        {
            return;
        }

        IsScanning = true;
        StatusMessage = "Scanning...";

        try
        {
            var folderList = folders.Where(f => !string.IsNullOrWhiteSpace(f) && Directory.Exists(f))
                .Select(f => Path.GetFullPath(f!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (folderList.Length == 0)
            {
                StatusMessage = "Set the SC4 installation folder and/or the Plugins folder in Options first.";
                return;
            }

            var (rows, scannedFiles, fromCache, reparsed) = await Task.Run(() => ScanFolders(folderList)).ConfigureAwait(false);
            RebuildImageLocationIndex();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Only replace results that came from THESE folders - a second call for a
                // different folder (e.g. Plugins, after an earlier awaited call already
                // populated the SC4 installation folder's own results) must not wipe out
                // what the first call already found.
                for (var i = Results.Count - 1; i >= 0; i--)
                {
                    if (folderList.Any(f => Results[i].FilePath.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
                    {
                        Results.RemoveAt(i);
                    }
                }

                foreach (var row in rows)
                {
                    Results.Add(row);
                }
            });

            SaveCache();
            StatusMessage = $"{rows.Count} UI element(s) across {scannedFiles} file(s) " +
                             $"({fromCache} from cache, {reparsed} (re)parsed).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private (List<AnalysisResultRowViewModel> Rows, int ScannedFiles, int FromCache, int Reparsed) ScanFolders(
        IReadOnlyList<string> folders)
    {
        var rows = new List<AnalysisResultRowViewModel>();
        var newCache = new Dictionary<string, CachedFileEntry>(StringComparer.OrdinalIgnoreCase);
        var scannedFiles = 0;
        var fromCache = 0;
        var reparsed = 0;

        foreach (var folder in folders)
        {
            IEnumerable<string> files;
            try
            {
                // Sorted, not just whatever order the filesystem happens to hand back
                // (Directory.EnumerateFiles makes no ordering guarantee at all) - this
                // matters because RebuildImageLocationIndex below keeps only the LAST file
                // seen for a given image (Group,Instance), and SC4's own Plugins load order
                // convention (numbered folders like "050-load-first" vs "895-my-overrides")
                // relies on exactly this kind of alphabetical/numeric ordering to decide
                // which of two files defining the same TGI - e.g. a UI skin mod
                // deliberately reusing the base game's own chrome image TGIs to override
                // them - should actually win. An unsorted/arbitrary enumeration order could
                // just as easily have picked the base game's own file over the override
                // one, or picked a different file each time the folder was rescanned.
                files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                    .Where(f => Sc4FileExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                continue;
            }

            foreach (var path in files)
            {
                scannedFiles++;
                List<CachedTgi> entries;
                List<CachedImageLocation> imageEntries;

                FileInfo info;
                try
                {
                    info = new FileInfo(path);
                }
                catch
                {
                    continue;
                }

                if (_cache.TryGetValue(path, out var cached) &&
                    cached.FileSizeBytes == info.Length &&
                    cached.LastWriteTimeUtcTicks == info.LastWriteTimeUtc.Ticks &&
                    cached.ImageEntries is not null)
                {
                    entries = cached.Entries;
                    imageEntries = cached.ImageEntries;
                    fromCache++;
                }
                else
                {
                    try
                    {
                        var allEntries = new DBPFFile(path).ListOfEntries;
                        entries = allEntries
                            .Where(e => e.TGI.TypeID == 0)
                            .Select(e => new CachedTgi
                            {
                                GroupId = e.TGI.GroupID,
                                InstanceId = e.TGI.InstanceID,
                                EntryType = e.TGI.GetEntryType(),
                                SizeBytes = e.GetSize(),
                            })
                            .ToList();
                        // Same pass, no extra file open: also remember where every image
                        // entry lives, purely as a (Group,Instance) location index (no
                        // decoding here) - see ResolveSharedImage/_imageLocationIndex.
                        imageEntries = allEntries
                            .Where(e => e.TGI.TypeID == ImageTypeId)
                            .Select(e => new CachedImageLocation { GroupId = e.TGI.GroupID, InstanceId = e.TGI.InstanceID })
                            .ToList();
                        reparsed++;
                    }
                    catch
                    {
                        // Unreadable/corrupt file - skip it (and don't cache a bad result for
                        // it either, so a later fix to the file gets picked up next scan).
                        continue;
                    }
                }

                newCache[path] = new CachedFileEntry
                {
                    FileSizeBytes = info.Length,
                    LastWriteTimeUtcTicks = info.LastWriteTimeUtc.Ticks,
                    Entries = entries,
                    ImageEntries = imageEntries,
                };

                foreach (var e in entries)
                {
                    rows.Add(new AnalysisResultRowViewModel
                    {
                        FilePath = path,
                        Tgi = new TGI(0, e.GroupId, e.InstanceId),
                        EntryType = e.EntryType,
                        SizeBytes = e.SizeBytes,
                    });
                }
            }
        }

        // Merge, don't replace - a second call scoped to a different folder (see
        // RefreshAsync's own doc comment) must not lose what an earlier call already found
        // for other folders.
        foreach (var kvp in newCache)
        {
            _cache[kvp.Key] = kvp.Value;
        }

        return (rows, scannedFiles, fromCache, reparsed);
    }

    private void LoadCache()
    {
        try
        {
            if (File.Exists(_cachePath))
            {
                var json = File.ReadAllText(_cachePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, CachedFileEntry>>(json);
                if (loaded is not null)
                {
                    _cache = new Dictionary<string, CachedFileEntry>(loaded, StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        catch
        {
            // Corrupt/unreadable cache - just rescan everything from scratch this run.
            _cache = new Dictionary<string, CachedFileEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveCache()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cache);
            File.WriteAllText(_cachePath, json);
        }
        catch
        {
            // Best-effort only; a failed write just means every file gets rescanned next run.
        }
    }

    private void RebuildImageLocationIndex()
    {
        var index = new Dictionary<(uint, uint), string>();
        foreach (var (path, fileEntry) in _cache)
        {
            if (fileEntry.ImageEntries is null)
            {
                continue;
            }

            foreach (var img in fileEntry.ImageEntries)
            {
                // Last one wins on a (rare) duplicate across files - good enough for a
                // fallback lookup; not worth tracking every location an image appears in.
                index[(img.GroupId, img.InstanceId)] = path;
            }
        }

        _imageLocationIndex = index;
    }

    private sealed class CachedFileEntry
    {
        public long FileSizeBytes { get; set; }
        public long LastWriteTimeUtcTicks { get; set; }
        public List<CachedTgi> Entries { get; set; } = new();

        /// <summary>Null (rather than an empty list) means this cache entry predates image
        /// indexing and must be treated as a cache miss so the file gets rescanned once to
        /// pick up its image entries too - see the ScanFolders reuse-from-cache check.</summary>
        public List<CachedImageLocation>? ImageEntries { get; set; }
    }

    private sealed class CachedTgi
    {
        public uint GroupId { get; set; }
        public uint InstanceId { get; set; }
        public string EntryType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    private sealed class CachedImageLocation
    {
        public uint GroupId { get; set; }
        public uint InstanceId { get; set; }
    }
}
