using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Resolves the item ids of the product feeds: a product's content item id, or its id and a variant's id joined by a
/// dash.
/// </summary>
public sealed class ContentItemAgentItemResolver : IAgentItemResolver
{
    // Content item ids are 26 characters, with no dash; a variant's id follows a dash.
    private const int ContentItemIdLength = 26;

    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemAgentItemResolver"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    public ContentItemAgentItemResolver(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    /// <inheritdoc />
    public async Task<AgentItem> ResolveAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        itemId = itemId.Trim();

        var contentItemId = itemId;
        string variantId = null;

        if (itemId.Length > ContentItemIdLength + 1 && itemId[ContentItemIdLength] == '-')
        {
            contentItemId = itemId.Substring(0, ContentItemIdLength);
            variantId = itemId.Substring(ContentItemIdLength + 1);
        }

        var contentItem = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);

        return contentItem is null ? null : new AgentItem(itemId, contentItem.ContentItemId, variantId);
    }

    /// <inheritdoc />
    public string GetItemId(string contentItemId, string variantId, string sku)
        => string.IsNullOrEmpty(variantId) ? contentItemId : $"{contentItemId}-{variantId}";
}
