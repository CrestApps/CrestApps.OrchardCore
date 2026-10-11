using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Resolves an inherited tax classification for a content item from a source other than the item's own
/// <see cref="TaxationPart"/>. Providers let a content item inherit its classification, for example from
/// the taxonomy terms (product categories) it belongs to, so that tax codes can be managed per category
/// rather than repeated on every item.
/// </summary>
/// <remarks>
/// A classification set explicitly on the item's own <see cref="TaxationPart"/> always takes precedence.
/// Providers are consulted in ascending <see cref="Order"/> only when the item does not classify itself,
/// and the first provider that yields a category wins.
/// </remarks>
public interface ITaxClassificationProvider
{
    /// <summary>
    /// Gets the relative order in which the provider is consulted. Lower values are consulted first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Resolves the inherited tax classification for the supplied content item.
    /// </summary>
    /// <param name="contentItem">The content item to resolve the classification for.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The resolved classification, or <see langword="null"/> when the provider cannot classify the item.</returns>
    ValueTask<TaxClassification> GetClassificationAsync(ContentItem contentItem, CancellationToken cancellationToken = default);
}
