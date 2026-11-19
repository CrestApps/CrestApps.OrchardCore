using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tickets.WorkOrders;

/// <summary>
/// Tells a ticket when the work order made from it is completed or canceled, on the ticket's timeline, so its agent
/// knows to answer the requester. The ticket's status is left to the agent.
/// </summary>
public sealed class TicketWorkOrderObserver : IWorkOrderObserver
{
    private readonly ITimelineService _timeline;

    internal readonly IStringLocalizer S;

    public TicketWorkOrderObserver(ITimelineService timeline, IStringLocalizer<TicketWorkOrderObserver> stringLocalizer)
    {
        _timeline = timeline;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task OnEventAsync(WorkOrder workOrder, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken = default)
    {
        if (workOrder?.Source != WorkOrderSource.Ticket || string.IsNullOrEmpty(workOrder.SourceId))
        {
            return;
        }

        string body = null;

        if (eventName == WorkOrdersConstants.Events.Completed)
        {
            body = S["Work order {0} was completed.", workOrder.Number];
        }
        else if (eventName == WorkOrdersConstants.Events.StatusChanged && workOrder.Status == WorkOrderStatus.Canceled)
        {
            body = string.IsNullOrWhiteSpace(workOrder.CancelReason)
                ? S["Work order {0} was canceled.", workOrder.Number]
                : S["Work order {0} was canceled: {1}", workOrder.Number, workOrder.CancelReason.Trim()];
        }

        if (body is null)
        {
            return;
        }

        await _timeline.AddAsync(new TimelineEntry
        {
            Subject = new RecordRef(TicketsConstants.RecordKind, workOrder.SourceId),
            Kind = TicketsConstants.EntryKinds.Event,
            Visibility = TimelineVisibility.Internal,
            AuthorName = S["Work orders"],
            Body = body,
        }, cancellationToken);
    }
}
