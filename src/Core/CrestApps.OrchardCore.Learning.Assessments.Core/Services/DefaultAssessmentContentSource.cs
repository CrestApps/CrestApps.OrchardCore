using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Lists.Indexes;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Reads quizzes and questions through the content manager; a bank lists its questions through the List part.
/// </summary>
public sealed class DefaultAssessmentContentSource : IAssessmentContentSource
{
    private readonly IContentManager _contentManager;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAssessmentContentSource"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="session">The session.</param>
    public DefaultAssessmentContentSource(IContentManager contentManager, ISession session)
    {
        _contentManager = contentManager;
        _session = session;
    }

    /// <inheritdoc />
    public Task<ContentItem> GetItemAsync(string contentItemId)
        => string.IsNullOrEmpty(contentItemId) ? Task.FromResult<ContentItem>(null) : _contentManager.GetAsync(contentItemId, VersionOptions.Published);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentItem>> GetQuestionsAsync(IEnumerable<string> questionIds)
    {
        var ids = questionIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var items = (await _contentManager.GetAsync(ids, VersionOptions.Published)).ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);

        return ids.Where(items.ContainsKey).Select(id => items[id]).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentItem>> GetBankQuestionsAsync(string bankId)
    {
        if (string.IsNullOrEmpty(bankId))
        {
            return [];
        }

        return (await _session.Query<ContentItem, ContainedPartIndex>(index => index.ListContentItemId == bankId)
            .With<ContentItemIndex>(index => index.Published)
            .ListAsync()).ToList();
    }
}
