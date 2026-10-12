using CrestApps.OrchardCore.Addresses.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Addresses.Services;

/// <summary>
/// Resolves a content item that carries an <c>AddressPart</c> into a money-safe <see cref="Address"/> whose
/// components hold stable codes (falling back to display names). Consumers such as taxation, checkout, and
/// subscriptions keep working against the flat, string-based <see cref="Address"/> model even though the
/// geographic components are captured as content-item selectors.
/// </summary>
public interface IAddressResolver
{
    /// <summary>
    /// Resolves the <c>AddressPart</c> of the supplied content item into a money-safe <see cref="Address"/>.
    /// Each geographic selector is loaded and reduced to its stable code, or to its display name when no code
    /// is present. The postal code is copied verbatim.
    /// </summary>
    /// <param name="contentItem">The content item carrying the <c>AddressPart</c> to resolve.</param>
    /// <returns>The resolved money-safe address. Never <see langword="null"/>.</returns>
    ValueTask<Address> ResolveAsync(ContentItem contentItem);
}
