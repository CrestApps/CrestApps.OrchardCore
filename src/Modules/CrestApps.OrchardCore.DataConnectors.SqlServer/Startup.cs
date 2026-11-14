using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.SqlServer.Drivers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DataConnectors.SqlServer;

/// <summary>
/// Registers the SQL Server connector and its connection editor.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant's configuration, which the host's operator controls.</param>
    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<SqlServerHostIdentityOptions>(_shellConfiguration.GetSection(SqlServerHostIdentityOptions.SectionName));
        services.AddDataConnector<SqlServerDataConnector>();
        services.AddDisplayDriver<DataConnection, SqlServerConnectionDisplayDriver>();
        services.AddSingleton<IDataConnectionSecretTarget, SqlServerSecretTarget>();
    }
}
