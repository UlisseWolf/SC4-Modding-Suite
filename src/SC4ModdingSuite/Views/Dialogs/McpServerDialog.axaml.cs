using Avalonia.Controls;
using Avalonia.Interactivity;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>See McpServerDialog.axaml - start/stop the in-process MCP HTTP server, and generate the JSON a real MCP client's own config needs.</summary>
public partial class McpServerDialog : Window
{
    public McpServerDialog()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    public McpServerDialog(McpServerDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private McpServerDialogViewModel ViewModel => (McpServerDialogViewModel)DataContext!;

    private async void OnStartClick(object? sender, RoutedEventArgs e) => await ViewModel.StartAsync();

    private void OnStopClick(object? sender, RoutedEventArgs e) => ViewModel.Stop();

    private async void OnCopyConfigClick(object? sender, RoutedEventArgs e)
    {
        var snippet = ViewModel.BuildClientConfigSnippet();
        if (snippet is null)
        {
            return;
        }

        var clipboard = Avalonia.Controls.TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(snippet);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // The server only exists inside this process - closing the panel (or the whole
        // app) is the only way it can ever stop, so make sure it actually does rather than
        // leaving an orphaned listener bound to its port.
        if (ViewModel.IsRunning)
        {
            ViewModel.Stop();
        }

        ViewModel.Dispose();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
