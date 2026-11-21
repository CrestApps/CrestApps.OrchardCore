using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Surveys.Core.Services;

/// <summary>
/// Reads the survey content items.
/// </summary>
public interface ISurveyContentSource
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
internal sealed class DefaultSurveyContentSource : ISurveyContentSource
{
    private readonly IContentManager _contentManager;

    public DefaultSurveyContentSource(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public async Task<ContentItem> GetPublishedAsync(string contentItemId)
        => string.IsNullOrEmpty(contentItemId) ? null : await _contentManager.GetAsync(contentItemId, VersionOptions.Published);
}
