namespace SC4ModdingSuite.Models;

/// <summary>
/// General app options, persisted as a single JSON file by <see cref="AppOptionsService"/>.
/// </summary>
public sealed class AppOptions
{
    /// <summary>Path to SimCityLocale.DAT, for reference/quick-open convenience.</summary>
    public string? SimCityLocalePath { get; set; }

    /// <summary>SC4 installation folder - used as the starting folder when opening files.</summary>
    public string? Sc4InstallFolder { get; set; }

    /// <summary>
    /// The user's SC4 Plugins folder (typically <c>Documents\SimCity 4\Plugins</c>) - where
    /// custom/downloaded mod files actually live, as opposed to <see cref="Sc4InstallFolder"/>
    /// (the base game's own install folder). Preferred over <see cref="Sc4InstallFolder"/> as
    /// the starting folder when opening files, since that's what someone using this app to
    /// edit their own mods will need most often.
    /// </summary>
    public string? PluginsFolder { get; set; }

    public string? PimXPath { get; set; }
    public string? DataNodePath { get; set; }
    public string? MapperPath { get; set; }
    public string? TerraformerPath { get; set; }
    public string? Sc4PacEditorPath { get; set; }

    /// <summary>
    /// Path to the "NAM Development Suite" tool. Only ever surfaced in the UI (Options
    /// path field, External Tools button) when <see cref="DevFeatureFlags.IsNamDevelopmentSuiteEnabled"/>
    /// is true - see that class for how the feature is unlocked.
    /// </summary>
    public string? NamDevelopmentSuitePath { get; set; }

    /// <summary>Language code (matches a Localization/&lt;code&gt;.toml file). English is primary/default.</summary>
    public string Language { get; set; } = "en";

    /// <summary>Theme key ("default" for plain Fluent, or a Themes/&lt;key&gt;.toml file).</summary>
    public string Theme { get; set; } = "bloomberg";

    // --- AI-assisted translation (LTEXT Editor's Translation Grid - see
    // Models/LlmTranslationService.cs) - entirely optional, off unless a key is set. Stored
    // in this same plain-text options.json like everything else here - there is no OS
    // keychain/credential-manager integration, so this key sits on disk unencrypted, same
    // as every other path/setting in this file. The Options dialog says so explicitly next
    // to the field. Never logged, never included in StatusMessage/exports/bug reports -
    // only ever read directly into the Authorization header of a request this app sends
    // straight to whichever LlmBaseUrl is configured, from the user's own machine. ---

    /// <summary>API key for the configured LLM provider - null/empty means the AI translation feature is simply unavailable (no key, no calls).</summary>
    public string? LlmApiKey { get; set; }

    /// <summary>Base URL of an OpenAI-compatible "/chat/completions" endpoint - defaults to OpenAI's own, but any compatible provider or local server (LM Studio, Ollama's OpenAI-compat shim, ...) works by pointing this elsewhere.</summary>
    public string LlmBaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>Model name to request - left blank by default (no single default is safe to hardcode/guaranteed available across every provider) - the Options dialog prompts for one once a key is entered.</summary>
    public string LlmModel { get; set; } = string.Empty;

    /// <summary>
    /// Port the in-process MCP HTTP server (see Mcp/McpHttpServer.cs) listens on when
    /// started from the MCP Server panel - kept stable across app restarts so a real
    /// client's own config (pointing at this same port) keeps working without needing to
    /// be updated every time. Only ever binds to 127.0.0.1, never the network.
    /// </summary>
    public int McpServerPort { get; set; } = 7337;
}
