using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Renders the metadata-driven model parameter editor for the <em>utility</em> deployment on the AI profile
/// template editor and persists the selected values onto <see cref="AIDeploymentParametersMetadata.UtilityValues"/>.
/// Profiles created from the template inherit these values.
/// </summary>
internal sealed class AIProfileTemplateUtilityModelParametersDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private const string BindingPrefix = "UtilityModelParameters";

    private readonly AIModelParameterViewService _viewService;

    public AIProfileTemplateUtilityModelParametersDisplayDriver(AIModelParameterViewService viewService)
    {
        _viewService = viewService;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<ModelParameterEditorViewModel>("AIModelParameters_Edit", async model =>
        {
            template.TryGet<AIDeploymentParametersMetadata>(out var metadata);

            var built = await _viewService.BuildAsync(metadata?.UtilityValues, "UtilityDeploymentName", "templateUtilityModelParameters", BindingPrefix);

            model.DeploymentFieldName = built.DeploymentFieldName;
            model.ElementPrefix = built.ElementPrefix;
            model.BindingPrefix = built.BindingPrefix;
            model.Parameters = built.Parameters;
            model.CapabilitiesJson = built.CapabilitiesJson;
            model.FeaturesJson = built.FeaturesJson;
        }).Location("Content:2.5%Deployments;2");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        var model = new ModelParameterEditorViewModel();

        await context.Updater.TryUpdateModelAsync(model, $"{Prefix}.{BindingPrefix}");

        var metadata = template.GetOrCreate<AIDeploymentParametersMetadata>();

        metadata.UtilityValues = (model.Values ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        template.Put(metadata);

        return Edit(template, context);
    }
}
