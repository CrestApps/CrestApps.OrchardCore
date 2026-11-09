using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Moves the leads that were already called to the contacted status. Before a completed activity moved a lead out of
/// the default status, a lead called once stayed <c>Open - Not Contacted</c>, so a load that picked that status loaded
/// it again. This puts every such lead -- still in the default status, not converted, with a completed activity --
/// where a lead completed today would be.
/// </summary>
/// <remarks>
/// It reads the leads in pages and commits each page in its own session, so a large tenant is never held in one
/// transaction. It saves the documents through the store rather than the content manager: the change is a status
/// correction, not an edit, so no content handler or audit entry should run for each of possibly many thousand leads.
/// It is safe to run again: a lead it moved is no longer in the default status.
/// </remarks>
internal sealed class LeadContactedStatusBackfill
{
    internal const int PageSize = 200;

    private readonly IStore _store;
    private readonly INamedCatalog<LeadStatus> _statuses;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadContactedStatusBackfill"/> class.
    /// </summary>
    /// <param name="store">The store the leads and activities are read from.</param>
    /// <param name="statuses">The lead status catalog.</param>
    /// <param name="logger">The logger.</param>
    public LeadContactedStatusBackfill(
        IStore store,
        INamedCatalog<LeadStatus> statuses,
        ILogger<LeadContactedStatusBackfill> logger)
    {
        _store = store;
        _statuses = statuses;
        _logger = logger;
    }

    /// <summary>
    /// Moves every lead that is still in the default status and has a completed activity to the contacted status.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of lead documents moved, counting a published and a draft version separately.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await _statuses.GetAllAsync(cancellationToken);
        var defaultStatus = statuses.FirstOrDefault(status => status.IsDefault && !status.IsClosed && !status.IsConverted);
        var contactedStatus = statuses.FirstOrDefault(status => status.IsContacted && !status.IsClosed && !status.IsConverted);

        if (defaultStatus is null || contactedStatus is null || defaultStatus.ItemId == contactedStatus.ItemId)
        {
            _logger.LogInformation("Skipped moving called leads to the contacted status, because the CRM has no default or no contacted lead status.");

            return 0;
        }

        var defaultStatusId = defaultStatus.ItemId;
        var contactedStatusId = contactedStatus.ItemId;
        var documentId = 0L;
        var moved = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var session = _store.CreateSession();

            // Every version of a lead has its own row, so a published lead with a draft has both moved.
            var rows = (await session.QueryIndex<LeadIndex>(index =>
                    index.StatusId == defaultStatusId &&
                    !index.IsConverted &&
                    index.DocumentId > documentId)
                .OrderBy(index => index.DocumentId)
                .Take(PageSize)
                .ListAsync(cancellationToken))
                .ToList();

            if (rows.Count == 0)
            {
                break;
            }

            documentId = rows[^1].DocumentId;

            var leadIds = rows
                .Select(row => row.ContentItemId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var calledLeadIds = (await session.QueryIndex<OmnichannelActivityIndex>(index =>
                    index.ContactContentItemId.IsIn(leadIds) &&
                    index.Status == ActivityStatus.Completed,
                    collection: OmnichannelConstants.CollectionName)
                .ListAsync(cancellationToken))
                .Select(index => index.ContactContentItemId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (calledLeadIds.Count == 0)
            {
                continue;
            }

            var documentIds = rows
                .Where(row => calledLeadIds.Contains(row.ContentItemId))
                .Select(row => row.DocumentId)
                .ToArray();

            var movedOnPage = 0;

            foreach (var contentItem in await session.GetAsync<ContentItem>(documentIds, cancellationToken: cancellationToken))
            {
                // The index can lag its document, so the document has the last word.
                if (contentItem is null ||
                    !contentItem.TryGet<LeadPart>(out var leadPart) ||
                    leadPart.IsConverted ||
                    !string.Equals(leadPart.StatusId, defaultStatusId, StringComparison.Ordinal))
                {
                    continue;
                }

                contentItem.Alter<LeadPart>(part => part.StatusId = contactedStatusId);

                await session.SaveAsync(contentItem, cancellationToken: cancellationToken);

                movedOnPage++;
            }

            await session.SaveChangesAsync(cancellationToken);

            moved += movedOnPage;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Moved {Count} lead document(s) that already had a completed activity from the lead status '{DefaultStatus}' to '{ContactedStatus}'.",
                moved,
                defaultStatus.Name,
                contactedStatus.Name);
        }

        return moved;
    }
}
