using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The row and the editor of a desk.
/// </summary>
internal sealed class TicketDeskDisplayDriver : TicketSettingsDisplayDriverBase<TicketDesk>
{
    private readonly IServiceLevelPolicyManager _policies;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketDeskDisplayDriver"/> class.
    /// </summary>
    /// <param name="policies">The service-level policies.</param>
    /// <param name="contentDefinitionManager">The content definition manager, for the ticket types.</param>
    public TicketDeskDisplayDriver(IServiceLevelPolicyManager policies, IContentDefinitionManager contentDefinitionManager)
    {
        _policies = policies;
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <inheritdoc/>
    protected override string Column => "Audience";

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketDesk desk, BuildEditorContext context)
        => Initialize<TicketDeskViewModel>("TicketDeskFields_Edit", async model =>
        {
            model.Name = desk.Name;
            model.Description = desk.Description;
            model.Audience = desk.Audience;
            model.DefaultTeamId = desk.DefaultTeamId;
            model.HoursScheduleId = desk.HoursScheduleId;
            model.ServiceLevelPolicyId = desk.ServiceLevelPolicyId;
            model.TicketTypes = [.. desk.TicketTypes];
            model.ReopenWindowDays = desk.ReopenWindowDays;
            model.AutoCloseDays = desk.AutoCloseDays;
            model.NotifyRequester = desk.NotifyRequester;
            model.NotifyAssignee = desk.NotifyAssignee;
            model.Policies = [.. (await _policies.ListByScopeAsync(TicketsConstants.ServiceLevelScope)).Select(policy => new SelectListItem(policy.Name, policy.ItemId, policy.ItemId == desk.ServiceLevelPolicyId))];
            model.TicketTypeOptions = [.. (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                .Where(type => type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
                .Select(type => new SelectListItem(type.DisplayName, type.Name, desk.TicketTypes.Contains(type.Name, StringComparer.OrdinalIgnoreCase)))];
        }).Location("Content:1");

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(TicketDesk desk, UpdateEditorContext context)
    {
        var model = new TicketDeskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        desk.Name = model.Name?.Trim();
        desk.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        desk.Audience = model.Audience;
        desk.DefaultTeamId = string.IsNullOrWhiteSpace(model.DefaultTeamId) ? null : model.DefaultTeamId;
        desk.HoursScheduleId = string.IsNullOrWhiteSpace(model.HoursScheduleId) ? null : model.HoursScheduleId;
        desk.ServiceLevelPolicyId = string.IsNullOrWhiteSpace(model.ServiceLevelPolicyId) ? null : model.ServiceLevelPolicyId;
        desk.TicketTypes = [.. (model.TicketTypes ?? []).Where(type => !string.IsNullOrWhiteSpace(type)).Distinct(StringComparer.OrdinalIgnoreCase)];
        desk.ReopenWindowDays = model.ReopenWindowDays;
        desk.AutoCloseDays = model.AutoCloseDays;
        desk.NotifyRequester = model.NotifyRequester;
        desk.NotifyAssignee = model.NotifyAssignee;

        return Edit(desk, context);
    }
}
