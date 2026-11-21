using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ProtectedFiles.Azure;

/// <summary>
/// Keeps every protected file area in Azure Blob Storage.
/// </summary>
/// <remarks>
/// The areas move to Azure Blob Storage when the <c>CrestApps:ProtectedFiles:AzureBlobStorage</c> shell configuration
/// section names a connection string and a container; otherwise they stay in the tenant's App_Data folder.
/// </remarks>
public sealed class Startup : StartupBase
{
    /// <summary>
    /// The shell configuration section the Azure Blob Storage options are read from.
    /// </summary>
    internal const string ConfigurationSectionName = "CrestApps:ProtectedFiles:AzureBlobStorage";

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddProtectedFilesAzureBlobStorage(ConfigurationSectionName);
}
