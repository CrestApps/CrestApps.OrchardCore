using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Drivers;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.DataSources.PostgreSQL;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIDataSource, PostgreSQLAIDataSourceDisplayDriver>();
        services.AddKeyedScoped<IAIDataSourceSourceHandler, PostgreSQLAIDataSourceSourceHandler>(AIDataSourceSourceTypes.PostgreSQL);
        services.Configure<AIDataSourceSourceOptions>(options => options.AddOrUpdate(
            AIDataSourceSourceTypes.PostgreSQL,
            S["PostgreSQL"],
            S["Read source documents from a PostgreSQL table using explicit connection settings."]));
    }
}
