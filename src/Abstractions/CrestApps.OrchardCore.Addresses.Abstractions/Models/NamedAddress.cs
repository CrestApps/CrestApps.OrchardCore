namespace CrestApps.OrchardCore.Addresses.Models;

/// <summary>
/// A place with a name: a store, a warehouse, a pickup point, a register or a site. Records that keep a list of places
/// keep these instead of declaring their own id, name and address fields, or an address as one string.
/// </summary>
public sealed record NamedAddress
{
    /// <summary>
    /// Gets the place's id, unique among the places of its owner.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the place's name, such as <c>Main warehouse</c>.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the place's postal address.
    /// </summary>
    public Address Address { get; init; }
}
