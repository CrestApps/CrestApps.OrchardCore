using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that runs an ad-hoc SQL query, when the SQL Queries feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.QueriesSql)]
public sealed class SqlQueriesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlQueriesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SqlQueriesToolsStartup(IStringLocalizer<SqlQueriesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<RunSqlQueryTool>(RunSqlQueryTool.TheName)
            .WithTitle(S["Run SQL Query"])
            .WithDescription(S["Runs an ad-hoc read-only SQL SELECT (Orchard SQL dialect, table prefix applied automatically, optional parameters) against the site's database and returns the rows. It can read any table, including user and settings records, so use it only for an explicit reporting or diagnostics request and always limit the rows."])
            .WithCategory(S["Query Management"])
            .RequireApproval()
            .Selectable();
    }
}
