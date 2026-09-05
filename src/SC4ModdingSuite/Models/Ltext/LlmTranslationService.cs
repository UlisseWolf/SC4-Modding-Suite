using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Optional "generate a first-draft translation" call for the LTEXT Editor's Translation
/// Grid (see <c>LtextTranslationDialog</c>) - entirely opt-in, and entirely useless without
/// the user's own <see cref="AppOptions.LlmApiKey"/> set in Options first (see that
/// property's own doc comment on how/where it's stored). Never called automatically; only
/// ever in direct response to the person clicking a specific "Translate with AI" button on
/// a specific cell, and the result always lands in the editable grid for review - it is
/// never written into an entry or saved to disk on its own.
///
/// <para>
/// Speaks the OpenAI "/chat/completions" JSON schema, which is also what most compatible
/// providers and local servers (LM Studio, Ollama's own OpenAI-compat endpoint, OpenRouter,
/// ...) implement - so pointing <see cref="AppOptions.LlmBaseUrl"/> at any of those works
/// the same way, not just OpenAI itself. This app never hardcodes which provider/model to
/// use; both come directly from what the user typed into Options.
/// </para>
/// </summary>
public sealed class LlmTranslationService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };

    /// <summary>
    /// Asks the configured LLM to translate <paramref name="sourceText"/> from
    /// <paramref name="sourceLanguage"/> into <paramref name="targetLanguage"/>, returning
    /// just the translated string (no explanation/preamble - the prompt explicitly asks
    /// for that). Throws <see cref="InvalidOperationException"/> with a friendly message on
    /// any failure (no key configured, network error, non-2xx response, unparsable
    /// response) - callers show that message directly rather than a raw exception.
    /// </summary>
    public async Task<string> TranslateAsync(
        AppOptions options, string sourceText, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.LlmApiKey))
        {
            throw new InvalidOperationException("No AI provider API key is set - add one in Options first.");
        }

        if (string.IsNullOrWhiteSpace(options.LlmModel))
        {
            throw new InvalidOperationException("No AI model name is set - add one in Options first (e.g. \"gpt-4o-mini\").");
        }

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            throw new InvalidOperationException("Nothing to translate - the source cell is empty.");
        }

        var systemPrompt =
            "You translate short strings from a city-building video game's in-game UI/help text " +
            "(SimCity 4). Translate the user's message from " + sourceLanguage + " to " + targetLanguage +
            ". Preserve any placeholder tokens, line breaks, and punctuation style exactly. " +
            "Reply with ONLY the translated text - no quotes, no explanation, no original text.";

        var requestBody = new
        {
            model = options.LlmModel,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = sourceText },
            },
            temperature = 0.3,
        };

        var baseUrl = options.LlmBaseUrl.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.LlmApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Couldn't reach the AI provider: {ex.Message}");
        }

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = TryExtractErrorMessage(responseText) ?? responseText;
            throw new InvalidOperationException($"AI provider returned {(int)response.StatusCode}: {Truncate(detail, 300)}");
        }

        var translated = TryExtractContent(responseText);
        if (translated is null)
        {
            throw new InvalidOperationException("AI provider response didn't contain the expected content - check the Base URL/model in Options.");
        }

        return translated.Trim();
    }

    private static string? TryExtractContent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
        catch
        {
            return null;
        }
    }

    private static string? TryExtractErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("error").GetProperty("message").GetString();
        }
        catch
        {
            return null;
        }
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "...";
}
