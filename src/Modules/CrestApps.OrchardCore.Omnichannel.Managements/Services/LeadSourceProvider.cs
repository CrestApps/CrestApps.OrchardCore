using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Reads the lead sources, the published items of the <see cref="OmnichannelConstants.ContentTypes.LeadSource"/>
/// content type. Leads and opportunities store the content item identifier of their source; this service turns the
/// identifier into a name for display and a name into an identifier for imports and searches.
/// </summary>
public sealed class LeadSourceProvider
{
    private readonly ISession _session;

    private IReadOnlyList<ContentItem> _sources;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadSourceProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public LeadSourceProvider(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Returns the published lead sources ordered by name. The list is read once per request.
    /// </summary>
    public async Task<IReadOnlyList<ContentItem>> GetAllAsync()
    {
        if (_sources is null)
        {
            var sources = await _session.Query<ContentItem, ContentItemIndex>(index =>
                    index.ContentType == OmnichannelConstants.ContentTypes.LeadSource &&
                    index.Published)
                .ListAsync();

            _sources = sources
                .OrderBy(source => source.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return _sources;
    }

    /// <summary>
    /// Returns the lead sources as options for a select list.
    /// </summary>
    /// <param name="selectedId">The identifier of the selected lead source, if any.</param>
    public async Task<IList<SelectListItem>> GetOptionsAsync(string selectedId)
    {
        return (await GetAllAsync())
            .Select(source => new SelectListItem(source.DisplayText, source.ContentItemId, source.ContentItemId == selectedId))
            .ToList();
    }

    /// <summary>
    /// Returns the name of a lead source, or <see langword="null"/> when there is no published lead source with the
    /// identifier.
    /// </summary>
    /// <param name="sourceId">The content item identifier of the lead source.</param>
    public async Task<string> GetNameAsync(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId))
        {
            return null;
        }

        return (await GetAllAsync()).FirstOrDefault(source => source.ContentItemId == sourceId)?.DisplayText;
    }

    /// <summary>
    /// Returns the names of the lead sources keyed by content item identifier.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync()
    {
        return (await GetAllAsync()).ToDictionary(source => source.ContentItemId, source => source.DisplayText, StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns the identifier of the lead source with a name, ignoring case, or with that identifier. Returns
    /// <see langword="null"/> when no published lead source matches.
    /// </summary>
    /// <param name="nameOrId">The name or the content item identifier of the lead source.</param>
    public async Task<string> FindIdAsync(string nameOrId)
    {
        if (string.IsNullOrWhiteSpace(nameOrId))
        {
            return null;
        }

        var value = nameOrId.Trim();

        return (await GetAllAsync()).FirstOrDefault(source =>
            string.Equals(source.DisplayText, value, StringComparison.OrdinalIgnoreCase) ||
            source.ContentItemId == value)?.ContentItemId;
    }
}
