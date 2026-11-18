using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.DataSources;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that save and delete AI data sources, when the AI Data Sources feature is on. They live here, not
/// in the Data Sources module with the list, sync and search tools, because they must refuse to change the data source
/// of the assistant running the turn, and that check (<c>AIConfigurationGuard</c>) belongs to this module. No tool takes
/// or returns a key, a password or a connection string.
/// </summary>
[RequireFeatures(AIConstants.Feature.DataSources)]
public sealed class AIDataSourcesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourcesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIDataSourcesToolsStartup(IStringLocalizer<AIDataSourcesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SaveAIDataSourceTool>(SaveAIDataSourceTool.TheName)
            .WithTitle(S["Save AI Data Source"])
            .WithDescription(S["Creates an AI data source of a given source type, or updates an existing one by id. Use it after listAIDataSources shows the source types; an existing data source whose configuration is locked can only be renamed. Prefer a data connection or the default connection over typing credentials; credentials are never taken."])
            .WithCategory(S["AI Knowledge"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteAIDataSourceTool>(DeleteAIDataSourceTool.TheName)
            .WithTitle(S["Delete AI Data Source"])
            .WithDescription(S["Deletes an AI data source. Profiles, web crawlers and file sources that point at it stop having knowledge to search; this cannot be undone."])
            .WithCategory(S["AI Knowledge"])
            .RequireApproval()
            .Selectable();
    }
}
