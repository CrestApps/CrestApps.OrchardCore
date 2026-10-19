namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Where each contact type keeps its phone numbers and email addresses.
/// </summary>
/// <remarks>
/// A contact type that is not listed keeps them in its <c>ContactMethods</c> bag. Register a type with
/// <c>AddContactMethodsProvider</c> rather than writing to these dictionaries directly, so the provider is also
/// registered with the container.
/// </remarks>
public sealed class ContactMethodsOptions
{
    /// <summary>
    /// Gets the provider type (an <c>IContactMethodsProvider</c>) that reads each listed content type's contact
    /// methods, keyed by content type name.
    /// </summary>
    public Dictionary<string, Type> Providers { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether contacts of a content type keep their phone numbers and email addresses in the
    /// <c>ContactMethods</c> bag, which is so for every type without a provider of its own.
    /// </summary>
    /// <param name="contentType">The content type name.</param>
    public bool UsesContactMethodsBag(string contentType)
        => string.IsNullOrEmpty(contentType) || !Providers.ContainsKey(contentType);
}
