using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Adds the lead settings to the subject action editor: the status every action type can move a lead to, and the
/// settings of the <c>Convert lead</c> action type.
/// </summary>
internal sealed class LeadSubjectActionDisplayDriver : DisplayDriver<SubjectAction>
{
    private readonly INamedCatalog<LeadStatus> _statuses;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadSubjectActionDisplayDriver"/> class.
    /// </summary>
    /// <param name="statuses">The lead status catalog.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadSubjectActionDisplayDriver(
        INamedCatalog<LeadStatus> statuses,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<LeadSubjectActionDisplayDriver> stringLocalizer)
    {
        _statuses = statuses;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SubjectAction action, BuildEditorContext context)
    {
        var results = new List<IDisplayResult>
        {
            Initialize<LeadSubjectActionViewModel>("LeadStatusSubjectAction_Edit", async model =>
            {
                model.SetLeadStatusId = action.TryGet<SetLeadStatusActionMetadata>(out var metadata) ? metadata.StatusId : null;
                model.LeadStatuses = (await _statuses.GetAllAsync())
                    .Where(status => !status.IsConverted)
                    .OrderBy(status => status.Order)
                    .Select(status => new SelectListItem(status.Name, status.ItemId, status.ItemId == model.SetLeadStatusId))
                    .ToList();
            }).Location("Content:101"),
        };

        if (IsConvertLead(action))
        {
            results.Add(Initialize<LeadSubjectActionViewModel>("ConvertLeadSubjectActionFields_Edit", async model =>
            {
                var metadata = action.TryGet<ConvertLeadActionMetadata>(out var stored) ? stored : new ConvertLeadActionMetadata();

                model.AccountMode = metadata.AccountMode;
                model.CreateOpportunity = metadata.CreateOpportunity;
                model.OpportunityContentType = metadata.OpportunityContentType;
                model.OpenActivities = metadata.OpenActivities;
                model.OpportunityContentTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                    .Where(OmnichannelRecordKinds.IsOpportunity)
                    .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(definition => new SelectListItem(definition.DisplayName, definition.Name, definition.Name == metadata.OpportunityContentType))
                    .ToList();
                model.AccountModes =
                [
                    new SelectListItem(S["Use the account named after the lead's company, or create it"], nameof(LeadConversionAccountMode.Automatic), metadata.AccountMode == LeadConversionAccountMode.Automatic),
                    new SelectListItem(S["Always create a new account"], nameof(LeadConversionAccountMode.CreateNew), metadata.AccountMode == LeadConversionAccountMode.CreateNew),
                    new SelectListItem(S["No account"], nameof(LeadConversionAccountMode.None), metadata.AccountMode == LeadConversionAccountMode.None),
                ];
            }).Location("Content:5"));
        }

        return Combine(results);
    }

    public override async Task<IDisplayResult> UpdateAsync(SubjectAction action, UpdateEditorContext context)
    {
        var model = new LeadSubjectActionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var statuses = await _statuses.GetAllAsync();
        var statusId = statuses.Any(status => status.ItemId == model.SetLeadStatusId && !status.IsConverted)
            ? model.SetLeadStatusId
            : null;

        action.Put(new SetLeadStatusActionMetadata { StatusId = statusId });

        if (IsConvertLead(action))
        {
            action.Put(new ConvertLeadActionMetadata
            {
                AccountMode = model.AccountMode,
                CreateOpportunity = model.CreateOpportunity,
                OpportunityContentType = string.IsNullOrWhiteSpace(model.OpportunityContentType) ? null : model.OpportunityContentType,
                OpenActivities = model.OpenActivities,
            });
        }

        return Edit(action, context);
    }

    private static bool IsConvertLead(SubjectAction action)
        => string.Equals(action.Source, OmnichannelConstants.ActionTypes.ConvertLead, StringComparison.OrdinalIgnoreCase);
}
