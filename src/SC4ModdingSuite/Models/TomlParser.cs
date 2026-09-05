using System;
using System.Collections.Generic;
using System.Text;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Minimal TOML reader supporting exactly the subset needed for the translation files in
/// this app: "# comments", "[section]" table headers (returned as a "section.key" prefix,
/// though the app's own translation files intentionally avoid sections - see
/// <see cref="LocalizationService"/> for why), and "key = "value"" string assignments
/// (with <c>\\ \" \n \t \r</c> escapes and trailing "# comment" stripping). This is not a
/// general-purpose TOML parser (no arrays, numbers, dates, inline tables, multi-line
/// strings) - just enough for simple flat key → localized-string files.
/// </summary>
public static class TomlParser
{
    public static Dictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var currentSection = string.Empty;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('['))
            {
                var closeIndex = line.IndexOf(']');
                if (closeIndex > 0)
                {
                    currentSection = line[1..closeIndex].Trim();
                }

                continue;
            }

            var equalsIndex = FindUnquotedEquals(line);
            if (equalsIndex < 0)
            {
                continue;
            }

            var key = line[..equalsIndex].Trim().Trim('"');
            var rawValue = line[(equalsIndex + 1)..].Trim();
            var value = ParseStringValue(rawValue);

            var fullKey = string.IsNullOrEmpty(currentSection) ? key : $"{currentSection}.{key}";
            result[fullKey] = value;
        }

        return result;
    }

    private static int FindUnquotedEquals(string line)
    {
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == '=' && !inQuotes)
            {
                return i;
            }
        }

        return -1;
    }

    private static string ParseStringValue(string raw)
    {
        raw = StripTrailingComment(raw);

        return raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"'
            ? Unescape(raw[1..^1])
            : raw;
    }

    private static string StripTrailingComment(string raw)
    {
        var inQuotes = false;
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '"' && (i == 0 || raw[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
            }
            else if (raw[i] == '#' && !inQuotes)
            {
                return raw[..i].TrimEnd();
            }
        }

        return raw;
    }

    private static string Unescape(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                i++;
                sb.Append(s[i] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '"' => '"',
                    '\\' => '\\',
                    _ => s[i],
                });
            }
            else
            {
                sb.Append(s[i]);
            }
        }

        return sb.ToString();
    }
}
