using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Diagnostics;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Core.Deployments;

/// <summary>
/// Exports every entry of a named catalog as one recipe step that holds the entries in one array.
/// </summary>
/// <remarks>
/// The step is named after the deployment step, and each entry is written by <see cref="CatalogDeploymentSerializer"/>,
/// so the members the environment owns are left out. A <see cref="Recipes.NamedCatalogRecipeStep{TEntry}"/> with the
/// same step and array names imports it.
/// <para>
/// Every export is measured: a <c>crestapps.recipes.deployment.export</c> span, and the number of entries written on
/// <c>crestapps.recipes.deployment.entries</c>.
/// </para>
/// </remarks>
/// <typeparam name="TStep">The deployment step type.</typeparam>
/// <typeparam name="TEntry">The catalog entry type.</typeparam>
public abstract class NamedCatalogDeploymentSource<TStep, TEntry> : CatalogDeploymentSource<TStep, TEntry>
    where TStep : DeploymentStep
    where TEntry : CatalogItem, INameAwareModel, new()
{
    private readonly string _entriesName;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedCatalogDeploymentSource{TStep, TEntry}"/> class.
    /// </summary>
    /// <param name="entriesName">The name of the step member that holds the array of entries.</param>
    /// <param name="manager">The manager the entries are read through.</param>
    protected NamedCatalogDeploymentSource(
        string entriesName,
        INamedCatalogManager<TEntry> manager)
        : base(entriesName, manager)
    {
        _entriesName = entriesName;
    }

    /// <inheritdoc/>
    protected override async Task ProcessAsync(TStep step, DeploymentPlanResult result)
    {
        using var telemetry = TelemetryScope.Start(RecipeStepTelemetry.ActivitySource, RecipeStepTelemetry.DeploymentSpanName, duration: null);
        telemetry.SetSpanTag(RecipeStepTelemetry.StepSpanTag, step?.Name);

        try
        {
            await base.ProcessAsync(step, result);
            telemetry.Succeed();
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);

            throw;
        }

        // The base source adds its step last, with the entries under the array name.
        var count = result.Steps.Count > 0 && result.Steps[result.Steps.Count - 1]?[_entriesName] is JsonArray entries
            ? entries.Count
            : 0;

        telemetry.SetSpanTag("crestapps.recipes.entries", count);
        RecipeStepTelemetry.RecordDeploymentEntries(step?.Name, count);
    }
}
