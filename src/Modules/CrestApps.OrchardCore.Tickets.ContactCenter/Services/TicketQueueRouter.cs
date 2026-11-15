using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.ContactCenter.Models;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.ContactCenter.Services;

/// <summary>
/// Offers a desk's new tickets through its Contact Center queue: a new, unassigned ticket (not one waiting for
/// approval) becomes a task activity in the queue, linked both ways, and the queue offers it to an available agent with
/// the skills and capacity for it. When the ticket is given to someone by hand, solved, closed, merged or deleted before
/// an agent took it, the queued work is withdrawn; work an agent already took stays theirs to complete in the agent
/// workspace, like any activity.
/// </summary>
public sealed class TicketQueueRouter : ITicketObserver
{
    private readonly IServiceProvider _services;
    private readonly IOmnichannelActivityManager _activities;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    // The ticket service constructs its observers, this one among them, so the ticket service and anything that leads
    // back to it are resolved when used, not injected: the queue and withdrawal services publish Contact Center events,
    // the event outbox constructs every event handler, and TicketOfferAcceptedHandler uses the ticket service.
    public TicketQueueRouter(
        IServiceProvider services,
        IOmnichannelActivityManager activities,
        ICatalog<TicketDesk> desks,
        IClock clock,
        ILogger<TicketQueueRouter> logger)
    {
        _services = services;
        _activities = activities;
        _desks = desks;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CreatedAsync(ContentItem ticket, CancellationToken cancellationToken = default)
    {
        var part = ticket.GetOrCreate<TicketPart>();

        if (!string.IsNullOrEmpty(part.AssigneeId)
            || part.Approval?.State == TicketApprovalState.Pending
            || await QueueOfAsync(part.DeskId, cancellationToken) is not { } queueId)
        {
            return;
        }

        var now = _clock.UtcNow;
        var activity = await _activities.NewAsync(cancellationToken: cancellationToken);

        activity.Kind = ActivityKind.Task;
        activity.Source = ActivitySources.Api;
        activity.InteractionType = ActivityInteractionType.Manual;
        activity.Status = ActivityStatus.NotStated;
        activity.ScheduledUtc = now;
        activity.CreatedUtc = now;
        activity.Instructions = $"{part.Number} {ticket.DisplayText}".Trim();
        activity.Put(new TicketActivityLink { TicketId = ticket.ContentItemId });

        await _activities.CreateAsync(activity, cancellationToken: cancellationToken);
        await _services.GetRequiredService<IActivityQueueService>().EnqueueAsync(activity.ItemId, queueId, PriorityOf(part.PriorityRank), cancellationToken);

        // The ticket names the activity, which shows on its timeline and lets the router find it again.
        await _services.GetRequiredService<ITicketService>().LinkAsync(ticket.ContentItemId, new RecordRef(TicketsContactCenterConstants.ActivityRecordKind, activity.ItemId), TicketLinkKinds.Related, new TicketActor { Name = "Contact Center" }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task AssignedAsync(ContentItem ticket, string previousAssigneeId, CancellationToken cancellationToken = default)
        => ticket.GetOrCreate<TicketPart>().AssigneeId is { Length: > 0 } && TicketEventActor.Current?.Name != OfferAcceptedActorName
            ? WithdrawAsync(ticket, "The ticket was given to someone.", cancellationToken)
            : Task.CompletedTask;

    /// <inheritdoc/>
    public Task StatusChangedAsync(ContentItem ticket, TicketStatus previous, TicketStatus current, CancellationToken cancellationToken = default)
        => current?.Category is TicketStatusCategory.Solved or TicketStatusCategory.Closed
            ? WithdrawAsync(ticket, "The ticket was " + (current.Category == TicketStatusCategory.Solved ? "solved." : "closed."), cancellationToken)
            : Task.CompletedTask;

    /// <inheritdoc/>
    public Task MergedAsync(ContentItem ticket, ContentItem into, CancellationToken cancellationToken = default)
        => WithdrawAsync(ticket, "The ticket was merged into another.", cancellationToken);

    /// <inheritdoc/>
    public Task DeletedAsync(ContentItem ticket, CancellationToken cancellationToken = default)
        => WithdrawAsync(ticket, "The ticket was deleted.", cancellationToken);

    /// <summary>
    /// The name of the actor that assigns a ticket when an agent accepts its offer.
    /// </summary>
    public const string OfferAcceptedActorName = "Contact Center";

    /// <summary>
    /// The queue priority of a ticket priority's rank.
    /// </summary>
    /// <param name="rank">The rank, higher is more urgent.</param>
    /// <returns>The priority.</returns>
    public static InteractionPriority PriorityOf(int rank)
        => rank switch
        {
            <= 1 => InteractionPriority.Low,
            2 => InteractionPriority.Normal,
            3 => InteractionPriority.High,
            _ => InteractionPriority.Highest,
        };

    private async Task<string> QueueOfAsync(string deskId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(deskId)
            || await _desks.FindByIdAsync(deskId, cancellationToken) is not { } desk
            || !desk.TryGet<TicketDeskQueueSettings>(out var settings)
            || string.IsNullOrEmpty(settings.QueueId))
        {
            return null;
        }

        // A queue deleted or turned off after the desk named it, or one a recipe named that this site doesn't have, offers
        // nothing: the ticket waits on its desk like any other.
        if (await _services.GetRequiredService<IActivityQueueManager>().FindByIdAsync(settings.QueueId, cancellationToken) is not { Enabled: true })
        {
            _logger.LogWarning("The {Desk} desk names a Contact Center queue that doesn't exist or is turned off, so its new tickets aren't offered through it.", desk.Name.SanitizeLogValue());

            return null;
        }

        return settings.QueueId;
    }

    private async Task WithdrawAsync(ContentItem ticket, string reason, CancellationToken cancellationToken)
    {
        foreach (var link in ticket.GetOrCreate<TicketPart>().Links.Where(link => link.Record.Kind == TicketsContactCenterConstants.ActivityRecordKind))
        {
            try
            {
                var outcome = await _services.GetRequiredService<IQueuedWorkWithdrawalService>().WithdrawAsync(link.Record.Id, reason, ContactCenterActor.System, cancellationToken);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("The Contact Center work of ticket {TicketId}: {Outcome}.", ticket.ContentItemId, outcome);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "The Contact Center work of ticket {TicketId} couldn't be withdrawn.", ticket.ContentItemId);
            }
        }
    }
}
