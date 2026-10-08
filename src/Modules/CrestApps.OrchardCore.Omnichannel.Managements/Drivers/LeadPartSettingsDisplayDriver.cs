using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class LeadPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<LeadPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadPartSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public LeadPartSettingsDisplayDriver(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<LeadPartSettingsViewModel>("LeadPartSettings_Edit", async model =>
        {
            var settings = contentTypePartDefinition.GetSettings<LeadPartSettings>();
            var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();

            model.TargetContactContentType = settings.TargetContactContentType;
            model.DefaultOpportunityContentType = settings.DefaultOpportunityContentType;
            model.ContactContentTypes = definitions
                .Where(OmnichannelRecordKinds.IsContact)
                .Where(definition => definition.Name != contentTypePartDefinition.ContentTypeDefinition?.Name)
                .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(definition => new SelectListItem(definition.DisplayName, definition.Name, definition.Name == settings.TargetContactContentType))
                .ToList();
            model.OpportunityContentTypes = definitions
                .Where(OmnichannelRecordKinds.IsOpportunity)
                .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(definition => new SelectListItem(definition.DisplayName, definition.Name, definition.Name == settings.DefaultOpportunityContentType))
                .ToList();
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new LeadPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(new LeadPartSettings
        {
            TargetContactContentType = string.IsNullOrWhiteSpace(model.TargetContactContentType) ? null : model.TargetContactContentType,
            DefaultOpportunityContentType = string.IsNullOrWhiteSpace(model.DefaultOpportunityContentType) ? null : model.DefaultOpportunityContentType,
        });

        return Edit(contentTypePartDefinition, context);
    }
}
