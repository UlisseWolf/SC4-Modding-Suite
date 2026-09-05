using System.IO;
using csDBPF;

namespace SC4ModdingSuite.ViewModels;

/// <summary>One hit in an Analysis-mode results grid (Find/Index Analyser, Property Find/Count) - which file it came from and its TGI.</summary>
public sealed class AnalysisResultRowViewModel
{
    public required string FilePath { get; init; }
    public required TGI Tgi { get; init; }
    public required string EntryType { get; init; }
    public long SizeBytes { get; init; }

    public string TgiText => $"{Tgi.TypeID:X8}-{Tgi.GroupID:X8}-{Tgi.InstanceID:X8}";
    public string FileName => Path.GetFileName(FilePath);
}
