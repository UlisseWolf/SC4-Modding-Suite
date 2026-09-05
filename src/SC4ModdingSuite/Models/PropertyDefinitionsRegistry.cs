using System;
using System.Collections.Generic;
using System.Linq;

namespace SC4ModdingSuite.Models;

/// <summary>
/// In-memory lookup of <see cref="PropertyDefinition"/> by ID or name, loaded from a
/// new_properties.xml file on disk via <see cref="PropertyDefinitionsXmlParser"/>. Name
/// matching is case-insensitive and ignores spaces, mirroring csDBPF's own
/// <c>XMLProperties.GetXMLProperty(string)</c> matching rule.
/// </summary>
public sealed class PropertyDefinitionsRegistry
{
    private readonly Dictionary<uint, PropertyDefinition> _byId = new();
    private readonly Dictionary<string, PropertyDefinition> _byName = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byId.Count;

    /// <summary>Human-readable label of where the loaded definitions came from (for display).</summary>
    public string SourceDescription { get; private set; } = "No property database loaded";

    public IReadOnlyCollection<PropertyDefinition> All => _byId.Values;

    public void Load(string xmlPath, string sourceDescription)
    {
        _byId.Clear();
        _byName.Clear();

        foreach (var definition in PropertyDefinitionsXmlParser.Parse(xmlPath))
        {
            _byId[definition.Id] = definition;
            _byName[NormalizeName(definition.Name)] = definition;
        }

        SourceDescription = $"{sourceDescription} ({_byId.Count:N0} properties)";
    }

    public PropertyDefinition? FindById(uint id)
    {
        if (_byId.TryGetValue(id, out var exact))
        {
            return exact;
        }

        return TryResolveRepeatingProperty(id);
    }

    /// <summary>
    /// Some SC4 Exemplar properties repeat many times within one entry using an
    /// <b>incrementing ID scheme</b> instead of a "count + array" layout - the classic
    /// example being <c>LotConfigPropertyLotObject</c> in Lot Configuration (T10)
    /// Exemplars, one instance per building/prop/texture/flora/network placement on the
    /// lot, IDs <c>0x88EDC900</c> through <c>0x88EDC900 + 1279</c> (<c>0x88EDCDFF</c>) -
    /// confirmed via the SC4 Devotion wiki and Simtropolis community documentation.
    /// <c>new_properties.xml</c> only documents the base ID; every other instance is a
    /// perfectly valid property that's simply absent from the database under its own
    /// specific ID, which is why they were previously all shown as "not present in the
    /// property database". Even Ilive Reader has this exact limitation (only recognizes
    /// the first occurrence, labels the rest "Unknown"); SC4PIM resolves every instance,
    /// which is what this matches. Deliberately narrow: this is the one specific,
    /// well-documented repeating-ID family handled here, not a generic guess for any
    /// property that happens to look unresolved.
    /// </summary>
    private PropertyDefinition? TryResolveRepeatingProperty(uint id)
    {
        const uint lotConfigPropertyLotObjectBase = 0x88EDC900;
        const uint lotConfigPropertyLotObjectCount = 1280;

        if (id < lotConfigPropertyLotObjectBase || id >= lotConfigPropertyLotObjectBase + lotConfigPropertyLotObjectCount)
        {
            return null;
        }

        var index = id - lotConfigPropertyLotObjectBase;
        var baseDefinition = _byId.GetValueOrDefault(lotConfigPropertyLotObjectBase);

        return new PropertyDefinition
        {
            Id = id,
            Name = $"{baseDefinition?.Name ?? "LotConfigPropertyLotObject"} #{index}",
            DataType = baseDefinition?.DataType ?? csDBPF.DBPFProperty.PropertyDataType.UINT32,
            Description = baseDefinition?.Description,
            Count = baseDefinition?.Count ?? 0,
            MinValue = baseDefinition?.MinValue,
            MaxValue = baseDefinition?.MaxValue,
            Step = baseDefinition?.Step ?? 0,
            Options = baseDefinition?.Options ?? Array.Empty<PropertyDefinitionOption>(),
        };
    }

    public PropertyDefinition? FindByName(string name) => _byName.GetValueOrDefault(NormalizeName(name));

    /// <summary>Finds definitions whose name or hex ID contains <paramref name="query"/>, name-sorted.</summary>
    public IEnumerable<PropertyDefinition> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return All.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase);
        }

        var normalizedQuery = NormalizeName(query);
        return All
            .Where(d => NormalizeName(d.Name).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                        || d.Id.ToString("X8").Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string name) => name.Replace(" ", string.Empty);
}
