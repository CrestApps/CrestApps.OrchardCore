using CrestApps.OrchardCore.ContentTransfer.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContentTransfer.Azure;

/// <summary>
/// Keeps the Content Transfer import and export files.
/// </summary>
/// <remarks>
/// The store moves to Azure Blob Storage when the <c>CrestApps:ContentTransfer:AzureBlobStorage</c> shell configuration section names a connection
/// string and a container; otherwise the local store of the parent feature stays in use.
/// </remarks>
public sealed class Startup : StartupBase
{
    /// <summary>
    /// The shell configuration section the Azure Blob Storage options are read from.
    /// </summary>
    internal const string ConfigurationSectionName = "CrestApps:ContentTransfer:AzureBlobStorage";

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddTenantFileStore<IContentTransferFileStore>(
            "Temp",
            ConfigurationSectionName,
            (serviceProvider, fileStore) => new ContentTransferFileStore(fileStore),
            removeOnTenantRemoval: true);
}