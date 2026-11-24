using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.DataPipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that lists the data pipeline step types when the Data Pipelines feature is on: it declares the
/// data pipeline permissions and registers the step types. The data sets a pipeline can read are listed by the Report
/// Builder's listReportDataSets and getReportDataSetSchema, which work whether or not Data Pipelines is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelines)]
public sealed class DataPipelinesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataPipelinesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DataPipelinesToolsStartup(IStringLocalizer<DataPipelinesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListDataPipelineStepTypesTool>(ListDataPipelineStepTypesTool.TheName)
            .WithTitle(S["List Data Pipeline Step Types"])
            .WithDescription(S["Lists the step types available on this site by category (source, transform, file, destination) with their input and output ports."])
            .WithCategory(S["Data Pipelines"])
            .Selectable();
    }
}
