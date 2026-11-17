using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.DataPipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list the data pipeline step types and the data sets a pipeline can read, when the Data
/// Pipelines feature is on: it declares the data pipeline permissions and registers the step types.
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

        services.AddCoreAITool<ListDataSetsTool>(ListDataSetsTool.TheName)
            .WithTitle(S["List Data Sets"])
            .WithDescription(S["Lists the data sets the current user may read (content types, users, saved queries and module data), with their Source::DataSet value, name, description, data source and group; optional text and data source filter. Use it to pick what a \"Read a data source\" step reads."])
            .WithCategory(S["Data Pipelines"])
            .Selectable();

        services.AddCoreAITool<GetDataSetSchemaTool>(GetDataSetSchemaTool.TheName)
            .WithTitle(S["Get Data Set Schema"])
            .WithDescription(S["Returns the fields of one data set with their names, labels and types. Use it before writing the fields, filters and formulas of a pipeline that reads it."])
            .WithCategory(S["Data Pipelines"])
            .Selectable();
    }
}
