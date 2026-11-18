using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The row and the editor of a shared ticket view.
/// </summary>
internal sealed class TicketViewDisplayDriver : TicketSettingsDisplayDriverBase<TicketView>
{
    private readonly ICatalog<TicketDesk> _desks;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketViewDisplayDriver"/> class.
    /// </summary>
    /// <param name="desks">The desks.</param>
    public TicketViewDisplayDriver(ICatalog<TicketDesk> desks)
    {
        _desks = desks;
    }

    /// <inheritdoc/>
    protected override string Column => "Shared";

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketView view, BuildEditorContext context)
        => Initialize<TicketViewViewModel>("TicketViewFields_Edit", async model =>
        {
            var query = view.Query ?? new TicketQuery();

            model.Name = view.Name;
            model.Order = view.Order;
            model.SharedWithTeamId = view.SharedWithTeamId;
            model.Categories = [.. query.Categories];
            model.DeskIds = [.. query.DeskIds];
            model.Assignee = query.Assignee;
            model.DueWithinHours = query.DueWithin is { } within ? (int)within.TotalHours : null;
            model.Sort = query.Sort;
            model.Desks = [.. (await _desks.GetAllAsync()).OrderBy(desk => desk.Name).Select(desk => new SelectListItem(desk.Name, desk.ItemId, query.DeskIds.Contains(desk.ItemId)))];
        }).Location("Content:1");

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(TicketView view, UpdateEditorContext context)
    {
        var model = new TicketViewViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        view.Name = model.Name?.Trim();
        view.Order = model.Order;
        view.SharedWithTeamId = string.IsNullOrWhiteSpace(model.SharedWithTeamId) ? null : model.SharedWithTeamId;
        view.Query = new TicketQuery
        {
            Categories = [.. (model.Categories ?? []).Distinct()],
            DeskIds = [.. (model.DeskIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)],
            Assignee = string.IsNullOrWhiteSpace(model.Assignee) ? null : model.Assignee,
            DueWithin = model.DueWithinHours is int hours and > 0 ? TimeSpan.FromHours(hours) : null,
            Sort = model.Sort,
        };

        return Edit(view, context);
    }
}
