using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Drivers;

/// <summary>
/// Display driver for the <see cref="ConvertLeadTask"/> workflow activity.
/// </summary>
public sealed class ConvertLeadTaskDisplayDriver : ActivityDisplayDriver<ConvertLeadTask, ConvertLeadTaskViewModel>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConvertLeadTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager, for the opportunity types.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager used to validate the lead expression.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ConvertLeadTaskDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<ConvertLeadTaskDisplayDriver> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(ConvertLeadTask activity, ConvertLeadTaskViewModel model)
    {
        model.LeadContentItemId = activity.LeadContentItemId;
        model.AccountMode = activity.AccountMode;
        model.CreateOpportunity = activity.CreateOpportunity;
        model.OpportunityContentType = activity.OpportunityContentType;
        model.OpenActivities = activity.OpenActivities;
        model.OpportunityContentTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(OmnichannelRecordKinds.IsOpportunity)
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(definition => new SelectListItem(definition.DisplayName, definition.Name, definition.Name == activity.OpportunityContentType))
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ConvertLeadTask activity, UpdateEditorContext context)
    {
        var model = new ConvertLeadTaskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.LeadContentItemId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.LeadContentItemId), S["The lead is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.LeadContentItemId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.LeadContentItemId), S["The lead expression is invalid."]);
        }

        activity.LeadContentItemId = model.LeadContentItemId?.Trim();
        activity.AccountMode = model.AccountMode;
        activity.CreateOpportunity = model.CreateOpportunity;
        activity.OpportunityContentType = string.IsNullOrWhiteSpace(model.OpportunityContentType) ? null : model.OpportunityContentType.Trim();
        activity.OpenActivities = model.OpenActivities;

        return Edit(activity, context);
    }
}
