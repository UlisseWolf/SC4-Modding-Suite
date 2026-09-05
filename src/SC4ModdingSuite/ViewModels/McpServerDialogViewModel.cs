using System;
using System.Text.Json;
using System.Threading.Tasks;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>Backs McpServerDialog - start/stop the in-process MCP HTTP server with a live status readout, plus a one-click client config generator. See McpServerLauncher's own doc comment for what "start" here actually verifies.</summary>
public sealed class McpServerDialogViewModel : ViewModelBase, IDisposable
{
    private readonly AppOptions _options;
    private readonly AppOptionsService _optionsService;
    private readonly McpServerLauncher _launcher = new();

    public McpServerDialogViewModel(AppOptions options, AppOptionsService optionsService)
    {
        _options = options;
        _optionsService = optionsService;
        _portText = options.McpServerPort.ToString();
        UpdateStatus("Stopped.");
    }

    private string _portText;
    /// <summary>The port to listen on - kept as text so the field can be temporarily empty/invalid mid-edit without crashing; validated in <see cref="StartAsync"/>.</summary>
    public string PortText
    {
        get => _portText;
        set => SetField(ref _portText, value);
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set => SetField(ref _isRunning, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    /// <summary>Persists <see cref="PortText"/> to Options immediately (if it's a valid port) - called before Start, so it survives even if the person closes this panel without an explicit Save button.</summary>
    public void SavePort()
    {
        if (int.TryParse(PortText, out var port) && port is > 0 and <= 65535)
        {
            _options.McpServerPort = port;
            _optionsService.Save(_options);
        }
    }

    public async Task StartAsync()
    {
        if (!int.TryParse(PortText, out var port) || port is <= 0 or > 65535)
        {
            UpdateStatus($"\"{PortText}\" isn't a valid port (1-65535).");
            return;
        }

        SavePort();
        IsBusy = true;
        UpdateStatus("Starting...");

        try
        {
            var result = await _launcher.StartAsync(port);
            IsRunning = result.Success;
            UpdateStatus(result.Success
                ? $"Running at {_launcher.Url} - {result.ToolCount} tools available."
                : $"Failed to start: {result.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Stop()
    {
        _launcher.Stop();
        IsRunning = false;
        UpdateStatus("Stopped.");
    }

    private void UpdateStatus(string text) => StatusText = text;

    /// <summary>
    /// The exact JSON snippet a client's own config file (e.g. Claude Desktop's
    /// <c>claude_desktop_config.json</c>) needs under its own <c>"mcpServers"</c> object to
    /// connect to this server over HTTP - with the URL already filled in, so there's
    /// nothing left to hand-edit. Returns <see langword="null"/> if the server isn't
    /// running (there's no URL yet to give out).
    /// </summary>
    public string? BuildClientConfigSnippet()
    {
        if (_launcher.Url is not { } url)
        {
            return null;
        }

        var snippet = new
        {
            mcpServers = new System.Collections.Generic.Dictionary<string, object>
            {
                ["sc4-dbpf"] = new { url },
            },
        };

        return JsonSerializer.Serialize(snippet, new JsonSerializerOptions { WriteIndented = true });
    }

    public void Dispose() => _launcher.Dispose();
}
