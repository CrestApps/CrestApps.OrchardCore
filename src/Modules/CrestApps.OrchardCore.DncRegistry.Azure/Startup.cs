using CrestApps.OrchardCore.DncRegistry.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DncRegistry.Azure;

/// <summary>
/// Keeps the local do-not-call list files.
/// </summary>
/// <remarks>
/// The store moves to Azure Blob Storage when the <c>CrestApps:DncRegistry:AzureBlobStorage</c> shell configuration section names a connection
/// string and a container; otherwise the local store of the parent feature stays in use.
/// </remarks>
public sealed class Startup : StartupBase
{
    /// <summary>
    /// The shell configuration section the Azure Blob Storage options are read from.
    /// </summary>
    internal const string ConfigurationSectionName = "CrestApps:DncRegistry:AzureBlobStorage";

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddTenantFileStore<ILocalDncFileStore>(
            "DncRegistry",
            ConfigurationSectionName,
            (serviceProvider, fileStore) => new LocalDncFileStore(fileStore),
            removeOnTenantRemoval: true);
}