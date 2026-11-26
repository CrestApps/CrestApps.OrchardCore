using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The row and the editor of a shared ticket view.
/// </summary>
internal sealed class TicketViewDisplayDriver : TicketSettingsDisplayDriverBase<TicketView>
{
    private readonly ICatalog<TicketDesk> _desks;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketViewDisplayDriver"/> class.
    /// </summary>
    /// <param name="desks">The desks.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketViewDisplayDriver(
        ICatalog<TicketDesk> desks,
        IStringLocalizer<TicketViewDisplayDriver> stringLocalizer)
    {
        _desks = desks;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string Column => "Shared";

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(TicketView view, BuildEditorContext context)
    {
        var model = new TicketViewViewModel();

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

        // Every card edits a copy of the same model under the same prefix, so UpdateAsync still binds it once.
        void Populate(TicketViewViewModel target)
        {
            target.Name = model.Name;
            target.Order = model.Order;
            target.SharedWithTeamId = model.SharedWithTeamId;
            target.Categories = model.Categories;
            target.DeskIds = model.DeskIds;
            target.Assignee = model.Assignee;
            target.DueWithinHours = model.DueWithinHours;
            target.Sort = model.Sort;
            target.Desks = model.Desks;
        }

        return Combine(
            Initialize<TicketViewViewModel>("TicketViewFields_Edit", Populate)
                .Location(l => l.Zone("Content", "1").Card(S["View"], "10")),
            Initialize<TicketViewViewModel>("TicketViewFilters_Edit", Populate)
                .Location(l => l.Zone("Content", "1").Card(S["Which Tickets It Lists"], "20")));
    }

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

        return await EditAsync(view, context);
    }
}
