using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list and run saved queries, when the Queries feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Queries)]
public sealed class QueriesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueriesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public QueriesToolsStartup(IStringLocalizer<QueriesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListQueriesTool>(ListQueriesTool.TheName)
            .WithTitle(S["List Queries"])
            .WithDescription(S["Lists the saved queries with name, source (Sql, Lucene, Elasticsearch), template, target index, result schema and whether they return content items, plus the query sources enabled on the site; supports a name filter and paging. Use it to find a query to run or edit."])
            .WithCategory(S["Query Management"])
            .Selectable();

        services.AddCoreAITool<RunQueryTool>(RunQueryTool.TheName)
            .WithTitle(S["Run Query"])
            .WithDescription(S["Executes a saved query by name with optional parameters and returns its rows or content items. Use it to answer data questions with queries an administrator already defined."])
            .WithCategory(S["Query Management"])
            .Selectable();
    }
}
