namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Which field the Entries (TGI) list is currently sorted by - clicking a "header" button
/// above the list sets this (see <see cref="MainWindowViewModel.SortEntriesCommand"/>),
/// mirroring the clickable-column-header sort in Ilive Reader's own Workspace entry list
/// (reader/WorkspaceList.cpp). <see cref="Type"/> is this app's own addition beyond parity
/// with Ilive Reader (which has no sortable column for the classified type label, only an
/// icon); <see cref="TypeId"/>/<see cref="GroupId"/>/<see cref="InstanceId"/> match Ilive
/// Reader's own Type/Group/Instance columns exactly (always sorted by the raw numeric TGI
/// value, never the hex text).
/// </summary>
public enum EntrySortColumn
{
    /// <summary>Unsorted - entries stay in the package's own on-disk order.</summary>
    None,
    Type,
    TypeId,
    GroupId,
    InstanceId,
}
