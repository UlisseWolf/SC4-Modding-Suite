using csDBPF;

namespace SC4ModdingSuite.ViewModels;

public enum DirectorySyncStatus
{
    Ok,
    SizeMismatch,
    DeclaredButMissing,
    CompressedButNotDeclared,
}

/// <summary>
/// One row of the Directory sync dialog (Ilive Reader's DlgDirectory): a TGI declared in
/// the package's on-disk Directory (DIR) subfile, cross-checked against the actual entry
/// with that TGI in the file.
/// </summary>
public sealed class DirectoryRowViewModel
{
    public required TGI Tgi { get; init; }
    public uint DeclaredUncompressedSize { get; init; }
    public DBPFEntry? ActualEntry { get; init; }
    public DirectorySyncStatus Status { get; init; }

    public string TgiText => $"{Tgi.TypeID:X8}-{Tgi.GroupID:X8}-{Tgi.InstanceID:X8}";

    public string DeclaredSizeText => Status == DirectorySyncStatus.CompressedButNotDeclared
        ? "(not declared)"
        : $"{DeclaredUncompressedSize:N0} B";

    public string ActualSizeText => ActualEntry is null
        ? "(missing)"
        : $"{ActualEntry.GetSize():N0} B ({(ActualEntry.IsCompressed ? "compressed" : "uncompressed")})";

    public string StatusText => Status switch
    {
        DirectorySyncStatus.Ok => "OK",
        DirectorySyncStatus.SizeMismatch => "SIZE MISMATCH",
        DirectorySyncStatus.DeclaredButMissing => "DECLARED, ENTRY MISSING",
        DirectorySyncStatus.CompressedButNotDeclared => "COMPRESSED, NOT IN DIRECTORY",
        _ => string.Empty,
    };

    /// <summary>Can "Select in list" (DlgDirectory::OnMenuSync) jump to this row's entry?</summary>
    public bool CanSelect => ActualEntry is not null;
}
