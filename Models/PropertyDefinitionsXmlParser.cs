using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Parses new_properties.xml (the exemplar-property name/type/description database shared
/// by PIM-X and Ilive Reader) into a flat list of <see cref="PropertyDefinition"/>.
///
/// <para>
/// This is our own independent implementation of what Ilive Reader's
/// <c>InitPropertyList()</c> (<c>or_dat/sim015_const.cpp</c>) does. It has to be independent
/// because csDBPF ships its own copy of new_properties.xml baked in as an embedded resource
/// (<c>csDBPF.Properties.new_properties.xml</c>) that cannot be swapped at runtime - we need
/// our own parser to support the downloadable/switchable/developer-local versions of the
/// file requested here. The schema below is confirmed against two independent sources:
/// Ilive Reader's C++ parser AND csDBPF's own <c>XMLExemplarProperty</c> field list
/// (ID, Name, DataType, Count, DefaultValue, MinValue/MinLength, MaxValue/MaxLength, Step),
/// which match it exactly:
/// </para>
///
/// <code>
/// &lt;EXEMPLARPROPERTIES&gt;
///   &lt;PROPERTIES&gt;
///     &lt;PROPERTY ID="0x..." Name="..." Type="Uint32" Count="1" Default="..."
///               MinValue="..." MaxValue="..." Step="..."&gt;
///       &lt;HELP&gt;description&lt;/HELP&gt;
///       &lt;OPTION Value="0x1" Name="Low"/&gt;
///       ...
///     &lt;/PROPERTY&gt;
///   &lt;/PROPERTIES&gt;
/// &lt;/EXEMPLARPROPERTIES&gt;
/// </code>
/// </summary>
public static class PropertyDefinitionsXmlParser
{
    public static List<PropertyDefinition> Parse(string xmlPath)
    {
        var doc = XDocument.Load(xmlPath);
        var results = new List<PropertyDefinition>();

        var root = doc.Root;
        if (root is null)
        {
            return results;
        }

        // Search the whole document for PROPERTY elements rather than assuming a fixed
        // EXEMPLARPROPERTIES/PROPERTIES nesting depth - both known sources use that
        // structure, but being lenient here means a minor future layout tweak upstream
        // doesn't silently break every property lookup in the app.
        foreach (var propertyElement in root.DescendantsAndSelf()
                     .Where(e => string.Equals(e.Name.LocalName, "PROPERTY", StringComparison.OrdinalIgnoreCase)))
        {
            var definition = ParseProperty(propertyElement);
            if (definition is not null)
            {
                results.Add(definition);
            }
        }

        return results;
    }

    private static PropertyDefinition? ParseProperty(XElement element)
    {
        var idText = GetAttribute(element, "ID");
        var name = GetAttribute(element, "Name");

        if (idText is null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (!TryParseId(idText, out var id))
        {
            return null;
        }

        var typeText = GetAttribute(element, "Type");
        var dataType = string.IsNullOrEmpty(typeText)
            ? DBPFProperty.PropertyDataType.UNKNOWN
            : DBPFProperty.LookupDataType(typeText);

        int.TryParse(GetAttribute(element, "Count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count);
        int.TryParse(GetAttribute(element, "Step"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var step);

        var minValue = GetAttribute(element, "MinValue") ?? GetAttribute(element, "MinLength");
        var maxValue = GetAttribute(element, "MaxValue") ?? GetAttribute(element, "MaxLength");
        var defaultValue = GetAttribute(element, "Default");

        var help = element.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "HELP", StringComparison.OrdinalIgnoreCase));
        var description = CleanDescription(help?.Value);

        var options = element.Elements()
            .Where(e => string.Equals(e.Name.LocalName, "OPTION", StringComparison.OrdinalIgnoreCase))
            .Select(e => new PropertyDefinitionOption
            {
                Name = GetAttribute(e, "Name") ?? string.Empty,
                Value = GetAttribute(e, "Value") ?? string.Empty,
            })
            .ToList();

        return new PropertyDefinition
        {
            Id = id,
            Name = name,
            DataType = dataType,
            Description = description,
            DefaultValue = string.IsNullOrEmpty(defaultValue) ? null : defaultValue,
            Count = count,
            MinValue = string.IsNullOrEmpty(minValue) ? null : minValue,
            MaxValue = string.IsNullOrEmpty(maxValue) ? null : maxValue,
            Step = step,
            Options = options,
        };
    }

    private static string? CleanDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim('\r', '\n', ' ', '\t');
    }

    private static string? GetAttribute(XElement element, string name) => element.Attribute(name)?.Value;

    /// <summary>
    /// Parses a property ID written as a bare hex string ("0xAABBCCDD") or, less commonly,
    /// a decimal number - matching Ilive Reader's own "if it contains 'x', base 16, else
    /// base 10" rule from <c>InitPropertyList()</c>.
    /// </summary>
    private static bool TryParseId(string text, out uint id)
    {
        text = text.Trim();
        var xIndex = text.IndexOf('x', StringComparison.OrdinalIgnoreCase);
        if (xIndex >= 0)
        {
            return uint.TryParse(text[(xIndex + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id);
        }

        return uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }
}
