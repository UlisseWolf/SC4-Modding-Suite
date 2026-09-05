namespace SC4ModdingSuite.Models;

/// <summary>
/// Validates that raw bytes decode as a well-formed binary Exemplar/Cohort ("EQZB1###"
/// format) by delegating to <see cref="ExemplarBinaryParser.Parse"/> - rather than
/// independently reimplementing the same offset-walking decode loop, this just reduces
/// the parser's result to a pass/fail/count/error summary.
///
/// Used as a safety check before exporting an Exemplar/Cohort entry to a standalone file,
/// so a genuinely malformed property never gets silently written to disk without at least
/// a clear, actionable warning naming the exact offset and property index where decoding
/// broke down - matching exactly how Ilive Reader's own decoder would fail on the same
/// bytes (its <c>default: return;</c> for an unrecognized data type silently truncates the
/// property list rather than erroring, which is worth flagging explicitly here instead of
/// staying silent about it too).
/// </summary>
public static class ExemplarBinaryValidator
{
    public readonly record struct ValidationResult(bool IsValid, int PropertiesParsed, string? Error);

    public static ValidationResult Validate(byte[]? data)
    {
        var parsed = ExemplarBinaryParser.Parse(data);

        // Not a binary-encoded Exemplar (e.g. the rarer EQZT text format) - the parser
        // treats this as unparseable, but there's nothing to validate here, so it's not
        // an error as far as the validator is concerned.
        if (parsed.Error == "Not a binary-encoded (EQZB) Exemplar.")
        {
            return new ValidationResult(true, 0, null);
        }

        return new ValidationResult(parsed.IsWellFormed, parsed.Properties.Count, parsed.Error);
    }
}
