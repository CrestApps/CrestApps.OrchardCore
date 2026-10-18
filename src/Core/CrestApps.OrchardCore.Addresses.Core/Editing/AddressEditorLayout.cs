namespace CrestApps.OrchardCore.Addresses.Core.Editing;

/// <summary>
/// How the address editor lays out one country's address: the lines of components, their labels, which are required,
/// and the country's regions. The page renders it, and the editor's script asks for another country's when the country
/// changes.
/// </summary>
public sealed class AddressEditorLayout
{
    public string Country { get; init; }

    /// <summary>
    /// Gets the lines of the address, each a list of <see cref="Models.AddressComponent"/> names, without the country.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> Lines { get; init; } = [];

    /// <summary>
    /// Gets the names of the components the country requires.
    /// </summary>
    public IReadOnlyList<string> Required { get; init; } = [];

    /// <summary>
    /// Gets each component's label, by component name, in the visitor's language.
    /// </summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the country's regions. When there are any the region is chosen from them.
    /// </summary>
    public IReadOnlyList<AddressEditorRegion> Regions { get; init; } = [];

    /// <summary>
    /// Gets an example postal code, shown as a hint.
    /// </summary>
    public string PostalCodeExample { get; init; }

    public bool Uses(string component)
        => Lines.Any(line => line.Contains(component, StringComparer.Ordinal));

    public bool IsRequired(string component)
        => Required.Contains(component, StringComparer.Ordinal);

    public string LabelOf(string component)
        => Labels.TryGetValue(component, out var label) ? label : component;
}

/// <summary>
/// A region as the editor offers it: the code it stores and the name it shows.
/// </summary>
public sealed record AddressEditorRegion(string Code, string Name);
