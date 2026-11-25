using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The desk's priority matrix on its editor: the priority a ticket gets for each impact and urgency. The desk's handler
/// checks the cells, for the editor and recipes alike.
/// </summary>
internal sealed class TicketDeskPriorityMatrixDisplayDriver : DisplayDriver<TicketDesk>
{
    private readonly ICatalog<TicketPriority> _priorities;

    internal readonly IStringLocalizer S;

    public TicketDeskPriorityMatrixDisplayDriver(
        ICatalog<TicketPriority> priorities,
        IStringLocalizer<TicketDeskPriorityMatrixDisplayDriver> stringLocalizer)
    {
        _priorities = priorities;
        S = stringLocalizer;
    }

    /// <summary>
    /// The index of a cell of the matrix in <see cref="TicketDeskPriorityMatrixViewModel.Cells"/>.
    /// </summary>
    public static int IndexOf(TicketLevel impact, TicketLevel urgency)
        => (((int)impact - 1) * 3) + ((int)urgency - 1);

    public override IDisplayResult Edit(TicketDesk desk, BuildEditorContext context)
        => Initialize<TicketDeskPriorityMatrixViewModel>("TicketDeskPriorityMatrix_Edit", async model =>
        {
            model.Cells = [.. Enumerable.Repeat<string>(null, 9)];

            foreach (var rule in (desk.PriorityMatrix ?? []).Where(rule => !string.IsNullOrEmpty(rule.PriorityId) && Enum.IsDefined(rule.Impact) && Enum.IsDefined(rule.Urgency)))
            {
                model.Cells[IndexOf(rule.Impact, rule.Urgency)] = rule.PriorityId;
            }
            model.Priorities = [.. (await _priorities.GetAllAsync())
                .OrderByDescending(priority => priority.Rank)
                .Select(priority => new SelectListItem(priority.Name, priority.ItemId))];
        }).Location(l => l.Zone("Content", "2").Card(S["Priority Matrix"], "60"));

    public override async Task<IDisplayResult> UpdateAsync(TicketDesk desk, UpdateEditorContext context)
    {
        var model = new TicketDeskPriorityMatrixViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var rules = new List<TicketPriorityRule>();

        foreach (var impact in Enum.GetValues<TicketLevel>())
        {
            foreach (var urgency in Enum.GetValues<TicketLevel>())
            {
                var index = IndexOf(impact, urgency);
                var priorityId = index < model.Cells.Count ? model.Cells[index] : null;

                if (!string.IsNullOrEmpty(priorityId))
                {
                    rules.Add(new TicketPriorityRule { Impact = impact, Urgency = urgency, PriorityId = priorityId });
                }
            }
        }

        desk.PriorityMatrix = rules;

        return Edit(desk, context);
    }
}
