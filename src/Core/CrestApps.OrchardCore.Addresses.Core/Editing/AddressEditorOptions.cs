namespace CrestApps.OrchardCore.Addresses.Core.Editing;

/// <summary>
/// What a page asks for in its address editor, besides what the country requires.
/// </summary>
public sealed class AddressEditorOptions
{
    public static readonly AddressEditorOptions Default = new();

    /// <summary>
    /// Gets whether the editor asks who receives mail at the address.
    /// </summary>
    public bool ShowName { get; init; } = true;

    /// <summary>
    /// Gets whether the recipient must be filled in.
    /// </summary>
    public bool RequireName { get; init; }

    /// <summary>
    /// Gets whether the editor asks for a company.
    /// </summary>
    public bool ShowCompany { get; init; } = true;

    /// <summary>
    /// Gets whether the editor asks for a phone number for the courier.
    /// </summary>
    public bool ShowPhone { get; init; }

    /// <summary>
    /// Gets the codes of the countries offered, or <see langword="null"/> for every country the site serves.
    /// </summary>
    public IReadOnlyCollection<string> Countries { get; init; }

    /// <summary>
    /// Gets the country chosen for a new address, such as the store's own.
    /// </summary>
    public string DefaultCountry { get; init; }
}
