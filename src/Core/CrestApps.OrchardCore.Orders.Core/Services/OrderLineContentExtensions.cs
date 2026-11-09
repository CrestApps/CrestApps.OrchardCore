using CrestApps.OrchardCore.Orders.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Reads the product an order line bought as it was when it was bought.
/// </summary>
public static class OrderLineContentExtensions
{
    /// <summary>
    /// Returns the version of the line's product that was bought, through <see cref="OrderLine.ContentItemVersionId"/>,
    /// so what is granted after payment is what was sold, not what the product is today or a draft of it. A line without
    /// a version, such as one from an older order, reads the published product.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="line">The order line.</param>
    /// <returns>The purchased content item, or <see langword="null"/> when it no longer exists.</returns>
    public static async Task<ContentItem> GetPurchasedAsync(this IContentManager contentManager, OrderLine line)
    {
        ArgumentNullException.ThrowIfNull(contentManager);
        ArgumentNullException.ThrowIfNull(line);

        if (!string.IsNullOrEmpty(line.ContentItemVersionId) &&
            await contentManager.GetVersionAsync(line.ContentItemVersionId) is { } purchased)
        {
            return purchased;
        }

        return string.IsNullOrEmpty(line.ContentItemId)
            ? null
            : await contentManager.GetAsync(line.ContentItemId, VersionOptions.Published);
    }
}
