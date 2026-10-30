using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.SqlServer.Drivers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DataConnectors.SqlServer;

/// <summary>
/// Registers the SQL Server connector and its connection editor.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataConnector<SqlServerDataConnector>();
        services.AddDisplayDriver<DataConnection, SqlServerConnectionDisplayDriver>();
    }
}
