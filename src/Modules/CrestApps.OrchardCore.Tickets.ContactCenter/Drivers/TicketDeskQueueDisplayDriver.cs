using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Tickets.ContactCenter.Models;
using CrestApps.OrchardCore.Tickets.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.ContactCenter.Drivers;

/// <summary>
/// The Contact Center queue a desk offers its new tickets through, on the desk's editor.
/// </summary>
internal sealed class TicketDeskQueueDisplayDriver : DisplayDriver<TicketDesk>
{
    private readonly IActivityQueueManager _queues;

    internal readonly IStringLocalizer S;

    public TicketDeskQueueDisplayDriver(IActivityQueueManager queues, IStringLocalizer<TicketDeskQueueDisplayDriver> stringLocalizer)
    {
        _queues = queues;
        S = stringLocalizer;
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

        var queueId = string.IsNullOrWhiteSpace(model.QueueId) ? null : model.QueueId;

        if (queueId is not null && await _queues.FindByIdAsync(queueId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.QueueId), S["Choose a queue that exists."]);
        }
        else
        {
            desk.Put(new TicketDeskQueueSettings { QueueId = queueId });
        }

        return Edit(desk, context);
    }
}
