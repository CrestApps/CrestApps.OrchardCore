using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Approvals;

/// <summary>
/// Edits the approval steps of a ticket type, on the Ticket part's settings.
/// </summary>
internal sealed class TicketApprovalSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<TicketPart>
{
    private readonly ICatalog<TicketStatus> _statuses;

    internal readonly IStringLocalizer S;

    public TicketApprovalSettingsDisplayDriver(
        ICatalog<TicketStatus> statuses,
        IStringLocalizer<TicketApprovalSettingsDisplayDriver> stringLocalizer)
    {
        _statuses = statuses;
        S = stringLocalizer;
    }

    /// <summary>
    /// How many steps the editor offers.
    /// </summary>
    public const int MaxSteps = 3;

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
        => Initialize<TicketApprovalSettingsViewModel>("TicketApprovalSettings_Edit", async model =>
        {
            var settings = contentTypePartDefinition.GetSettings<TicketApprovalSettings>();

            model.WaitingStatusId = settings.WaitingStatusId;
            model.Statuses = [.. (await _statuses.GetAllAsync())
                .Where(status => status.Category is TicketStatusCategory.Pending or TicketStatusCategory.OnHold)
                .OrderBy(status => status.Order)
                .Select(status => new SelectListItem(status.Name, status.ItemId, status.ItemId == settings.WaitingStatusId))];

            model.Steps = [.. settings.Steps.Take(MaxSteps).Select(step => new TicketApprovalStepViewModel
            {
                Name = step.Name,
                ApproverIds = [.. step.ApproverIds],
                RequesterManager = step.RequesterManager,
            })];

            while (model.Steps.Count < MaxSteps)
            {
                model.Steps.Add(new TicketApprovalStepViewModel());
            }
        }).Location(l => l.Zone("Content", "6").Card(S["Approval"], "60"));

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new TicketApprovalSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // A step needs an approver or the requester's manager; a step left empty is dropped.
        context.Builder.WithSettings(new TicketApprovalSettings
        {
            WaitingStatusId = string.IsNullOrWhiteSpace(model.WaitingStatusId) ? null : model.WaitingStatusId,
            Steps = [.. (model.Steps ?? [])
                .Take(MaxSteps)
                .Select(step => new TicketApprovalStepDefinition
                {
                    Name = string.IsNullOrWhiteSpace(step.Name) ? null : step.Name.Trim(),
                    ApproverIds = [.. (step.ApproverIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)],
                    RequesterManager = step.RequesterManager,
                })
                .Where(step => step.ApproverIds.Count > 0 || step.RequesterManager)],
        });

        return Edit(contentTypePartDefinition, context);
    }
}
