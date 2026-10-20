using OrchardCore.ContentManagement;
using OrchardCore.Contents.Services;
using OrchardCore.Contents.ViewModels;
using OrchardCore.DisplayManagement.ModelBinding;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Wraps the content list query service, so a leads list is counted from the lead index alone.
/// </summary>
/// <remarks>
/// The query the list shows is the one the wrapped service builds. When <see cref="LeadListCountQueryFactory"/> can
/// count the same leads from the lead index, the query is handed back as a <see cref="LeadListQuery"/>, which reads
/// its count from there; every other list keeps its own count.
/// </remarks>
internal sealed class LeadContentsAdminListQueryService : IContentsAdminListQueryService
{
    private readonly IContentsAdminListQueryService _inner;
    private readonly LeadListCountQueryFactory _countQueryFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadContentsAdminListQueryService"/> class.
    /// </summary>
    /// <param name="inner">The content list query service it wraps.</param>
    /// <param name="countQueryFactory">Builds the count of a leads list.</param>
    public LeadContentsAdminListQueryService(
        IContentsAdminListQueryService inner,
        LeadListCountQueryFactory countQueryFactory)
    {
        _inner = inner;
        _countQueryFactory = countQueryFactory;
    }

    /// <inheritdoc/>
    public async Task<IQuery<ContentItem>> QueryAsync(ContentOptionsViewModel model, IUpdateModel updater)
    {
        ArgumentNullException.ThrowIfNull(model);

        var query = await _inner.QueryAsync(model, updater);
        var countQuery = await _countQueryFactory.TryCreateAsync(model);

        return countQuery is null
            ? query
            : new LeadListQuery(query, countQuery);
    }
}
