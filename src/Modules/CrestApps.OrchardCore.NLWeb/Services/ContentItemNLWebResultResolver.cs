using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.Seo;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Resolves the content items the retrievers found into results: loads their published versions, keeps the ones the
/// caller may view and the content types the settings allow, and describes each one with the same schema.org data
/// its page publishes.
/// </summary>
internal sealed class ContentItemNLWebResultResolver : INLWebResultResolver
{
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IStructuredDataManager _structuredDataManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemNLWebResultResolver"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="structuredDataManager">The structured data manager.</param>
    public ContentItemNLWebResultResolver(
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        IStructuredDataManager structuredDataManager)
    {
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _structuredDataManager = structuredDataManager;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NLWebResult>> ResolveAsync(NLWebAskContext context, IReadOnlyList<NLWebCandidate> candidates, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            return [];
        }

        var settings = NLWebSettingsQueryProcessor.GetSettings(context);
        var allowedTypes = settings.ContentTypes is { Length: > 0 }
            ? new HashSet<string>(settings.ContentTypes, StringComparer.OrdinalIgnoreCase)
            : null;

        var contentItems = await _contentManager.GetAsync(candidates.Select(candidate => candidate.Id), VersionOptions.Published);
        var contentItemsById = new Dictionary<string, ContentItem>(StringComparer.Ordinal);

        foreach (var contentItem in contentItems)
        {
            contentItemsById.TryAdd(contentItem.ContentItemId, contentItem);
        }

        var results = new List<NLWebResult>();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!contentItemsById.TryGetValue(candidate.Id, out var contentItem) ||
                (allowedTypes is not null && !allowedTypes.Contains(contentItem.ContentType)) ||
                !await _authorizationService.AuthorizeAsync(context.User, CommonPermissions.ViewContent, contentItem))
            {
                continue;
            }

            var item = await _structuredDataManager.BuildEntityAsync(contentItem, StructuredDataPurpose.Answer);

            if (item is null)
            {
                continue;
            }

            results.Add(new NLWebResult
            {
                Id = contentItem.ContentItemId,
                Item = item,
                Url = JsonLd.GetString(item, "url"),
                Name = JsonLd.GetString(item, "name") ?? contentItem.DisplayText,
                Score = candidate.Score,
            });
        }

        return FilterByItemType(results, context.ItemType);
    }

    /// <summary>
    /// Keeps the results of the schema.org type asked for. When none has that type, every result is kept: the type may
    /// have been guessed, and an answer of other types is better than no answer.
    /// </summary>
    /// <param name="results">The results.</param>
    /// <param name="itemType">The schema.org type asked for, or <see langword="null"/>.</param>
    /// <returns>The results to return.</returns>
    internal static IReadOnlyList<NLWebResult> FilterByItemType(IReadOnlyList<NLWebResult> results, string itemType)
    {
        if (string.IsNullOrWhiteSpace(itemType))
        {
            return results;
        }

        var matching = results
            .Where(result => result.Item is JsonObject item && string.Equals(JsonLd.GetType(item), itemType.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matching.Count > 0 ? matching : results;
    }
}
