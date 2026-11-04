using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using Json.Schema;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Recipes;

/// <summary>
/// Writes records as the Workforce for Contact Center recipe steps read them: whole, with their ids.
/// </summary>
internal static class ContactCenterRecipeExport
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static JsonArray Export<T>(IEnumerable<T> records)
        => new([.. records.Select(record => JsonSerializer.SerializeToNode(record, _options))]);
}

/// <summary>
/// Exports the adherence rule sets.
/// </summary>
public sealed class WorkforceAdherenceDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<WorkforceAdherenceDeploymentStep>("Workforce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<WorkforceAdherenceDeploymentStep>("Adherence rule sets");

    /// <inheritdoc/>
    public override string Name => AdherenceRecipeStep.AdherenceStepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class WorkforceAdherenceDeploymentSource : DeploymentSourceBase<WorkforceAdherenceDeploymentStep>
{
    private readonly AdherenceRuleSetManager _ruleSets;

    public WorkforceAdherenceDeploymentSource(AdherenceRuleSetManager ruleSets)
    {
        _ruleSets = ruleSets;
    }

    protected override async Task ProcessAsync(WorkforceAdherenceDeploymentStep step, DeploymentPlanResult result)
    {
        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["RuleSets"] = ContactCenterRecipeExport.Export(await _ruleSets.GetAllAsync()),
        });
    }
}

internal sealed class WorkforceAdherenceDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, WorkforceAdherenceDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(WorkforceAdherenceDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("WorkforceAdherenceDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("WorkforceAdherenceDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(WorkforceAdherenceDeploymentStep step, BuildEditorContext context)
        => View("WorkforceAdherenceDeploymentStep_Fields_Edit", step).Location("Content");
}

/// <summary>
/// Describes the adherence deployment step.
/// </summary>
internal sealed class WorkforceAdherenceDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(WorkforceAdherenceDeploymentStep);

    protected override string DisplayText => "Adherence rule sets";

    protected override string Description => "Exports every adherence rule set, with the teams it applies to and its rules.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}

/// <summary>
/// Exports the forecasting setup: planning groups, forecast events and capacity plans.
/// </summary>
public sealed class WorkforceForecastingDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<WorkforceForecastingDeploymentStep>("Workforce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<WorkforceForecastingDeploymentStep>("Forecasting setup");

    /// <inheritdoc/>
    public override string Name => ForecastingRecipeStep.ForecastingStepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class WorkforceForecastingDeploymentSource : DeploymentSourceBase<WorkforceForecastingDeploymentStep>
{
    private readonly PlanningGroupManager _groups;
    private readonly ForecastEventManager _events;
    private readonly CapacityPlanManager _plans;

    public WorkforceForecastingDeploymentSource(PlanningGroupManager groups, ForecastEventManager events, CapacityPlanManager plans)
    {
        _groups = groups;
        _events = events;
        _plans = plans;
    }

    protected override async Task ProcessAsync(WorkforceForecastingDeploymentStep step, DeploymentPlanResult result)
    {
        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["PlanningGroups"] = ContactCenterRecipeExport.Export(await _groups.GetAllAsync()),
            ["ForecastEvents"] = ContactCenterRecipeExport.Export(await _events.GetAllAsync()),
            ["CapacityPlans"] = ContactCenterRecipeExport.Export(await _plans.GetAllAsync()),
        });
    }
}

internal sealed class WorkforceForecastingDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, WorkforceForecastingDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(WorkforceForecastingDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("WorkforceForecastingDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("WorkforceForecastingDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(WorkforceForecastingDeploymentStep step, BuildEditorContext context)
        => View("WorkforceForecastingDeploymentStep_Fields_Edit", step).Location("Content");
}

/// <summary>
/// Describes the forecasting deployment step.
/// </summary>
internal sealed class WorkforceForecastingDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(WorkforceForecastingDeploymentStep);

    protected override string DisplayText => "Forecasting setup";

    protected override string Description => "Exports every planning group, forecast event and capacity plan.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
