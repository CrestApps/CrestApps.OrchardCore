using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Tickets.Core.Handlers;

/// <summary>
/// Removes a deleted ticket's timeline and stops its service-level timers, whichever way the item was removed (the
/// ticket service or Orchard's content pages), after the request's work is committed.
/// </summary>
public sealed class TicketContentCleanup : ContentHandlerBase
{
    /// <inheritdoc/>
    public override Task RemovedAsync(RemoveContentContext context)
    {
        if (context.NoActiveVersionLeft && context.ContentItem.Has<TicketPart>() && ShellScope.Current is not null)
        {
            var subject = new RecordRef(TicketsConstants.RecordKind, context.ContentItem.ContentItemId);

            ShellScope.AddDeferredTask(async scope =>
            {
                await scope.ServiceProvider.GetRequiredService<IServiceLevelTracker>().CancelAsync(subject);
                await scope.ServiceProvider.GetRequiredService<ITimelineService>().DeleteAllAsync(subject);
            });
        }

        return Task.CompletedTask;
    }
}
