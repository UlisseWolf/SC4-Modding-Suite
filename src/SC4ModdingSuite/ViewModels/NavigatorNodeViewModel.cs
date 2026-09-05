using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// One folder or file in the MDI shell's "Navigator" tree (Views/MainWindow.axaml's left
/// panel) - Ilive Reader's own Workspace bar (WorkspaceBar.cpp), showing the Plugins and
/// SC4 Install folders so a package can be opened straight from there instead of always
/// going through File > Open. Subfolders are listed first (alphabetically), then recognized
/// SC4 package files (.dat/.sc4lot/.sc4desc/.sc4model); anything else in a folder
/// (readme.txt, screenshots, ...) is left out, matching Ilive Reader's own Workspace bar
/// only ever listing recognized package files.
///
/// Children are loaded lazily, one directory at a time, the first time a node is expanded
/// (<see cref="IsExpanded"/> is bound TwoWay to each generated TreeViewItem's own IsExpanded
/// in Views/MainWindow.axaml's TreeView.Styles) - not eagerly walked for the whole subtree up
/// front. A cheap existence check at construction time (<see cref="HasAnyRecognizedEntry"/>)
/// decides whether to show an expander arrow at all, without loading the actual children.
/// </summary>
public sealed class NavigatorNodeViewModel : ViewModelBase
{
    private static readonly string[] Sc4Extensions = { ".dat", ".sc4lot", ".sc4desc", ".sc4model" };

    private bool _childrenLoaded;
    private bool _isExpanded;

    /// <summary>Root-level node not backed by an actual folder yet (e.g. "PLUGINS" before Options sets a path).
    /// Starts expanded so, once <see cref="AddRootFolder"/> points it at a real folder, that folder is
    /// immediately visible instead of hidden behind an extra click on a node that has nothing else to show.</summary>
    public NavigatorNodeViewModel(string name)
    {
        Name = name;
        IsDirectory = true;
        Path = null;
        _childrenLoaded = true; // real content only ever arrives via AddRootFolder, not LoadChildren
        _isExpanded = true;
    }

    private NavigatorNodeViewModel(string path, bool isDirectory)
    {
        Path = path;
        IsDirectory = isDirectory;
        var name = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        Name = string.IsNullOrEmpty(name) ? path : name; // empty for a drive root, e.g. "C:\"

        if (isDirectory)
        {
            // Cheap check only (Any() stops at the first match) - just enough to know whether
            // to show an expander arrow. The actual children aren't read until this node is
            // expanded (see EnsureChildrenLoaded).
            if (HasAnyRecognizedEntry(path))
            {
                Children.Add(LoadingPlaceholder());
            }
            else
            {
                _childrenLoaded = true;
            }
        }
        else
        {
            _childrenLoaded = true; // files never have children
        }
    }

    public string Name { get; }

    /// <summary>Full filesystem path, or null for a synthetic root node (e.g. "PLUGINS" with no folder configured yet).</summary>
    public string? Path { get; }

    public bool IsDirectory { get; }

    public bool IsSc4Package => !IsDirectory && Path is not null
        && Sc4Extensions.Contains(System.IO.Path.GetExtension(Path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Bound TwoWay to each TreeViewItem's own IsExpanded (Views/MainWindow.axaml's
    /// TreeView.Styles) - flipping to true triggers <see cref="EnsureChildrenLoaded"/>.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetField(ref _isExpanded, value) && value)
            {
                EnsureChildrenLoaded();
            }
        }
    }

    public ObservableCollection<NavigatorNodeViewModel> Children { get; } = new();

    /// <summary>Points a synthetic root node (see the name-only constructor) at a real folder, (re)building its subtree.</summary>
    public void AddRootFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Children.Clear();
        Children.Add(new NavigatorNodeViewModel(path, isDirectory: true));
    }

    /// <summary>A non-functional child node used only to make an unexpanded directory show an
    /// expander arrow before its real children have been read from disk. Swapped out for the
    /// real children (or removed, if the folder turns out empty/inaccessible after all) the
    /// first time this node is expanded.</summary>
    private static NavigatorNodeViewModel LoadingPlaceholder() => new("...");

    /// <summary>True as soon as a directory has at least one subfolder or one recognized SC4
    /// package file - stops at the first match, so this is cheap even on a huge folder.</summary>
    private static bool HasAnyRecognizedEntry(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).Any()
                || Directory.EnumerateFiles(path)
                    .Any(f => Sc4Extensions.Contains(System.IO.Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Loads this node's immediate children (subfolders, then recognized package
    /// files) the first time it's expanded. Only ever reads one level deep - each subfolder
    /// child does its own cheap <see cref="HasAnyRecognizedEntry"/> check for its expander
    /// arrow and loads its own children only once IT is expanded in turn.</summary>
    private void EnsureChildrenLoaded()
    {
        if (_childrenLoaded || Path is null)
        {
            return;
        }

        _childrenLoaded = true;

        try
        {
            var dirs = Directory.EnumerateDirectories(Path)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .Select(d => new NavigatorNodeViewModel(d, isDirectory: true))
                .ToList();

            var files = Directory.EnumerateFiles(Path)
                .Where(f => Sc4Extensions.Contains(System.IO.Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(f => new NavigatorNodeViewModel(f, isDirectory: false))
                .ToList();

            Children.Clear(); // drops the loading placeholder
            foreach (var dir in dirs)
            {
                Children.Add(dir);
            }

            foreach (var file in files)
            {
                Children.Add(file);
            }
        }
        catch
        {
            // Folder removed since the cheap existence check, or inaccessible (permissions) -
            // just drop the placeholder so the node reports itself as empty instead of leaving
            // a dead "..." entry behind.
            Children.Clear();
        }
    }
}
