using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.Tickets.Core.Handlers;

/// <summary>
/// Removes a deleted ticket's timeline and stops its service-level timers, whichever way the item was removed (the
/// ticket service or Orchard's content pages). The timeline is deleted by <see cref="TimelineDeletionStep"/>, asked for
/// in the removal's own transaction, so a failure is run again instead of leaving the entries and files behind; the
/// timers stop after the request's work is committed.
/// </summary>
public sealed class TicketContentCleanup : ContentHandlerBase
{
    private readonly ResumableSteps _steps;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketContentCleanup"/> class.
    /// </summary>
    public TicketContentCleanup(ResumableSteps steps, ISession session)
    {
        _steps = steps;
        _session = session;
    }

    /// <inheritdoc/>
    public override async Task RemovedAsync(RemoveContentContext context)
    {
        if (!context.NoActiveVersionLeft || !context.ContentItem.Has<TicketPart>())
        {
            return;
        }

        var subject = new RecordRef(TicketsConstants.RecordKind, context.ContentItem.ContentItemId);

        await TimelineDeletionStep.RequestAsync(_steps, subject);

        // The ticket's protected information goes with it, in the removal's own transaction. The documents are deleted
        // here, through the session: the protected information service notes changes on the timeline, whose services
        // need the content manager this handler is part of.
        var ticketId = context.ContentItem.ContentItemId;

        foreach (var item in await _session.Query<TicketProtectedItemDocument, TicketProtectedItemIndex>(index => index.TicketId == ticketId).ListAsync())
        {
            _session.Delete(item);
        }

        if (ShellScope.Current is not null)
        {
            ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IServiceLevelTracker>().CancelAsync(subject));
        }
    }
}
