using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Coursework.Core.Services;

/// <summary>
/// Reads the content items coursework needs: the coursework item and its rubric.
/// </summary>
public interface ICourseworkContentSource
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
internal sealed class DefaultCourseworkContentSource : ICourseworkContentSource
{
    private readonly IContentManager _contentManager;

    public DefaultCourseworkContentSource(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public async Task<ContentItem> GetPublishedAsync(string contentItemId)
        => string.IsNullOrEmpty(contentItemId) ? null : await _contentManager.GetAsync(contentItemId, VersionOptions.Published);
}
