using System;
using System.Collections.Generic;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>One named "Value=..." choice for a property, e.g. Wealth Type 1 = "Low".</summary>
public sealed class PropertyDefinitionOption
{
    public required string Name { get; init; }
    public required string Value { get; init; }
}

/// <summary>
/// One property entry as described by new_properties.xml: its human-friendly name, expected
/// data type, description, and (for enum-like properties) named value options. This is our
/// own model - independent of csDBPF's internal (embedded, unswappable) copy of the same
/// data - so the source file can be downloaded/switched/updated at runtime (see
/// <see cref="PropertySourceService"/>).
/// </summary>
public sealed class PropertyDefinition
{
    public required uint Id { get; init; }
    public required string Name { get; init; }
    public DBPFProperty.PropertyDataType DataType { get; init; } = DBPFProperty.PropertyDataType.UNKNOWN;
    public string? Description { get; init; }
    public string? DefaultValue { get; init; }
    public int Count { get; init; }
    public string? MinValue { get; init; }
    public string? MaxValue { get; init; }
    public int Step { get; init; }
    public IReadOnlyList<PropertyDefinitionOption> Options { get; init; } = Array.Empty<PropertyDefinitionOption>();
}
