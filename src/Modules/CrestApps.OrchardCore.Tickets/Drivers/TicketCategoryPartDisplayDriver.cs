using CrestApps.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// Edits what a ticket category gives its tickets, on the category's taxonomy term: desk, team, priority, service-level
/// policy and whether customers can choose it. Each value left empty comes from the category above.
/// </summary>
internal sealed class TicketCategoryPartDisplayDriver : ContentPartDisplayDriver<TicketCategoryPart>
{
    private const int MaxDescriptionLength = 500;

    private readonly ICatalog<TicketDesk> _desks;
    private readonly ICatalog<TicketPriority> _priorities;
    private readonly IServiceLevelPolicyManager _policies;

    public TicketCategoryPartDisplayDriver(ICatalog<TicketDesk> desks, ICatalog<TicketPriority> priorities, IServiceLevelPolicyManager policies)
    {
        _desks = desks;
        _priorities = priorities;
        _policies = policies;
    }

    public override IDisplayResult Edit(TicketCategoryPart part, BuildPartEditorContext context)
        => Initialize<TicketCategoryPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.DeskId = part.DeskId;
            model.TeamId = part.TeamId;
            model.PriorityId = part.PriorityId;
            model.ServiceLevelPolicyId = part.ServiceLevelPolicyId;
            model.HiddenFromCustomers = part.HiddenFromCustomers;
            model.CustomerDescription = part.CustomerDescription;
            model.Desks = [.. (await _desks.GetAllAsync()).OrderBy(desk => desk.Name, StringComparer.CurrentCultureIgnoreCase).Select(desk => new SelectListItem(desk.Name, desk.ItemId, desk.ItemId == part.DeskId))];
            model.Priorities = [.. (await _priorities.GetAllAsync()).OrderBy(priority => priority.Rank).Select(priority => new SelectListItem(priority.Name, priority.ItemId, priority.ItemId == part.PriorityId))];
            model.Policies = [.. (await _policies.ListByScopeAsync(TicketsConstants.ServiceLevelScope)).Select(policy => new SelectListItem(policy.Name, policy.ItemId, policy.ItemId == part.ServiceLevelPolicyId))];
        });

    public override async Task<IDisplayResult> UpdateAsync(TicketCategoryPart part, UpdatePartEditorContext context)
    {
        var model = new TicketCategoryPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.DeskId = Blank(model.DeskId);
        part.TeamId = Blank(model.TeamId);
        part.PriorityId = Blank(model.PriorityId);
        part.ServiceLevelPolicyId = Blank(model.ServiceLevelPolicyId);
        part.HiddenFromCustomers = model.HiddenFromCustomers;
        var description = Blank(model.CustomerDescription);
        part.CustomerDescription = description?.Length > MaxDescriptionLength ? description[..MaxDescriptionLength] : description;

        return Edit(part, context);
    }

    private static string Blank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
