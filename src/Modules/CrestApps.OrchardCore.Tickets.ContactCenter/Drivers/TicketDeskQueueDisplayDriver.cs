using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Tickets.ContactCenter.Models;
using CrestApps.OrchardCore.Tickets.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.ContactCenter.Drivers;

/// <summary>
/// The Contact Center queue a desk offers its new tickets through, on the desk's editor.
/// </summary>
internal sealed class TicketDeskQueueDisplayDriver : DisplayDriver<TicketDesk>
{
    private readonly IActivityQueueManager _queues;

    public TicketDeskQueueDisplayDriver(IActivityQueueManager queues)
    {
        _queues = queues;
    }

    public override IDisplayResult Edit(TicketDesk desk, BuildEditorContext context)
        => Initialize<TicketDeskQueueViewModel>("TicketDeskQueue_Edit", async model =>
        {
            var current = desk.TryGet<TicketDeskQueueSettings>(out var settings) ? settings.QueueId : null;

            model.QueueId = current;
            model.Queues = [.. (await _queues.GetEnabledAsync())
                .OrderBy(queue => queue.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(queue => new SelectListItem(queue.Name, queue.ItemId, queue.ItemId == current))];
        }).Location("Content:7");

    public override async Task<IDisplayResult> UpdateAsync(TicketDesk desk, UpdateEditorContext context)
    {
        var model = new TicketDeskQueueViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // A queue that is gone or turned off when a ticket comes in offers nothing (TicketQueueRouter logs it), whether
        // it was deleted after the desk named it or a recipe named one this site doesn't have.
        desk.Put(new TicketDeskQueueSettings { QueueId = string.IsNullOrWhiteSpace(model.QueueId) ? null : model.QueueId });

        return Edit(desk, context);
    }
}
