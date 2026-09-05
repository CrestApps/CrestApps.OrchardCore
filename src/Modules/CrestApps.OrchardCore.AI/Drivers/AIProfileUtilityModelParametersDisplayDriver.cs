using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Renders the metadata-driven model parameter editor for the <em>utility</em> deployment on the AI profile
/// editor and persists the selected values onto <see cref="UtilityDeploymentParametersMetadata"/>.
/// </summary>
internal sealed class AIProfileUtilityModelParametersDisplayDriver : DisplayDriver<AIProfile>
{
    private const string BindingPrefix = "UtilityModelParameters";

    private readonly AIModelParameterViewService _viewService;

    public AIProfileUtilityModelParametersDisplayDriver(AIModelParameterViewService viewService)
    {
        _viewService = viewService;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<ModelParameterEditorViewModel>("AIModelParameters_Edit", async model =>
        {
            profile.TryGet<UtilityDeploymentParametersMetadata>(out var metadata);

            var built = await _viewService.BuildAsync(metadata?.Values, "UtilityDeploymentName", "profileUtilityModelParameters", BindingPrefix);

            model.DeploymentFieldName = built.DeploymentFieldName;
            model.ElementPrefix = built.ElementPrefix;
            model.BindingPrefix = built.BindingPrefix;
            model.Parameters = built.Parameters;
            model.CapabilitiesJson = built.CapabilitiesJson;
            model.FeaturesJson = built.FeaturesJson;
        }).Location("Content:2.5%Deployments;2");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new ModelParameterEditorViewModel();

        await context.Updater.TryUpdateModelAsync(model, $"{Prefix}.{BindingPrefix}");

        var metadata = profile.GetOrCreate<UtilityDeploymentParametersMetadata>();

        metadata.Values = (model.Values ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        profile.Put(metadata);

        return Edit(profile, context);
    }
}
