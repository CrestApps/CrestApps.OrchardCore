using CrestApps.OrchardCore.AI.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// Counts the published content items, such as an AI chat widget, that put an AI profile in front of site visitors.
/// </summary>
internal interface IPublishedChatWidgetFinder
{
    /// <summary>
    /// Counts the published content items whose AI profile part points at a profile.
    /// </summary>
    /// <param name="profileId">The ID of the profile.</param>
    /// <returns>The number of published content items.</returns>
    Task<int> CountAsync(string profileId);
}

/// <summary>
/// Finds the published content items with an <see cref="AIProfilePart"/> that names a profile, by reading every
/// published item of the content types that carry the part.
/// </summary>
internal sealed class PublishedChatWidgetFinder : IPublishedChatWidgetFinder
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishedChatWidgetFinder"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="session">The YesSql session.</param>
    public PublishedChatWidgetFinder(IContentDefinitionManager contentDefinitionManager, ISession session)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(string profileId)
    {
        if (string.IsNullOrEmpty(profileId))
        {
            return 0;
        }

        var partNamesByType = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Select(type => (type.Name, PartNames: type.Parts
                .Where(part => part.PartDefinition?.Name == nameof(AIProfilePart))
                .Select(part => part.Name)
                .ToArray()))
            .Where(type => type.PartNames.Length > 0)
            .ToDictionary(type => type.Name, type => type.PartNames, StringComparer.Ordinal);

        if (partNamesByType.Count == 0)
        {
            return 0;
        }

        var typeNames = partNamesByType.Keys.ToArray();
        var items = await _session.Query<ContentItem, ContentItemIndex>(index => index.Published && index.ContentType.IsIn(typeNames)).ListAsync();

        return items.Count(item => partNamesByType.TryGetValue(item.ContentType, out var partNames) &&
            partNames.Any(partName => string.Equals(item.Get<AIProfilePart>(partName)?.ProfileId, profileId, StringComparison.Ordinal)));
    }
}
