using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// Edits the service-level policy an account's tickets run on.
/// </summary>
internal sealed class TicketAccountPartDisplayDriver : ContentPartDisplayDriver<TicketAccountPart>
{
    private readonly IServiceLevelPolicyManager _policies;

    public TicketAccountPartDisplayDriver(IServiceLevelPolicyManager policies)
    {
        _policies = policies;
    }

    public override IDisplayResult Edit(TicketAccountPart part, BuildPartEditorContext context)
        => Initialize<TicketAccountPartViewModel>("TicketAccountPart_Edit", async model =>
        {
            model.ServiceLevelPolicyId = part.ServiceLevelPolicyId;
            model.Policies = [.. (await _policies.ListByScopeAsync(TicketsConstants.ServiceLevelScope)).Select(policy => new SelectListItem(policy.Name, policy.ItemId, policy.ItemId == part.ServiceLevelPolicyId))];
        });

    public override async Task<IDisplayResult> UpdateAsync(TicketAccountPart part, UpdatePartEditorContext context)
    {
        var model = new TicketAccountPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.ServiceLevelPolicyId = string.IsNullOrWhiteSpace(model.ServiceLevelPolicyId) ? null : model.ServiceLevelPolicyId;

        return Edit(part, context);
    }
}
