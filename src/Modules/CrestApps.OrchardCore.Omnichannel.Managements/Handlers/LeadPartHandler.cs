using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Gives a new lead the default status and copies the status's closed flag onto the lead whenever it is saved, so
/// the lead index can filter open and closed leads without reading the status catalog.
/// </summary>
internal sealed class LeadPartHandler : ContentPartHandler<LeadPart>
{
    private readonly INamedCatalog<LeadStatus> _statuses;

    private IReadOnlyList<LeadStatus> _cachedStatuses;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadPartHandler"/> class.
    /// </summary>
    /// <param name="statuses">The lead status catalog.</param>
    public LeadPartHandler(INamedCatalog<LeadStatus> statuses)
    {
        _statuses = statuses;
    }

    public override Task CreatingAsync(CreateContentContext context, LeadPart part)
        => ApplyStatusAsync(part);

    public override Task UpdatingAsync(UpdateContentContext context, LeadPart part)
        => ApplyStatusAsync(part);

    public override Task ImportingAsync(ImportContentContext context, LeadPart part)
        => ApplyStatusAsync(part);

    private async Task ApplyStatusAsync(LeadPart part)
    {
        var statuses = await GetStatusesAsync();

        if (part.IsConverted)
        {
            part.StatusId ??= statuses.FirstOrDefault(status => status.IsConverted)?.ItemId;
            part.IsClosed = true;

            // A part a handler changes is a copy until it is applied back to the content item.
            part.Apply();

            return;
        }

        var status = string.IsNullOrEmpty(part.StatusId)
            ? null
            : statuses.FirstOrDefault(entry => entry.ItemId == part.StatusId);

        if (status is null)
        {
            status = statuses.FirstOrDefault(entry => entry.IsDefault && !entry.IsConverted)
                ?? statuses.FirstOrDefault(entry => !entry.IsClosed);

            part.StatusId = status?.ItemId;
        }

        part.IsClosed = status?.IsClosed == true;

        part.Apply();
    }

    private async Task<IReadOnlyList<LeadStatus>> GetStatusesAsync()
        => _cachedStatuses ??= (await _statuses.GetAllAsync())
            .OrderBy(status => status.Order)
            .ToArray();
}
