namespace CrestApps.OrchardCore.Omnichannel.Models;

/// <summary>
/// One kind of contact a tenant has defined.
/// </summary>
/// <remarks>
/// A projection of whatever the host calls a contact type, so a service can ask what kinds of contact
/// exist and what each one tracks without knowing how the host models them.
/// </remarks>
public sealed class ContactDefinition
{
    /// <summary>
    /// Gets or sets the technical name, which is what a contact stores to say what kind it is.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name to show. Falls back to <see cref="Name"/> when the host has nothing better.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets what this kind of contact tracks.
    /// </summary>
    public ContactDefinitionSettings Settings { get; set; } = new();
}
