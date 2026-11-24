using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// Reads the published learning package items.
/// </summary>
public interface IPackageContentSource
{
    /// <summary>
    /// Gets the published version of a content item.
    /// </summary>
    /// <param name="contentItemId">The content item id.</param>
    /// <returns>The item, or <see langword="null"/>.</returns>
    Task<ContentItem> GetPublishedAsync(string contentItemId);
}

/// <summary>
/// Reads content items through the content manager.
/// </summary>
internal sealed class DefaultPackageContentSource : IPackageContentSource
{
    private readonly IContentManager _contentManager;

    public DefaultPackageContentSource(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public async Task<ContentItem> GetPublishedAsync(string contentItemId)
        => string.IsNullOrEmpty(contentItemId) ? null : await _contentManager.GetAsync(contentItemId, VersionOptions.Published);
}
