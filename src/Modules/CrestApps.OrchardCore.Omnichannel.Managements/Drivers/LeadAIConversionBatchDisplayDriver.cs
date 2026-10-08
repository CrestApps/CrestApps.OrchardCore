using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Adds <c>Allow AI to convert the lead</c> to an automatic inventory load. The options show when the chosen record
/// type is a lead type, are stored on the load, and are copied onto each activity it loads.
/// </summary>
internal sealed class LeadAIConversionBatchDisplayDriver : DisplayDriver<OmnichannelActivityBatch>
{
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadAIConversionBatchDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentTypeProvider">The CRM content type provider.</param>
    /// <param name="contentDefinitionManager">The content definition manager, for the opportunity types.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadAIConversionBatchDisplayDriver(
        OmnichannelContentTypeProvider contentTypeProvider,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<LeadAIConversionBatchDisplayDriver> stringLocalizer)
    {
        _contentTypeProvider = contentTypeProvider;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    /// <summary>
    /// Keeps the options apart from the load's own fields.
    /// </summary>
    protected override void BuildPrefix(OmnichannelActivityBatch model, string htmlFieldPrefix)
    {
        Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? "LeadAIConversion" : $"{htmlFieldPrefix}.LeadAIConversion";
    }

    public override IDisplayResult Edit(OmnichannelActivityBatch batch, BuildEditorContext context)
    {
        // Only an automatic load has AI conversations to convert from.
        if (!IsAutomatic(batch))
        {
            return null;
        }

        return Initialize<LeadAIConversionViewModel>("LeadAIConversion_Edit", async model =>
        {
            var settings = batch.TryGet<LeadAIConversionSettings>(out var stored) ? stored : new LeadAIConversionSettings();

            model.Enabled = settings.Enabled;
            model.CreateOpportunity = settings.CreateOpportunity;
            model.OpportunityContentType = settings.OpportunityContentType;
            model.QualificationGuidance = settings.QualificationGuidance;
            model.LeadContentTypes = (await _contentTypeProvider.GetLeadContentTypesAsync()).ToArray();
            model.OpportunityContentTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                .Where(OmnichannelRecordKinds.IsOpportunity)
                .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(definition => new SelectListItem(definition.DisplayName, definition.Name, definition.Name == settings.OpportunityContentType))
                .ToList();
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelActivityBatch batch, UpdateEditorContext context)
    {
        if (!IsAutomatic(batch))
        {
            return null;
        }

        var model = new LeadAIConversionViewModel();

        if (await context.Updater.TryUpdateModelAsync(model, Prefix) && model.Rendered)
        {
            var createOpportunity = model.Enabled && model.CreateOpportunity;
            var opportunityType = string.IsNullOrWhiteSpace(model.OpportunityContentType) ? null : model.OpportunityContentType.Trim();

            // An AI conversion has nobody to ask, and a conversion that cannot find its opportunity type fails as a
            // whole, so the type is settled here, when the load is saved.
            if (createOpportunity && opportunityType is null && !await LeadTypeHasDefaultOpportunityAsync(batch.ContactContentType))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.OpportunityContentType), S["Choose the opportunity type. The lead type has no default opportunity type."]);
            }

            batch.Put(new LeadAIConversionSettings
            {
                Enabled = model.Enabled,
                CreateOpportunity = createOpportunity,
                OpportunityContentType = createOpportunity ? opportunityType : null,
                QualificationGuidance = string.IsNullOrWhiteSpace(model.QualificationGuidance) ? null : model.QualificationGuidance.Trim(),
            });
        }

        return Edit(batch, context);
    }

    private async Task<bool> LeadTypeHasDefaultOpportunityAsync(string leadType)
    {
        var definition = string.IsNullOrEmpty(leadType) ? null : await _contentDefinitionManager.GetTypeDefinitionAsync(leadType);
        var leadPart = definition?.Parts.FirstOrDefault(part => part.PartDefinition?.Name == OmnichannelConstants.ContentParts.Lead);

        return !string.IsNullOrEmpty(leadPart?.GetSettings<LeadPartSettings>()?.DefaultOpportunityContentType);
    }

    private static bool IsAutomatic(OmnichannelActivityBatch batch)
        => string.Equals(batch?.Source, ActivitySources.Automatic, StringComparison.OrdinalIgnoreCase);
}
