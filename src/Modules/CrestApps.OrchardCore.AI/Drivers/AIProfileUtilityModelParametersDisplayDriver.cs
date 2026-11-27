using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Renders the metadata-driven model parameter editor for the <em>utility</em> deployment on the AI profile
/// editor and persists the selected values onto <see cref="AIDeploymentParametersMetadata.UtilityValues"/>,
/// which the framework applies to utility completions.
/// </summary>
internal sealed class AIProfileUtilityModelParametersDisplayDriver : DisplayDriver<AIProfile>
{
    private const string BindingPrefix = "UtilityModelParameters";

    private readonly AIModelParameterViewService _viewService;

    internal readonly IStringLocalizer S;

    public AIProfileUtilityModelParametersDisplayDriver(
        AIModelParameterViewService viewService,
        IStringLocalizer<AIProfileUtilityModelParametersDisplayDriver> stringLocalizer)
    {
        _viewService = viewService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<ModelParameterEditorViewModel>("AIModelParameters_Edit", async model =>
        {
            profile.TryGet<AIDeploymentParametersMetadata>(out var metadata);

            var built = await _viewService.BuildAsync(metadata?.UtilityValues, "UtilityDeploymentName", "profileUtilityModelParameters", BindingPrefix);

            model.DeploymentFieldName = built.DeploymentFieldName;
            model.ElementPrefix = built.ElementPrefix;
            model.BindingPrefix = built.BindingPrefix;
            model.Parameters = built.Parameters;
            model.CapabilitiesJson = built.CapabilitiesJson;
            model.FeaturesJson = built.FeaturesJson;
        }).Location(l => l.Zone("Content", "2.5").Card(S["Deployments & Interactions"], "2"));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new ModelParameterEditorViewModel();

        await context.Updater.TryUpdateModelAsync(model, $"{Prefix}.{BindingPrefix}");

        var metadata = profile.GetOrCreate<AIDeploymentParametersMetadata>();

        metadata.UtilityValues = (model.Values ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        profile.Put(metadata);

        return Edit(profile, context);
    }
}
