using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Recipes;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Drivers;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Models;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
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
        services.AddTransient<IConfigureOptions<PostgreSQLDataSourceOptions>, PostgreSQLDataSourceOptionsConfiguration>();
        services.AddDisplayDriver<AIDataSource, PostgreSQLAIDataSourceDisplayDriver>();

        // A data source's own connection string is held to the AI egress rule and, outside the default tenant, may not
        // sign in as the host; the global one is read only for the tables the host approved for the tenant (review 3).
        services.AddOptions<PostgreSQLSharedConnectionOptions>()
            .Configure<IShellConfiguration>((options, configuration) => configuration.GetSection(PostgreSQLSharedConnectionOptions.SectionName).Bind(options));
        services.TryAddSingleton<PostgreSQLConnectionGuard>();
        services.AddAIDataSourceSourceHandler<PostgreSQLAIDataSourceSourceHandler>(AIDataSourceSourceTypes.PostgreSQL);
        services.Configure<IngestionSourceSecretOptions>(options => options.Protect<AIDataSource>(nameof(PostgreSQLSourceMetadata), AIDataSourceProtectionConstants.SourceSecretPurpose));

        // This source reads rows it did not shape, and its handler extracts each document by the configured
        // TitleFieldName and ContentFieldName, so it has to ask which field is which.
        services.Configure<AIDataSourceFieldMappingOptions>(options => options.Require(AIDataSourceSourceTypes.PostgreSQL));
        services.Configure<AIDataSourceSourceOptions>(options => options.AddOrUpdate(
            AIDataSourceSourceTypes.PostgreSQL,
            S["PostgreSQL"],
            S["Read source documents from a PostgreSQL table using explicit or globally configured connection settings."]));
    }
}
