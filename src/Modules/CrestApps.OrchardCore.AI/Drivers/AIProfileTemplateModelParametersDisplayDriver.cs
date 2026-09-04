using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Renders the metadata-driven model parameter editor on the AI profile template editor and persists the
/// selected values onto <see cref="AIDeploymentParametersMetadata"/>. Profiles created from the template
/// inherit these values.
/// </summary>
internal sealed class AIProfileTemplateModelParametersDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly AIModelParameterViewService _viewService;

    public AIProfileTemplateModelParametersDisplayDriver(AIModelParameterViewService viewService)
    {
        _viewService = viewService;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<ModelParameterEditorViewModel>("AIModelParameters_Edit", async model =>
        {
            template.TryGet<AIDeploymentParametersMetadata>(out var metadata);

            var built = await _viewService.BuildAsync(metadata?.Values, "ChatDeploymentName", "templateModelParameters");

            model.DeploymentFieldName = built.DeploymentFieldName;
            model.ElementPrefix = built.ElementPrefix;
            model.Parameters = built.Parameters;
            model.CapabilitiesJson = built.CapabilitiesJson;
            model.FeaturesJson = built.FeaturesJson;
        }).Location("Content:2%Parameters;5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        var model = new ModelParameterEditorViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = template.GetOrCreate<AIDeploymentParametersMetadata>();

        metadata.Values = (model.Values ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        template.Put(metadata);

        return Edit(template, context);
    }
}
