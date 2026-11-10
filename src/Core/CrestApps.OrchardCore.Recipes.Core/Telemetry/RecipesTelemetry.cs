using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Recipes.Core.Telemetry;

/// <summary>
/// The OpenTelemetry instruments and spans of the recipes executed through <see cref="Services.RecipeExecutionService"/>,
/// under the meter and activity source named <c>CrestApps.OrchardCore.Recipes</c>.
/// </summary>
/// <remarks>
/// The recipe steps and deployment sources of named catalogs live in <c>CrestApps.OrchardCore.Core</c>, which does not
/// reference this project, so they create a meter and an activity source of the same name for their own instruments
/// (<c>crestapps.recipes.step.*</c> and <c>crestapps.recipes.deployment.*</c>). A listener subscribed to the name
/// receives both; the instrument names never overlap.
/// </remarks>
public static class RecipesTelemetry
{
    /// <summary>
    /// The area of the recipes instruments, which names their meter and activity source.
    /// </summary>
    public const string Area = "Recipes";

    /// <summary>
    /// The name of the span of one recipe execution.
    /// </summary>
    public const string ExecuteSpanName = "crestapps.recipes.execute";

    /// <summary>
    /// The name of the span of one deployment target handler importing the recipe, a child of <see cref="ExecuteSpanName"/>.
    /// </summary>
    public const string DeploymentTargetSpanName = "crestapps.recipes.deployment_target";

    /// <summary>
    /// The span tag that records the type name of the deployment target handler.
    /// </summary>
    public const string HandlerTag = "crestapps.recipes.handler";

    /// <summary>
    /// The span tag that records the number of steps of the recipe.
    /// </summary>
    public const string StepCountTag = "crestapps.recipes.steps";

    /// <summary>
    /// Gets the meter of the recipes instruments.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter(Area);

    /// <summary>
    /// Gets the activity source of the recipes spans.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource(Area);

    /// <summary>
    /// Gets the duration of recipe executions, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>.
    /// </summary>
    public static Histogram<double> ExecutionDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.recipes.execution.duration",
        "The duration of recipe executions, from writing the recipe to the last deployment target handler.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Gets the number of recipe executions, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>.
    /// </summary>
    public static Counter<long> Executions { get; } = Meter.CreateCounter<long>(
        "crestapps.recipes.executions",
        "{execution}",
        "The number of recipe executions, by how they ended.");

    /// <summary>
    /// Records one recipe execution.
    /// </summary>
    /// <param name="outcome">How the execution ended, such as <see cref="CrestAppsOrchardCoreTelemetry.OutcomeOk"/>.</param>
    public static void RecordExecution(string outcome)
    {
        if (!Executions.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Executions.Add(1, tags);
    }
}
