using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OpportunityPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<OpportunityPart>
{
    private readonly INamedCatalog<OpportunityStage> _stages;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityPartSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stages">The opportunity stage catalog.</param>
    public OpportunityPartSettingsDisplayDriver(INamedCatalog<OpportunityStage> stages)
    {
        _stages = stages;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<OpportunityPartSettingsViewModel>("OpportunityPartSettings_Edit", async model =>
        {
            var settings = contentTypePartDefinition.GetSettings<OpportunityPartSettings>();
            var selected = (settings.StageIds ?? []).ToHashSet(StringComparer.Ordinal);

            model.StageIds = settings.StageIds ?? [];
            model.Stages = (await _stages.GetAllAsync())
                .OrderBy(stage => stage.Order)
                .ThenBy(stage => stage.Name, StringComparer.OrdinalIgnoreCase)
                .Select(stage => new SelectListItem(stage.Name, stage.ItemId, selected.Contains(stage.ItemId)))
                .ToList();
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new OpportunityPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var known = (await _stages.GetAllAsync()).Select(stage => stage.ItemId).ToHashSet(StringComparer.Ordinal);

        context.Builder.WithSettings(new OpportunityPartSettings
        {
            StageIds = (model.StageIds ?? [])
                .Where(known.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
        });

        return Edit(contentTypePartDefinition, context);
    }
}
